using System.Security.Cryptography;
using System.Text;

namespace Extension.Security.Licensing;

/// <summary>
/// 客户端与宿主共用的授权文件存储。每次加载都重新验签，不保存“已激活”标记。
/// 按公钥、区分大小写的产品标识和 Windows 用户隔离。
/// 时间记录错误拒绝授权，其余 IO 错误向调用方报告；不能防止删除全部文件或磁盘快照回滚。
/// </summary>
public sealed class LicenseFileStore
{
    private const int MaximumFileBytes = 1024 * 1024;
    private readonly string _publicKey;
    private readonly string _productId;
    private readonly TimeProvider _timeProvider;
    private readonly LicenseTimeStore _timeStore;
    private readonly object _clockGate = new();
    private DateTimeOffset? _clockAnchorUtc;
    private long _clockAnchorTimestamp;
    public string LicensePath { get; }
    public string ProductId => _productId;

    /// <param name="publicKeyBase64">由宿主信任并提供的 Base64 DER RSA 公钥，不应让客户选择。</param>
    /// <param name="storageRoot">默认当前用户 LocalApplicationData/SoftwareFrame/Licensing；测试可提供隔离目录。</param>
    public LicenseFileStore(string publicKeyBase64, string? storageRoot = null, TimeProvider? timeProvider = null,
        string productId = LicenseProduct.DefaultId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(publicKeyBase64);
        LicenseProduct.Validate(productId);
        _productId = productId;
        using var rsa = RSA.Create();
        byte[] keyBytes = Convert.FromBase64String(publicKeyBase64);
        rsa.ImportRSAPublicKey(keyBytes, out int consumed);
        if (consumed != keyBytes.Length)
            throw new ArgumentException("公钥文件包含多余数据。", nameof(publicKeyBase64));
        byte[] canonical = rsa.ExportRSAPublicKey();
        _publicKey = Convert.ToBase64String(canonical);
        string fingerprint = Convert.ToHexString(SHA256.HashData(canonical));
        storageRoot ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SoftwareFrame", "Licensing");
        ArgumentException.ThrowIfNullOrWhiteSpace(storageRoot);
        string productFingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(productId)));
        LicensePath = Path.GetFullPath(Path.Combine(storageRoot, fingerprint, productFingerprint, "license.lic"));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _timeStore = new LicenseTimeStore(LicensePath + ".time", SHA256.HashData(Encoding.UTF8.GetBytes($"{_publicKey}|{productId}")));
    }

    public LicenseVerifyResult Load(string? requiredFeature = null) => Check(false, requiredFeature).Verification;

    /// <summary>重新验签及检查时间。claimReminder 为真时先持久化当天提醒标记；调用方负责显示提醒。</summary>
    public LicenseCheckResult Check(bool claimReminder = false, string? requiredFeature = null)
    {
        LicenseVerifyResult verified;
        try
        {
            verified = LicenseManagerEx.Verify(ReadBounded(LicensePath), _publicKey, requiredFeature: requiredFeature, expectedProductId: _productId);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return new(new LicenseVerifyResult { Status = LicenseStatus.Malformed, Message = "尚未导入授权，请联系供应商。" }, false);
        }
        return ApplyTime(verified, false, claimReminder);
    }

    private LicenseCheckResult ApplyTime(LicenseVerifyResult verified, bool allowInitialize, bool claimReminder)
    {
        if (verified.License == null || verified.License.IsPermanent ||
            verified.Status is not (LicenseStatus.Valid or LicenseStatus.Expired))
            return new(verified, false);
        try
        {
            var now = _timeProvider.GetLocalNow();
            if (!ClockProgressIsValid(now))
                return new(new LicenseVerifyResult { Status = LicenseStatus.ClockRollback, License = verified.License,
                    Message = "系统时间进度异常，请校正时间或联系供应商。" }, false);
            var timed = _timeStore.Check(verified.License, now, allowInitialize, claimReminder);
            if (timed.Status == LicenseTimeStatus.ClockRollback)
                return new(new LicenseVerifyResult { Status = LicenseStatus.ClockRollback, License = verified.License,
                    Message = "系统时间早于授权历史记录，请校正时间或联系供应商。" }, false);
            if (timed.Status == LicenseTimeStatus.Expired)
                return new(new LicenseVerifyResult { Status = LicenseStatus.Expired, License = verified.License,
                    Message = "软件使用期限已到期，请联系供应商。" }, false);
            return new(verified, verified.Ok && timed.ShouldRemind);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or CryptographicException or UnauthorizedAccessException
                                      or System.Security.SecurityException)
        {
            return new(new LicenseVerifyResult { Status = LicenseStatus.TimeStateInvalid, License = verified.License,
                Message = $"授权时间记录不可用，请联系供应商：{ex.Message}" }, false);
        }
    }

    // 同一实例内用单调计时检查墙上时钟是否停止/减慢。两秒仅容忍采样抖动，
    // 不在每次检查后重置基准，避免多次小幅回拨不断获得容差。
    private bool ClockProgressIsValid(DateTimeOffset now)
    {
        lock (_clockGate)
        {
            long timestamp = _timeProvider.GetTimestamp();
            var utc = now.ToUniversalTime();
            if (_clockAnchorUtc.HasValue)
            {
                var elapsed = _timeProvider.GetElapsedTime(_clockAnchorTimestamp, timestamp);
                if (elapsed < TimeSpan.Zero || elapsed > DateTimeOffset.MaxValue - _clockAnchorUtc.Value)
                    return false;
                var expected = _clockAnchorUtc.Value + elapsed;
                if (expected - utc > TimeSpan.FromSeconds(2)) return false;
                if (utc <= expected) return true;
            }
            _clockAnchorUtc = utc;
            _clockAnchorTimestamp = timestamp;
            return true;
        }
    }

    /// <summary>生成一次性恢复请求。新请求会替换未完成的旧请求；此操作不会改变授权时间记录。</summary>
    public string CreateTimeRecoveryRequest() => _timeStore.CreateRecoveryRequest(MachineCodeEx.GetMachineCode(), _productId);

    /// <summary>先验证再原子替换。普通续期不重置历史时间；显式厂商签名恢复授权可消费一次性请求恢复时间。</summary>
    public LicenseVerifyResult ImportFile(string sourcePath, string? requiredFeature = null)
    {
        string text = ReadBounded(sourcePath);
        var result = LicenseManagerEx.Verify(text, _publicKey, requiredFeature: requiredFeature, expectedProductId: _productId);
        if (!result.Ok) return result;
        if (result.License!.Extra.TryGetValue(LicenseRecoveryRequest.ExtraKey, out string? request))
        {
            try
            {
                request ??= string.Empty;
                string requestedMachine = LicenseRecoveryRequest.GetMachineCode(request);
                if (LicenseRecoveryRequest.GetProductId(request) != _productId ||
                    result.License.IsPermanent || !string.Equals(requestedMachine, result.License.MachineCode, StringComparison.OrdinalIgnoreCase)
                    || !MachineCodeEx.IsCurrentMachine(requestedMachine))
                    throw new InvalidDataException("恢复授权必须绑定请求所属机器，且必须为限时授权。");
                _timeStore.Recover(result.License, _timeProvider.GetLocalNow(), request);
                lock (_clockGate) _clockAnchorUtc = null;
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or CryptographicException or UnauthorizedAccessException
                                          or System.Security.SecurityException or ArgumentException)
            {
                return new LicenseVerifyResult { Status = LicenseStatus.TimeStateInvalid, License = result.License,
                    Message = $"无法恢复授权时间记录：{ex.Message}" };
            }
        }
        else
        {
            result = ApplyTime(result, !File.Exists(LicensePath), false).Verification;
            if (!result.Ok) return result;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(LicensePath)!);
        string temporary = LicensePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                file.Write(Encoding.ASCII.GetBytes(text));
                file.Flush(flushToDisk: true);
            }
            File.Move(temporary, LicensePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
        return result;
    }

    private static string ReadBounded(string path)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        long length = file.Length;
        if (length > MaximumFileBytes)
            throw new InvalidDataException("授权文件过大。");
        var bytes = new byte[checked((int)length)];
        file.ReadExactly(bytes);
        // UTF-8 BOM 只用于文件编码，不属于签名文本。
        return Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF').Trim();
    }
}
