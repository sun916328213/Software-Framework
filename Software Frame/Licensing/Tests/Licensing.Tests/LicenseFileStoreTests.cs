using Extension.Security.Cryptography;
using Extension.Security.Licensing;
using Xunit;

namespace Licensing.Tests;

public sealed class LicenseFileStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "LicensingTests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void ImportPersistsSignedLicenseAndNewInstanceCanLoadIt()
    {
        var (privateKey, publicKey) = RsaEx.GenerateKeyPair();
        var store = new LicenseFileStore(publicKey, _root);
        Assert.False(store.Load().Ok);
        var info = new LicenseInfo { Customer = "客户", ExpireAtUtc = DateTime.UtcNow.AddDays(10) };
        string source = WriteSource(LicenseManagerEx.Create(info, privateKey));
        Assert.True(store.ImportFile(source).Ok);
        var loaded = new LicenseFileStore(publicKey, _root).Load();
        Assert.True(loaded.Ok);
        Assert.Equal(info.Id, loaded.License!.Id);
    }

    [Fact]
    public void InvalidOrExpiredImportDoesNotReplaceCurrentLicense()
    {
        var (privateKey, publicKey) = RsaEx.GenerateKeyPair();
        var store = new LicenseFileStore(publicKey, _root);
        string original = LicenseManagerEx.Create(new LicenseInfo(), privateKey);
        Assert.True(store.ImportFile(WriteSource(original)).Ok);
        Assert.False(store.ImportFile(WriteSource("invalid")).Ok);
        string expired = LicenseManagerEx.Create(new LicenseInfo { ExpireAtUtc = DateTime.UtcNow.AddDays(-1) }, privateKey);
        Assert.Equal(LicenseStatus.Expired, store.ImportFile(WriteSource(expired)).Status);
        Assert.Equal(original, File.ReadAllText(store.LicensePath));
    }

    [Fact]
    public void LoadVerifiesSavedFileAgainAfterTampering()
    {
        var (privateKey, publicKey) = RsaEx.GenerateKeyPair();
        var store = new LicenseFileStore(publicKey, _root);
        Assert.True(store.ImportFile(WriteSource(LicenseManagerEx.Create(new LicenseInfo(), privateKey))).Ok);
        File.WriteAllText(store.LicensePath, "tampered");
        Assert.False(store.Load().Ok);
    }

    [Fact]
    public void DifferentKeysAreIsolatedAndWrongSignerCannotBeImported()
    {
        var (privateKey, publicKey) = RsaEx.GenerateKeyPair();
        var (_, otherPublicKey) = RsaEx.GenerateKeyPair();
        var store = new LicenseFileStore(publicKey, _root);
        var other = new LicenseFileStore(otherPublicKey, _root);
        Assert.NotEqual(store.LicensePath, other.LicensePath);
        Assert.Equal(LicenseStatus.InvalidSignature,
            other.ImportFile(WriteSource(LicenseManagerEx.Create(new LicenseInfo(), privateKey))).Status);
        Assert.False(File.Exists(other.LicensePath));
    }

    [Fact]
    public void OversizedFileIsRejectedForImportAndLoad()
    {
        var (_, publicKey) = RsaEx.GenerateKeyPair();
        var store = new LicenseFileStore(publicKey, _root);
        string source = WriteSource(new string('x', 1024 * 1024 + 1));
        Assert.Throws<InvalidDataException>(() => store.ImportFile(source));
        Directory.CreateDirectory(Path.GetDirectoryName(store.LicensePath)!);
        File.Copy(source, store.LicensePath);
        Assert.Throws<InvalidDataException>(() => store.Load());
    }

    [Fact]
    public void ReminderIsClaimedOncePerDayAcrossInstances()
    {
        var (privateKey, publicKey) = RsaEx.GenerateKeyPair();
        var clock = new TestClock { Now = DateTimeOffset.UtcNow.AddMinutes(1) };
        var store = new LicenseFileStore(publicKey, _root, clock);
        var license = new LicenseInfo { ExpireAtUtc = DateTime.UtcNow.AddDays(10) };
        Assert.True(store.ImportFile(WriteSource(LicenseManagerEx.Create(license, privateKey))).Ok);
        Assert.True(store.Check(claimReminder: true).ShouldRemind);
        Assert.False(new LicenseFileStore(publicKey, _root, clock).Check(claimReminder: true).ShouldRemind);
        clock.Now = clock.Now.AddDays(1);
        Assert.True(store.Check(claimReminder: true).ShouldRemind);
    }

    [Fact]
    public void ClockRollbackIsRejectedAndCorrectingTimeRestoresAuthorization()
    {
        var (privateKey, publicKey) = RsaEx.GenerateKeyPair();
        var clock = new TestClock { Now = DateTimeOffset.UtcNow.AddMinutes(1) };
        var store = new LicenseFileStore(publicKey, _root, clock);
        Assert.True(store.ImportFile(WriteSource(LicenseManagerEx.Create(
            new LicenseInfo { ExpireAtUtc = DateTime.UtcNow.AddDays(10) }, privateKey))).Ok);
        var highest = clock.Now;
        clock.Now = highest.AddHours(-1);
        Assert.Equal(LicenseStatus.ClockRollback, store.Load().Status);
        clock.Now = highest;
        Assert.True(store.Load().Ok);
    }

    [Fact]
    public void MissingOrCorruptedTimeRecordCannotBeSilentlyResetByImport()
    {
        var (privateKey, publicKey) = RsaEx.GenerateKeyPair();
        var store = new LicenseFileStore(publicKey, _root);
        string source = WriteSource(LicenseManagerEx.Create(
            new LicenseInfo { ExpireAtUtc = DateTime.UtcNow.AddDays(10) }, privateKey));
        Assert.True(store.ImportFile(source).Ok);
        File.WriteAllText(store.LicensePath + ".time", "corrupted");
        Assert.Equal(LicenseStatus.TimeStateInvalid, store.Load().Status);
        Assert.Equal(LicenseStatus.TimeStateInvalid, store.ImportFile(source).Status);
        File.Delete(store.LicensePath + ".time");
        Assert.Equal(LicenseStatus.TimeStateInvalid, store.Load().Status);
        Assert.Equal(LicenseStatus.TimeStateInvalid, store.ImportFile(source).Status);
    }

    [Fact]
    public void FrozenClockIsRejectedAfterMonotonicTimePassesAndCorrectionRecovers()
    {
        var (privateKey, publicKey) = RsaEx.GenerateKeyPair();
        var clock = new TestClock { Now = DateTimeOffset.UtcNow.AddMinutes(1) };
        var store = new LicenseFileStore(publicKey, _root, clock);
        Assert.True(store.ImportFile(WriteSource(LicenseManagerEx.Create(
            new LicenseInfo { ExpireAtUtc = DateTime.UtcNow.AddDays(10) }, privateKey))).Ok);
        clock.Timestamp = TimeSpan.FromSeconds(10).Ticks;
        Assert.Equal(LicenseStatus.ClockRollback, store.Load().Status);
        clock.Now = clock.Now.AddSeconds(10);
        Assert.True(store.Load().Ok);
    }

    [Fact]
    public void SmallClockDeficitsCannotKeepResettingMonotonicBaseline()
    {
        var (privateKey, publicKey) = RsaEx.GenerateKeyPair();
        var clock = new TestClock { Now = DateTimeOffset.UtcNow.AddMinutes(1) };
        var store = new LicenseFileStore(publicKey, _root, clock);
        Assert.True(store.ImportFile(WriteSource(LicenseManagerEx.Create(
            new LicenseInfo { ExpireAtUtc = DateTime.UtcNow.AddDays(10) }, privateKey))).Ok);
        clock.Timestamp = TimeSpan.FromSeconds(1).Ticks;
        Assert.True(store.Load().Ok);
        clock.Timestamp = TimeSpan.FromSeconds(2).Ticks;
        Assert.True(store.Load().Ok);
        clock.Timestamp = TimeSpan.FromSeconds(3).Ticks;
        Assert.Equal(LicenseStatus.ClockRollback, store.Load().Status);
    }

    [Fact]
    public void PermanentLicenseIgnoresFrozenClock()
    {
        var (privateKey, publicKey) = RsaEx.GenerateKeyPair();
        var clock = new TestClock { Now = DateTimeOffset.UtcNow };
        var store = new LicenseFileStore(publicKey, _root, clock);
        Assert.True(store.ImportFile(WriteSource(LicenseManagerEx.Create(new LicenseInfo(), privateKey))).Ok);
        clock.Timestamp = TimeSpan.FromDays(100).Ticks;
        clock.Now = clock.Now.AddDays(-1);
        Assert.True(store.Load().Ok);
    }

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; }
        public long Timestamp { get; set; }
        public override long GetTimestamp() => Timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override DateTimeOffset GetUtcNow() => Now.ToUniversalTime();
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private string WriteSource(string text)
    {
        Directory.CreateDirectory(_root);
        string path = Path.Combine(_root, "source.lic");
        File.WriteAllText(path, text);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
