using System.IO;
using System.Text;
using Extension.Security.Licensing;

namespace Licensing.Native.Client;

/// <summary>
/// 客户端授权会话。
/// 类型名与命名空间沿用历史（消费者已依赖），但实现已改为**直接调用托管授权核心**
/// <see cref="LicenseFileStore"/>，不再经过 Native AOT 组件 —— 发布客户包因此不需要 C++ 链接器，
/// 任何装了 .NET SDK 的机器都能产出。
/// 安全性不建立在代码加密上：客户包里没有私钥，反编译只能看到校验逻辑，伪造不了授权；
/// 机器码绑定与时间防回拨同样由托管核心完成。
/// </summary>
public sealed class NativeLicenseSession : IDisposable
{
    private readonly object _gate = new();
    private readonly LicenseFileStore _store;
    private bool _disposed;

    public string ProductId => _store.ProductId;
    public string LicensePath => _store.LicensePath;

    public NativeLicenseSession(string trustedPublicKey, string productId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trustedPublicKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(productId);
        // 公钥必须是宿主信任并提供的 Base64 DER；LicenseFileStore 会校验多余数据并做规范化。
        _store = new LicenseFileStore(trustedPublicKey, productId: productId);
    }

    public static string GetMachineCode() => MachineCodeEx.GetMachineCode();

    public NativeVerifyResult Load() => Check().Verification;

    public NativeCheckResult Check(bool claimReminder = false, string? requiredFeature = null)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var result = _store.Check(claimReminder, requiredFeature);
            return new NativeCheckResult
            {
                Verification = NativeVerifyResult.FromCore(result.Verification),
                ShouldRemind = result.ShouldRemind,
                LicensePath = _store.LicensePath
            };
        }
    }

    public NativeVerifyResult ImportFile(string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return NativeVerifyResult.FromCore(_store.ImportFile(sourcePath));
        }
    }

    /// <summary>
    /// 从授权文本导入。核心只接受文件路径，因此清洗后写临时文件桥接，返回前删除。
    /// 授权文本本身不含私钥；清洗会去掉复制粘贴带进来的空白、换行、全角空格和 BOM。
    /// 这里只做格式粗筛，验签、产品、机器和时间判断仍由核心完成。
    /// </summary>
    public NativeVerifyResult ImportText(string licenseText)
    {
        ArgumentNullException.ThrowIfNull(licenseText);
        string normalized = NormalizeText(licenseText);
        if (normalized.Length == 0) return TextFailure("没有可导入的授权内容，请确认已经粘贴完整。");
        if (normalized.Length > MaximumTextLength) return TextFailure("授权内容过长，请确认只粘贴了授权本身。");
        if (!IsWellFormed(normalized))
            return TextFailure("这不是本软件的授权内容：应形如 SF2.<载荷>.<签名>，请确认复制的是授权文件中的全部文本。");
        string path = Path.Combine(Path.GetTempPath(), "sf-license-" + Guid.NewGuid().ToString("N") + ".lic");
        try
        {
            File.WriteAllText(path, normalized, Encoding.ASCII);
            return ImportFile(path);
        }
        finally
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>
    /// 删除本机授权文件。保留时间状态（时钟回拨检测不能被“清除”动作重置），也不解除机器绑定：
    /// 重新使用仍需供应商按本机机器码签发的授权。没有授权文件时返回 false。
    /// </summary>
    public bool ClearLocalLicense()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (string.IsNullOrEmpty(_store.LicensePath) || !File.Exists(_store.LicensePath)) return false;
            File.Delete(_store.LicensePath);
            return true;
        }
    }

    public string CreateTimeRecoveryRequest()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _store.CreateTimeRecoveryRequest();
        }
    }

    public void Dispose()
    {
        lock (_gate) _disposed = true;
    }

    private const int MaximumTextLength = 64 * 1024;

    // Base64Url 不含空白；char.IsWhiteSpace 已覆盖全角空格(U+3000)和不换行空格(U+00A0)。
    private static string NormalizeText(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (char character in text)
        {
            if (character == '\uFEFF' || char.IsWhiteSpace(character)) continue;
            builder.Append(character);
        }
        return builder.ToString();
    }

    private static bool IsWellFormed(string normalized)
    {
        if (!normalized.StartsWith("SF2.", StringComparison.Ordinal)) return false;
        foreach (char character in normalized)
            if (character > '\u007F') return false;
        string[] parts = normalized.Split('.');
        return parts.Length == 3 && parts[1].Length > 0 && parts[2].Length > 0;
    }

    private static NativeVerifyResult TextFailure(string message) =>
        new() { Status = NativeLicenseStatus.Malformed, Message = message };
}
