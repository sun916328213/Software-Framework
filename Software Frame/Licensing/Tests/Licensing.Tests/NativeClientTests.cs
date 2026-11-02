using Extension.Security.Cryptography;
using Extension.Security.Licensing;
using Licensing.Native.Client;
using Xunit;

namespace Licensing.Tests;

public sealed class NativeClientTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "NativeClient-" + Guid.NewGuid().ToString("N"));
    private string? _installedDirectory;

    [Fact]
    public void ClientAdapterCallsManagedAuthorizationCoreDirectly()
    {
        // 不再经过 Native AOT：客户端程序集必须直接引用托管核心，发布客户包才不需要 C++ 链接器。
        Assert.Contains(typeof(NativeLicenseSession).Assembly.GetReferencedAssemblies(),
            assembly => assembly.Name == "Licensing.Core");
    }

    [Fact]
    public void AdapterReturnsNativeMetadataAndSupportsSignedRecovery()
    {
        var (privateKey, publicKey) = RsaEx.GenerateKeyPair();
        TrackInstall(publicKey);
        using var session = new NativeLicenseSession(publicKey, LicenseProduct.DefaultId);
        Assert.Equal(MachineCodeEx.GetMachineCode(), NativeLicenseSession.GetMachineCode());
        Assert.False(session.Load().Ok);
        var license = new LicenseInfo
        {
            Customer = "原生封装测试客户", MachineCode = MachineCodeEx.GetMachineCode(),
            ExpireAtUtc = DateTime.UtcNow.AddDays(10), Features = { "Vision" }
        };
        var imported = session.ImportFile(Write(LicenseManagerEx.Create(license, privateKey)));
        Assert.True(imported.Ok, imported.Message);
        Assert.Equal(license.Id, imported.License!.Id);
        Assert.Equal(license.Customer, imported.License.Customer);
        Assert.Equal(license.ExpireAtUtc, imported.License.ExpireAtUtc);
        Assert.False(imported.License.IsPermanent);
        Assert.Equal(NativeLicenseStatus.FeatureDenied, session.Check(requiredFeature: "Motion").Verification.Status);
        Assert.True(session.Check(requiredFeature: "vision").Verification.Ok);
        Assert.True(session.Check(claimReminder: true).ShouldRemind);
        Assert.False(session.Check(claimReminder: true).ShouldRemind);

        File.WriteAllText(session.LicensePath + ".time", "corrupted");
        Assert.Equal(NativeLicenseStatus.TimeStateInvalid, session.Load().Status);
        string request = session.CreateTimeRecoveryRequest();
        Assert.Equal(license.MachineCode, LicenseRecoveryRequest.GetMachineCode(request));
        var recovery = new LicenseInfo { Customer = license.Customer, ExpireAtUtc = DateTime.UtcNow.AddDays(20) };
        LicenseRecoveryRequest.AttachTo(recovery, request);
        Assert.True(session.ImportFile(Write(LicenseManagerEx.Create(recovery, privateKey))).Ok);
        Assert.True(session.Load().Ok);
    }

    [Fact]
    public void PermanentMetadataAndDisposedSessionAreHandled()
    {
        var (privateKey, publicKey) = RsaEx.GenerateKeyPair();
        TrackInstall(publicKey);
        var session = new NativeLicenseSession(publicKey, LicenseProduct.DefaultId);
        try
        {
            var result = session.ImportFile(Write(LicenseManagerEx.Create(new LicenseInfo(), privateKey)));
            Assert.True(result.Ok, result.Message);
            Assert.True(result.License!.IsPermanent);
            Assert.False(session.Check(claimReminder: true).ShouldRemind);
        }
        finally { session.Dispose(); }
        Assert.Throws<ObjectDisposedException>(() => session.Check());
        session.Dispose();
    }

    [Fact]
    public async Task NativeMonitorDetectsTamperingAndReleasesSession()
    {
        var (privateKey, publicKey) = RsaEx.GenerateKeyPair();
        TrackInstall(publicKey);
        using (var session = new NativeLicenseSession(publicKey, LicenseProduct.DefaultId))
            Assert.True(session.ImportFile(Write(LicenseManagerEx.Create(new LicenseInfo(), privateKey))).Ok);
        var detected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        var monitor = NativeLicenseMonitor.Start(publicKey, LicenseProduct.DefaultId, (result, _) =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                Assert.True(result.Verification.Ok);
                File.WriteAllText(result.LicensePath, "tampered");
            }
            else if (!result.Verification.Ok) detected.TrySetResult();
            return Task.CompletedTask;
        }, TimeSpan.FromMilliseconds(25));
        try
        {
            await detected.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(monitor.Current.Verification.Ok);
        }
        finally { await monitor.DisposeAsync(); }
        Assert.True(monitor.Completion.IsCompletedSuccessfully);
        Assert.False(monitor.Current.Verification.Ok);
        await monitor.DisposeAsync();
    }

    [Fact]
    public async Task NativeMonitorCallbackFailureIsObservableAndFailsClosed()
    {
        var (_, publicKey) = RsaEx.GenerateKeyPair();
        var monitor = NativeLicenseMonitor.Start(publicKey, LicenseProduct.DefaultId, (_, _) => throw new InvalidOperationException("host failure"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => monitor.Completion.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.False(monitor.Current.Verification.Ok);
        await Assert.ThrowsAsync<InvalidOperationException>(() => monitor.CheckNowAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => monitor.DisposeAsync().AsTask());
    }

    [Fact]
    public async Task ImmediateCheckDoesNotWaitForTimerOrBlockedNotificationCallback()
    {
        var (privateKey, publicKey) = RsaEx.GenerateKeyPair();
        TrackInstall(publicKey);
        string licensePath;
        using (var session = new NativeLicenseSession(publicKey, LicenseProduct.DefaultId))
        {
            Assert.True(session.ImportFile(Write(LicenseManagerEx.Create(new LicenseInfo(), privateKey))).Ok);
            licensePath = session.LicensePath;
        }
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int callbacks = 0;
        var monitor = NativeLicenseMonitor.Start(publicKey, LicenseProduct.DefaultId, async (result, token) =>
        {
            Assert.True(result.Verification.Ok);
            Interlocked.Increment(ref callbacks);
            entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
        }, TimeSpan.FromHours(1));
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(monitor.Current.Verification.Ok);
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => monitor.CheckNowAsync(cancelled.Token));
            File.WriteAllText(licensePath, "tampered");
            var current = await monitor.CheckNowAsync().WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(current.Verification.Ok);
            Assert.False(current.ShouldRemind);
            Assert.False(monitor.Current.Verification.Ok);
            Assert.Equal(1, callbacks);
        }
        finally { await monitor.DisposeAsync(); }
        await Assert.ThrowsAsync<ObjectDisposedException>(() => monitor.CheckNowAsync());
    }

    [Fact]
    public void NativeProductSessionRejectsOtherProductUnderSamePublicKey()
    {
        var (privateKey, publicKey) = RsaEx.GenerateKeyPair();
        TrackInstall(publicKey, "product.A");
        using var a = new NativeLicenseSession(publicKey, "product.A");
        using var b = new NativeLicenseSession(publicKey, "product.B");
        var license = new LicenseInfo { ProductId = "product.A", MachineCode = MachineCodeEx.GetMachineCode() };
        string source = Write(LicenseManagerEx.CreateBound(license, privateKey));
        Assert.True(a.ImportFile(source).Ok);
        Assert.Equal("product.A", a.Load().License!.ProductId);
        Assert.Equal(NativeLicenseStatus.ProductMismatch, b.ImportFile(source).Status);
        Assert.False(b.Load().Ok);
        Assert.NotEqual(a.LicensePath, b.LicensePath);
        Assert.False(File.Exists(b.LicensePath));
    }

    private void TrackInstall(string publicKey, string productId = LicenseProduct.DefaultId)
    {
        string productDirectory = Path.GetDirectoryName(new LicenseFileStore(publicKey, productId: productId).LicensePath)!;
        string directory = Directory.GetParent(productDirectory)!.FullName;
        Assert.False(Directory.Exists(directory));
        _installedDirectory = directory;
    }

    private string Write(string text)
    {
        Directory.CreateDirectory(_root);
        string path = Path.Combine(_root, "source.lic");
        File.WriteAllText(path, text);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
        if (_installedDirectory != null && Directory.Exists(_installedDirectory)) Directory.Delete(_installedDirectory, true);
    }
}
