using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Extension.Security.Licensing;

/// <summary>当前用户 DPAPI 保护的时间记录。不能防止同一用户解密、删除全部状态或还原磁盘快照。</summary>
internal sealed class LicenseTimeStore(string path, byte[] entropy)
{
    public LicenseTimeResult Check(LicenseInfo license, DateTimeOffset now, bool allowInitialize, bool claimReminder)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("离线时间记录仅支持 Windows。");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // 各进程读/判断/写在同一锁内，避免旧状态覆盖新状态；锁冲突向上报告，可稍后重试。
        using var lease = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        DateTimeOffset highest;
        DateOnly? lastReminder;
        try
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (file.Length is <= 0 or > 4096)
                throw new InvalidDataException("授权时间记录损坏。");
            var cipher = new byte[(int)file.Length];
            file.ReadExactly(cipher);
            var plain = ProtectedData.Unprotect(cipher, entropy, DataProtectionScope.CurrentUser);
            if (plain.Length != 16 || BinaryPrimitives.ReadInt32LittleEndian(plain) != 1)
                throw new InvalidDataException("授权时间记录格式错误。");
            try
            {
                highest = new DateTimeOffset(BinaryPrimitives.ReadInt64LittleEndian(plain.AsSpan(4)), TimeSpan.Zero);
                int day = BinaryPrimitives.ReadInt32LittleEndian(plain.AsSpan(12));
                lastReminder = day == -1 ? null : DateOnly.FromDayNumber(day);
            }
            catch (ArgumentOutOfRangeException ex) { throw new InvalidDataException("授权时间记录内容错误。", ex); }
        }
        catch (FileNotFoundException) when (allowInitialize)
        {
            if (license.IssuedAtUtc.Kind != DateTimeKind.Utc)
                throw new InvalidDataException("授权签发时间必须为 UTC。");
            highest = new DateTimeOffset(license.IssuedAtUtc);
            lastReminder = null;
        }

        var result = LicenseTimePolicy.Evaluate(license, now, highest, lastReminder);
        if (result.Status == LicenseTimeStatus.ClockRollback) return result;
        // 在通知 UI 前占用当天提醒，保证跨进程至多一次；崩溃可能导致当天提醒未显示。
        if (claimReminder && result.ShouldRemind) lastReminder = DateOnly.FromDateTime(now.DateTime);
        WriteRecord(result.HighestObservedUtc, lastReminder);
        return result;
    }

    public string CreateRecoveryRequest(string machineCode, string productId)
    {
        RequireWindows();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var lease = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        string request = LicenseRecoveryRequest.Create(Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), machineCode, productId);
        WriteProtected(path + ".recovery", Encoding.UTF8.GetBytes(request));
        return request;
    }

    // 调用方必须先完成签名、机器、有效期和请求格式验证。
    public void Recover(LicenseInfo license, DateTimeOffset now, string signedRequest)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("离线时间记录仅支持 Windows。");
        using var lease = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        using (var file = new FileStream(path + ".recovery", FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            if (file.Length is <= 0 or > 4096) throw new InvalidDataException("恢复请求记录损坏。");
            var encrypted = new byte[(int)file.Length];
            file.ReadExactly(encrypted);
            byte[] pending = ProtectedData.Unprotect(encrypted, entropy, DataProtectionScope.CurrentUser);
            if (!CryptographicOperations.FixedTimeEquals(pending, Encoding.UTF8.GetBytes(signedRequest)))
                throw new InvalidDataException("恢复请求不匹配，请使用最新请求重新申请。");
        }
        if (license.IssuedAtUtc.Kind != DateTimeKind.Utc || now.UtcDateTime < license.IssuedAtUtc)
            throw new InvalidDataException("当前时间早于恢复授权签发时间，请先校正系统时间。");
        // 先消费请求再改时间记录。写入失败时需重新申请，不能通过重放请求反复重置时间。
        File.Delete(path + ".recovery");
        WriteRecord(now.ToUniversalTime(), DateOnly.FromDateTime(now.DateTime));
    }

    private static void RequireWindows()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("离线时间记录仅支持 Windows。");
    }

    private void WriteRecord(DateTimeOffset highest, DateOnly? lastReminder)
    {
        var data = new byte[16];
        BinaryPrimitives.WriteInt32LittleEndian(data, 1);
        BinaryPrimitives.WriteInt64LittleEndian(data.AsSpan(4), highest.UtcTicks);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(12), lastReminder?.DayNumber ?? -1);
        WriteProtected(path, data);
    }

    private void WriteProtected(string destination, byte[] data)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("离线时间记录仅支持 Windows。");
        var encrypted = ProtectedData.Protect(data, entropy, DataProtectionScope.CurrentUser);
        string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                file.Write(encrypted);
                file.Flush(flushToDisk: true);
            }
            File.Move(temporary, destination, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
