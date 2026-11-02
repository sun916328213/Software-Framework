using Extension.Security.Cryptography;
using Extension.Security.Licensing;
using Xunit;

namespace Licensing.Tests;

public sealed class LicenseMonitorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "LicenseMonitorTests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task MonitorChecksImmediatelyThenDetectsTamperingWithoutRestart()
    {
        var store = CreateStore();
        var changed = new TaskCompletionSource<LicenseCheckResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        int count = 0;
        await using var monitor = LicenseMonitor.Start(store, (result, _) =>
        {
            if (Interlocked.Increment(ref count) == 1)
            {
                Assert.True(result.Verification.Ok);
                File.WriteAllText(store.LicensePath, "tampered");
            }
            else if (!result.Verification.Ok) changed.TrySetResult(result);
            return Task.CompletedTask;
        }, TimeSpan.FromMilliseconds(25));
        var detected = await changed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(detected.Verification.Ok);
        Assert.False(monitor.Current.Verification.Ok);
    }

    [Fact]
    public async Task DisposeCancelsActiveCallbackAndClosesMonitorFailClosed()
    {
        var store = CreateStore();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        var monitor = LicenseMonitor.Start(store, async (_, token) =>
        {
            Interlocked.Increment(ref calls);
            entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
        }, TimeSpan.FromMilliseconds(25));
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally { await monitor.DisposeAsync(); }
        Assert.True(monitor.Completion.IsCompletedSuccessfully);
        Assert.False(monitor.Current.Verification.Ok);
        Assert.Equal(1, calls);
        await monitor.DisposeAsync();
    }

    [Fact]
    public async Task CallbackFailureIsObservableAndNeverLeavesValidState()
    {
        var store = CreateStore();
        var monitor = LicenseMonitor.Start(store, (_, _) => throw new InvalidOperationException("host failure"));
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => monitor.Completion.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Equal("host failure", exception.Message);
        Assert.False(monitor.Current.Verification.Ok);
        await Assert.ThrowsAsync<InvalidOperationException>(() => monitor.DisposeAsync().AsTask());
    }

    [Fact]
    public async Task MissingLicenseIsReportedAsInvalid()
    {
        var (_, key) = RsaEx.GenerateKeyPair();
        var store = new LicenseFileStore(key, _root);
        var checkedOnce = new TaskCompletionSource<LicenseCheckResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var monitor = LicenseMonitor.Start(store, (result, _) =>
        {
            checkedOnce.TrySetResult(result);
            return Task.CompletedTask;
        });
        Assert.False((await checkedOnce.Task.WaitAsync(TimeSpan.FromSeconds(10))).Verification.Ok);
    }

    [Fact]
    public async Task RunningMonitorDetectsExpiryWithoutRestart()
    {
        var (privateKey, publicKey) = RsaEx.GenerateKeyPair();
        var clock = new MonitorClock { Now = DateTimeOffset.UtcNow.AddMinutes(1) };
        Directory.CreateDirectory(_root);
        string source = Path.Combine(_root, "timed.lic");
        File.WriteAllText(source, LicenseManagerEx.Create(new LicenseInfo
        {
            ExpireAtUtc = clock.Now.UtcDateTime.AddDays(1)
        }, privateKey));
        var store = new LicenseFileStore(publicKey, _root, clock);
        Assert.True(store.ImportFile(source).Ok);
        var expired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        await using var monitor = LicenseMonitor.Start(store, (result, _) =>
        {
            if (++calls == 1)
            {
                Assert.True(result.Verification.Ok);
                clock.Now = clock.Now.AddDays(2);
            }
            else if (result.Verification.Status == LicenseStatus.Expired) expired.TrySetResult();
            return Task.CompletedTask;
        }, TimeSpan.FromMilliseconds(25));
        await expired.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(LicenseStatus.Expired, monitor.Current.Verification.Status);
    }

    private sealed class MonitorClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; }
        public override DateTimeOffset GetUtcNow() => Now.ToUniversalTime();
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private LicenseFileStore CreateStore()
    {
        var (privateKey, publicKey) = RsaEx.GenerateKeyPair();
        Directory.CreateDirectory(_root);
        string source = Path.Combine(_root, "source.lic");
        File.WriteAllText(source, LicenseManagerEx.Create(new LicenseInfo(), privateKey));
        var store = new LicenseFileStore(publicKey, _root);
        Assert.True(store.ImportFile(source).Ok);
        return store;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
