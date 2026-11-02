using System.IO;
using System.Security.Cryptography;
using System.Text;
using Extension.Security.Licensing;

namespace Licensing.Authorization;

/// <summary>密码加密的便携签发身份；会话仅缓存派生密钥，不保存密码，不绑定电脑或 Windows 账户。</summary>
internal sealed class IssuerEnvironment : IDisposable
{
    private static readonly byte[] BackupMagic = "SFIB1"u8.ToArray();
    private const int Iterations = 600_000;
    private byte[]? _sessionKey, _sessionSalt;
    private bool _disposed;
    private readonly bool _defaultLocation;
    internal string DirectoryPath { get; }
    internal string CredentialPath => Path.Combine(DirectoryPath, "issuer.bin");
    private string IdentityPath => Path.Combine(DirectoryPath, "issuer.identity");
    internal string ArchiveDirectory => Path.Combine(DirectoryPath, "Issued");
    internal bool IsUnlocked => _sessionKey != null;
    /// <summary>凭据文件存在即视为已初始化；不尝试解密，避免为了刷新界面而做一次 PBKDF2。</summary>
    internal bool IsInitialized => File.Exists(CredentialPath);
    private static string LegacyDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SoftwareFrame", "LicensingIssuer");
    internal bool HasLegacyLocalIdentity => _defaultLocation && File.Exists(Path.Combine(LegacyDirectory, "issuer.bin"));

    internal IssuerEnvironment(string? directory = null)
    {
        _defaultLocation = directory == null;
        DirectoryPath = Path.GetFullPath(directory ?? Path.Combine(AppContext.BaseDirectory, "IssuerData"));
    }

    internal IssuerIdentity? Load(string? password = null)
    {
        using var gate = Lock();
        if (!File.Exists(CredentialPath))
        {
            if (File.Exists(IdentityPath)) throw new InvalidDataException("签发身份文件已丢失，请恢复备份；不能重新初始化替换。");
            return null;
        }
        byte[] envelope = ReadBounded(CredentialPath);
        ValidateEnvelope(envelope);
        byte[] salt = envelope.AsSpan(5, 16).ToArray();
        byte[] key = password != null
            ? Derive(password, salt)
            : _sessionKey != null && _sessionSalt!.AsSpan().SequenceEqual(salt)
                ? _sessionKey.ToArray()
                : throw new InvalidOperationException("签发身份已锁定，请输入密码解锁。");
        byte[] plain = new byte[envelope.Length - 49];
        IssuerIdentity? identity = null;
        try
        {
            Decrypt(envelope, key, plain);
            identity = IssuerIdentity.FromDer(plain);
            CheckIdentity(identity);
            RememberKey(key, salt);
            return identity;
        }
        catch { identity?.Dispose(); throw; }
        finally { CryptographicOperations.ZeroMemory(key); CryptographicOperations.ZeroMemory(plain); }
    }

    internal IssuerIdentity Initialize(string password)
    {
        ValidatePassword(password);
        using var gate = Lock();
        EnsureUninitialized();
        if (HasLegacyLocalIdentity || Directory.Exists(ArchiveDirectory) && Directory.EnumerateFiles(ArchiveDirectory, "*.lic").Any())
            throw new InvalidOperationException("发现已有签发身份或授权记录，请恢复备份或迁移原电脑的本机身份，不能初始化另一套身份。");
        using var rsa = RSA.Create(3072);
        return StoreNew(rsa.ExportRSAPrivateKey(), password, overwrite: false);
    }

    // 仅在原电脑上进行一次迁移；日常便携使用不调用 DPAPI，旧文件不删除、不覆盖。
    internal IssuerIdentity MigrateLocal(string password, string? legacyDirectory = null)
    {
        ValidatePassword(password);
        using var gate = Lock();
        EnsureUninitialized();
        byte[] plain = ProtectedData.Unprotect(ReadBounded(Path.Combine(legacyDirectory ?? LegacyDirectory, "issuer.bin")),
            Encoding.UTF8.GetBytes("SoftwareFrame.Licensing.Issuer.v1"), DataProtectionScope.CurrentUser);
        try
        {
            CopyLegacyRecords(legacyDirectory ?? LegacyDirectory);
            return StoreNew(plain, password, overwrite: false);
        }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }

    private void CopyLegacyRecords(string legacyDirectory)
    {
        string records = Path.Combine(legacyDirectory, "Issued");
        if (Directory.Exists(records))
        {
            Directory.CreateDirectory(ArchiveDirectory);
            foreach (string source in Directory.EnumerateFiles(records, "*.lic"))
            {
                string target = Path.Combine(ArchiveDirectory, Path.GetFileName(source));
                if (File.Exists(target))
                {
                    using var original = File.OpenRead(source);
                    using var existing = File.OpenRead(target);
                    if (!SHA256.HashData(original).AsSpan().SequenceEqual(SHA256.HashData(existing)))
                        throw new IOException("已有同名但不同内容的授权记录，未覆盖；请先核实记录。");
                }
                else File.Copy(source, target, overwrite: false);
            }
        }
        string settings = Path.Combine(legacyDirectory, "last-product.txt");
        string destination = Path.Combine(DirectoryPath, "last-product.txt");
        if (File.Exists(settings) && !File.Exists(destination)) File.Copy(settings, destination, overwrite: false);
    }

    internal void Backup(string path, string password)
    {
        ValidatePassword(password);
        using var identity = Load() ?? throw new InvalidOperationException("请先创建或解锁签发身份。");
        byte[] plain = identity.ExportForBackup();
        try { WriteAtomic(path, Encrypt(plain, password), overwrite: false); }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }

    internal IssuerIdentity Restore(string path, string password)
    {
        // SFIB1 旧加密备份可直接恢复。原备份密码成为便携身份的解锁密码。
        ValidatePassword(password);
        byte[] envelope = ReadBounded(path);
        ValidateEnvelope(envelope);
        byte[] key = Derive(password, envelope.AsSpan(5, 16));
        byte[] plain = new byte[envelope.Length - 49];
        try
        {
            Decrypt(envelope, key, plain);
            using var gate = Lock();
            using var candidate = IssuerIdentity.FromDer(plain);
            CheckIdentity(candidate);
            if (File.Exists(CredentialPath) && !File.Exists(IdentityPath))
                throw new InvalidDataException("签发身份标记丢失，不能覆盖原凭据，请由管理员核实。");
            return StoreNew(plain.ToArray(), password, overwrite: true);
        }
        finally { CryptographicOperations.ZeroMemory(key); CryptographicOperations.ZeroMemory(plain); }
    }

    private IssuerIdentity StoreNew(byte[] plain, string password, bool overwrite)
    {
        IssuerIdentity? identity = null;
        try
        {
            identity = IssuerIdentity.FromDer(plain);
            CheckIdentity(identity);
            byte[] envelope = Encrypt(plain, password);
            if (!File.Exists(IdentityPath)) WriteAtomic(IdentityPath, Encoding.ASCII.GetBytes(identity.Fingerprint), overwrite: false);
            WriteAtomic(CredentialPath, envelope, overwrite);
            byte[] key = Derive(password, envelope.AsSpan(5, 16));
            try { RememberKey(key, envelope.AsSpan(5, 16)); }
            finally { CryptographicOperations.ZeroMemory(key); }
            return identity;
        }
        catch { identity?.Dispose(); throw; }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }

    private static byte[] Encrypt(byte[] plain, string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16), nonce = RandomNumberGenerator.GetBytes(12);
        byte[] key = Derive(password, salt);
        try
        {
            byte[] tag = new byte[16], cipher = new byte[plain.Length];
            using var aes = new AesGcm(key, 16);
            aes.Encrypt(nonce, plain, cipher, tag, BackupMagic);
            return [.. BackupMagic, .. salt, .. nonce, .. tag, .. cipher];
        }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    private static void Decrypt(byte[] envelope, byte[] key, byte[] plain)
    {
        using var aes = new AesGcm(key, 16);
        aes.Decrypt(envelope.AsSpan(21, 12), envelope.AsSpan(49), envelope.AsSpan(33, 16), plain, BackupMagic);
    }
    private static byte[] Derive(string password, ReadOnlySpan<byte> salt) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
    private static void ValidatePassword(string password)
    {
        if (password.Length < 12) throw new ArgumentException("密码至少 12 个字符，请单独保管；遗忘密码无法自动恢复。");
    }
    private static void ValidateEnvelope(byte[] envelope)
    {
        if (envelope.Length <= 49 || !envelope.AsSpan(0, 5).SequenceEqual(BackupMagic))
            throw new InvalidDataException("不是密码加密的便携身份文件；旧本机身份请用迁移入口，损坏文件请恢复备份。");
    }

    private void RememberKey(byte[] key, ReadOnlySpan<byte> salt)
    {
        if (_sessionKey != null) CryptographicOperations.ZeroMemory(_sessionKey);
        _sessionKey = key.ToArray(); _sessionSalt = salt.ToArray();
    }

    private void CheckIdentity(IssuerIdentity identity)
    {
        if (File.Exists(IdentityPath) && Encoding.ASCII.GetString(ReadBounded(IdentityPath)).Trim() != identity.Fingerprint)
            throw new InvalidDataException("备份或凭据不属于当前签发身份，不能覆盖现有身份。");
    }
    private void EnsureUninitialized()
    {
        if (File.Exists(CredentialPath) || File.Exists(IdentityPath))
            throw new InvalidOperationException("已有签发身份，不能重新创建；请解锁或恢复备份。");
    }
    private FileStream Lock()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Directory.CreateDirectory(DirectoryPath);
        return new FileStream(Path.Combine(DirectoryPath, "issuer.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }
    private static byte[] ReadBounded(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is <= 0 or > 65536) throw new InvalidDataException("签发身份文件大小不合法。");
        byte[] bytes = new byte[(int)stream.Length]; stream.ReadExactly(bytes); return bytes;
    }
    private static void WriteAtomic(string path, byte[] bytes, bool overwrite)
    {
        path = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { stream.Write(bytes); stream.Flush(flushToDisk: true); }
            File.Move(temporary, path, overwrite);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public void Dispose()
    {
        _disposed = true;
        if (_sessionKey != null) CryptographicOperations.ZeroMemory(_sessionKey);
        _sessionKey = null; _sessionSalt = null;
    }
}

internal sealed class IssuerIdentity : IDisposable
{
    private readonly RSA _rsa;
    internal string PublicKey { get; }
    internal string Fingerprint { get; }

    private IssuerIdentity(RSA rsa)
    {
        _rsa = rsa;
        byte[] publicDer = rsa.ExportRSAPublicKey();
        PublicKey = Convert.ToBase64String(publicDer);
        Fingerprint = Convert.ToHexString(SHA256.HashData(publicDer));
    }

    internal static IssuerIdentity FromDer(byte[] der)
    {
        var rsa = RSA.Create();
        try
        {
            rsa.ImportRSAPrivateKey(der, out int consumed);
            if (consumed != der.Length || rsa.KeySize < 2048) throw new InvalidDataException("签发凭据无效或强度不足。");
            return new IssuerIdentity(rsa);
        }
        catch { rsa.Dispose(); throw; }
    }

    internal string Sign(LicenseInfo info)
    {
        byte[] der = ExportForBackup();
        try { return LicenseManagerEx.CreateBound(info, Convert.ToBase64String(der)); }
        finally { CryptographicOperations.ZeroMemory(der); }
    }

    internal byte[] ExportForBackup() => _rsa.ExportRSAPrivateKey();
    public void Dispose() => _rsa.Dispose();
}
