using Extension.Security.Cryptography;
using Extension.Security.Licensing;
using Xunit;

namespace Licensing.Tests;

public sealed class LicenseRecoveryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "LicenseRecovery-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void SignedRecoveryRestoresCorruptedRecordAndCannotBeReplayed()
    {
        var (privateKey, publicKey) = RsaEx.GenerateKeyPair();
        var store = new LicenseFileStore(publicKey, _root);
        Assert.True(store.ImportFile(Write(LicenseManagerEx.Create(TimedLicense(), privateKey))).Ok);
        File.WriteAllText(store.LicensePath + ".time", "corrupted");
        string request = store.CreateTimeRecoveryRequest();
        Assert.Equal(LicenseStatus.TimeStateInvalid, store.Load().Status);
        var recovery = TimedLicense();
        LicenseRecoveryRequest.AttachTo(recovery, request);
        string text = LicenseManagerEx.Create(recovery, privateKey);
        Assert.True(store.ImportFile(Write(text)).Ok);
        Assert.True(store.Load().Ok);
        Assert.False(File.Exists(store.LicensePath + ".time.recovery"));
        Assert.Equal(LicenseStatus.TimeStateInvalid, store.ImportFile(Write(text)).Status);
        Assert.Equal(text, File.ReadAllText(store.LicensePath));
    }

    [Fact]
    public void OnlyLatestRequestAndTrustedSignatureCanAuthorizeRecovery()
    {
        var (privateKey, publicKey) = RsaEx.GenerateKeyPair();
        var (wrongPrivateKey, _) = RsaEx.GenerateKeyPair();
        var store = new LicenseFileStore(publicKey, _root);
        string oldRequest = store.CreateTimeRecoveryRequest();
        string latest = store.CreateTimeRecoveryRequest();
        Assert.NotEqual(oldRequest, latest);
        var oldRecovery = TimedLicense();
        LicenseRecoveryRequest.AttachTo(oldRecovery, oldRequest);
        Assert.Equal(LicenseStatus.TimeStateInvalid,
            store.ImportFile(Write(LicenseManagerEx.Create(oldRecovery, privateKey))).Status);
        var recovery = TimedLicense();
        LicenseRecoveryRequest.AttachTo(recovery, latest);
        Assert.Equal(LicenseStatus.InvalidSignature,
            store.ImportFile(Write(LicenseManagerEx.Create(recovery, wrongPrivateKey))).Status);
        Assert.True(store.ImportFile(Write(LicenseManagerEx.Create(recovery, privateKey))).Ok);
    }

    [Fact]
    public void AuthorizedRecoveryResetsSpoiledFutureHistoryAndInMemoryBaseline()
    {
        var (privateKey, publicKey) = RsaEx.GenerateKeyPair();
        var clock = new RecoveryClock { Now = DateTimeOffset.UtcNow.AddDays(1) };
        var store = new LicenseFileStore(publicKey, _root, clock);
        Assert.True(store.ImportFile(Write(LicenseManagerEx.Create(TimedLicense(), privateKey))).Ok);
        clock.Now = DateTimeOffset.UtcNow.AddMinutes(1);
        Assert.Equal(LicenseStatus.ClockRollback, store.Load().Status);
        string request = store.CreateTimeRecoveryRequest();
        var recovery = TimedLicense();
        Assert.Equal(LicenseStatus.ClockRollback,
            store.ImportFile(Write(LicenseManagerEx.Create(recovery, privateKey))).Status);
        LicenseRecoveryRequest.AttachTo(recovery, request);
        Assert.True(store.ImportFile(Write(LicenseManagerEx.Create(recovery, privateKey))).Ok);
        Assert.True(store.Load().Ok);
    }

    [Fact]
    public void FactoryAttachmentRejectsPermanentOrWrongMachineAndInvalidRequest()
    {
        var (_, key) = RsaEx.GenerateKeyPair();
        var store = new LicenseFileStore(key, _root);
        string request = store.CreateTimeRecoveryRequest();
        Assert.Throws<ArgumentException>(() => LicenseRecoveryRequest.AttachTo(new LicenseInfo(), request));
        var wrongMachine = TimedLicense();
        wrongMachine.MachineCode = "V2-0000-0000-0000-0000";
        Assert.Throws<ArgumentException>(() => LicenseRecoveryRequest.AttachTo(wrongMachine, request));
        Assert.Throws<ArgumentException>(() => LicenseRecoveryRequest.GetMachineCode("not a request"));
    }

    private static LicenseInfo TimedLicense() => new() { ExpireAtUtc = DateTime.UtcNow.AddDays(30) };

    private string Write(string text)
    {
        Directory.CreateDirectory(_root);
        string path = Path.Combine(_root, "source.lic");
        File.WriteAllText(path, text);
        return path;
    }

    private sealed class RecoveryClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; }
        public override DateTimeOffset GetUtcNow() => Now;
        public override long GetTimestamp() => 0;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
