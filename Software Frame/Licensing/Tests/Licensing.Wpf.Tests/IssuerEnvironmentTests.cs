using System.IO;
using System.Security.Cryptography;
using System.Text;
using Extension.Security.Cryptography;
using Extension.Security.Licensing;
using Licensing.Authorization;
using Xunit;

namespace Licensing.Wpf.Tests;

public sealed class IssuerEnvironmentTests : IDisposable
{
    private const string Password = "portable-vendor-password";
    private const string BackupPassword = "independent-backup-password";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "IssuerEnvironment-" + Guid.NewGuid().ToString("N"));
    private readonly List<IssuerEnvironment> _stores = [];
    private IssuerEnvironment Store(string name = "source")
    {
        var store = new IssuerEnvironment(Path.Combine(_root, name)); _stores.Add(store); return store;
    }

    [Fact]
    public void FirstLoadDoesNotGenerateCredentialsAndRestartRequiresPassword()
    {
        var store = Store();
        Assert.Null(store.Load());
        using var identity = store.Initialize(Password);
        var restarted = Store();
        Assert.Throws<InvalidOperationException>(() => restarted.Load());
        Assert.ThrowsAny<CryptographicException>(() => restarted.Load("wrong-password"));
        Assert.False(restarted.IsUnlocked);
        using var loaded = restarted.Load(Password);
        Assert.Equal(identity.PublicKey, loaded!.PublicKey);
        using var cached = restarted.Load();
        Assert.Equal(identity.PublicKey, cached!.PublicKey);
        Assert.Throws<InvalidOperationException>(() => store.Initialize(Password));
        byte[] der = identity.ExportForBackup();
        try
        {
            byte[] persisted = File.ReadAllBytes(store.CredentialPath);
            Assert.True(persisted.AsSpan(0, 5).SequenceEqual("SFIB1"u8));
            Assert.True(persisted.AsSpan().IndexOf(der) < 0);
            Assert.DoesNotContain(Password, Encoding.UTF8.GetString(persisted));
        }
        finally { CryptographicOperations.ZeroMemory(der); }
    }

    [Fact]
    public void CopyingTheFolderDirectlyPreservesSigningIdentityAndRecordsWithoutRestore()
    {
        var source = Store();
        using var identity = source.Initialize(Password);
        var info = Info();
        string license = identity.Sign(info);
        IssuerFiles.Record(info, license, source.ArchiveDirectory);
        var destination = Store("copied-folder");
        foreach (string file in Directory.GetFiles(source.DirectoryPath, "*", SearchOption.AllDirectories))
        {
            if (Path.GetFileName(file) == "issuer.lock") continue;
            string target = Path.Combine(destination.DirectoryPath, Path.GetRelativePath(source.DirectoryPath, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(file, target);
        }
        Assert.Throws<InvalidOperationException>(() => destination.Load());
        using var copied = destination.Load(Password);
        Assert.Equal(identity.PublicKey, copied!.PublicKey);
        string record = Assert.Single(Directory.GetFiles(destination.ArchiveDirectory, "*.lic"));
        Assert.True(LicenseManagerEx.Verify(File.ReadAllText(record), copied.PublicKey, checkMachine: false, expectedProductId: info.ProductId).Ok);
    }

    [Fact]
    public void BackupCanUseAnIndependentPasswordAndDoesNotChangeTheCurrentUnlockPassword()
    {
        var source = Store();
        using var original = source.Initialize(Password);
        string license = original.Sign(Info());
        string backup = Path.Combine(_root, "backup.sfib");
        source.Backup(backup, BackupPassword);
        using var restarted = Store().Load(Password);
        using var restored = Store("destination").Restore(backup, BackupPassword);
        Assert.Equal(original.Fingerprint, restored.Fingerprint);
        Assert.True(LicenseManagerEx.Verify(license, restored.PublicKey, checkMachine: false, expectedProductId: "Test.Product").Ok);
        using var loaded = Store("destination").Load(BackupPassword);
        Assert.Equal(original.PublicKey, loaded!.PublicKey);
        Assert.ThrowsAny<CryptographicException>(() => Store("destination").Load(Password));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WrongPasswordOrTamperingCannotReplaceExistingCredentials(bool tamper)
    {
        var store = Store();
        using var identity = store.Initialize(Password);
        string backup = Path.Combine(_root, "backup.sfib");
        store.Backup(backup, BackupPassword);
        if (tamper) { byte[] bytes = File.ReadAllBytes(backup); bytes[^1] ^= 1; File.WriteAllBytes(backup, bytes); }
        Assert.ThrowsAny<CryptographicException>(() => store.Restore(backup, tamper ? BackupPassword : "wrong-password"));
        using var unchanged = store.Load();
        Assert.Equal(identity.PublicKey, unchanged!.PublicKey);
    }

    [Fact]
    public void MissingOrCorruptCredentialsRequireRecoveryInsteadOfNewKeys()
    {
        var store = Store();
        using var identity = store.Initialize(Password);
        string backup = Path.Combine(_root, "backup.sfib");
        store.Backup(backup, BackupPassword);
        File.WriteAllText(store.CredentialPath, "corrupt");
        Assert.Throws<InvalidDataException>(() => store.Load(Password));
        Assert.Throws<InvalidOperationException>(() => store.Initialize(Password));
        using var repaired = store.Restore(backup, BackupPassword);
        Assert.Equal(identity.PublicKey, repaired.PublicKey);
        File.Delete(store.CredentialPath);
        Assert.Throws<InvalidDataException>(() => store.Load());
        Assert.Throws<InvalidOperationException>(() => store.Initialize(Password));
    }

    [Fact]
    public void DifferentIdentityBackupCannotOverwriteAnExistingEnvironment()
    {
        var firstStore = Store(); var secondStore = Store("second");
        using var first = firstStore.Initialize(Password);
        using var second = secondStore.Initialize(Password);
        string backup = Path.Combine(_root, "other.sfib");
        secondStore.Backup(backup, BackupPassword);
        Assert.Throws<InvalidDataException>(() => firstStore.Restore(backup, BackupPassword));
        using var unchanged = firstStore.Load();
        Assert.Equal(first.PublicKey, unchanged!.PublicKey);
    }

    [Fact]
    public void ReplacingTheLocalIdentityIsOptInAndRewritesTheFingerprintMarker()
    {
        var firstStore = Store(); var secondStore = Store("second");
        using var first = firstStore.Initialize(Password);
        using var second = secondStore.Initialize(Password);
        string backup = Path.Combine(_root, "other.sfib");
        secondStore.Backup(backup, BackupPassword);

        // 界面要先拿到两把指纹才能问人，核对本身不能写盘。
        Assert.Equal(first.Fingerprint, firstStore.LocalFingerprint);
        Assert.Equal(second.Fingerprint, secondStore.PeekBackupFingerprint(backup, BackupPassword));
        Assert.ThrowsAny<CryptographicException>(() => firstStore.PeekBackupFingerprint(backup, "wrong-password"));
        Assert.Throws<InvalidDataException>(() => firstStore.Restore(backup, BackupPassword));

        // 确认替换后必须同步改写身份标记，否则 Load() 会拿旧指纹拒绝新凭据，这台机器直接锁死。
        using var replaced = firstStore.Restore(backup, BackupPassword, replaceExistingIdentity: true);
        Assert.Equal(second.Fingerprint, replaced.Fingerprint);
        Assert.Equal(second.Fingerprint, firstStore.LocalFingerprint);
        using var reopened = Store("source").Load(BackupPassword);
        Assert.Equal(second.PublicKey, reopened!.PublicKey);
        string license = reopened.Sign(Info());
        Assert.True(LicenseManagerEx.Verify(license, reopened.PublicKey, checkMachine: false, expectedProductId: "Test.Product").Ok);
    }

    [Fact]
    public void HistoricalRecordsBlockAccidentalReinitialization()
    {
        var store = Store(); Directory.CreateDirectory(store.ArchiveDirectory);
        File.WriteAllText(Path.Combine(store.ArchiveDirectory, "existing.lic"), "existing record marker");
        Assert.Throws<InvalidOperationException>(() => store.Initialize(Password));
        Assert.Null(store.Load());
    }

    [Fact]
    public void OldDpapiIdentityCanBeMigratedOnceWithoutChangingTheSigningIdentity()
    {
        var (privateKey, publicKey) = RsaEx.GenerateKeyPair();
        string legacy = Path.Combine(_root, "legacy"); Directory.CreateDirectory(legacy);
        Directory.CreateDirectory(Path.Combine(legacy, "Issued"));
        File.WriteAllText(Path.Combine(legacy, "Issued", "original.lic"), "historical record");
        File.WriteAllText(Path.Combine(legacy, "last-product.txt"), "Test.Product");
        byte[] der = Convert.FromBase64String(privateKey);
        try
        {
            byte[] encrypted = ProtectedData.Protect(der, Encoding.UTF8.GetBytes("SoftwareFrame.Licensing.Issuer.v1"), DataProtectionScope.CurrentUser);
            File.WriteAllBytes(Path.Combine(legacy, "issuer.bin"), encrypted);
            var store = Store();
            using var migrated = store.MigrateLocal(Password, legacy);
            Assert.Equal(publicKey, migrated.PublicKey);
            Assert.Equal("historical record", File.ReadAllText(Path.Combine(store.ArchiveDirectory, "original.lic")));
            Assert.Equal("Test.Product", File.ReadAllText(Path.Combine(store.DirectoryPath, "last-product.txt")));
            Assert.Equal(encrypted, File.ReadAllBytes(Path.Combine(legacy, "issuer.bin")));
            using var reopened = Store().Load(Password);
            Assert.Equal(publicKey, reopened!.PublicKey);
        }
        finally { CryptographicOperations.ZeroMemory(der); }
    }

    [Fact]
    public void WeakPasswordsDoNotCreateCredentialsAndDefaultDataLivesBesideTheTool()
    {
        var store = Store();
        Assert.Throws<ArgumentException>(() => store.Initialize("short"));
        Assert.False(File.Exists(store.CredentialPath));
        using var defaultStore = new IssuerEnvironment();
        Assert.Equal(Path.Combine(AppContext.BaseDirectory, "IssuerData"), defaultStore.DirectoryPath);
        store.Dispose();
        Assert.Throws<ObjectDisposedException>(() => store.Load(Password));
    }

    private static LicenseInfo Info() => new() { ProductId = "Test.Product", Customer = "客户", MachineCode = "V2-1111-2222-3333-4444", ExpireAtUtc = DateTime.MaxValue };
    public void Dispose()
    {
        foreach (var store in _stores) store.Dispose();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
