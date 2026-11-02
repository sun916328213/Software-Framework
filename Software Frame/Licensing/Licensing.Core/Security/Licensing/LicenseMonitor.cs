using System.ComponentModel;
using System.Security;
using System.Security.Cryptography;

namespace Extension.Security.Licensing;

/// <summary>
/// 单一后台循环：立即检查并定期重新验签/检查时间，不操作 UI 或设备。
/// 宿主必须按 Current 控制任务入口、观察 Completion，并在退出时 await DisposeAsync。
/// 回调在后台线程串行执行，应及时返回、遵守取消，WPF 操作必须切到 Dispatcher。
/// </summary>
public sealed class LicenseMonitor : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _loop;
    private Task? _disposeTask;
    private LicenseCheckResult _current = Unavailable("授权检查尚未完成。");

    private LicenseMonitor(LicenseFileStore store,
        Func<LicenseCheckResult, CancellationToken, Task> onChecked, TimeSpan interval, string? requiredFeature)
    {
        _loop = Task.Run(async () =>
        {
            try
            {
                using var timer = new PeriodicTimer(interval);
                do
                {
                    _stop.Token.ThrowIfCancellationRequested();
                    LicenseCheckResult result;
                    try { result = store.Check(claimReminder: true, requiredFeature: requiredFeature); }
                    catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or SecurityException
                                                  or CryptographicException or InvalidOperationException
                                                  or Win32Exception or PlatformNotSupportedException)
                    {
                        result = Unavailable($"无法完成授权检查：{ex.Message}");
                    }
                    lock (_gate) _current = result;
                    await onChecked(result, _stop.Token).ConfigureAwait(false);
                } while (await timer.WaitForNextTickAsync(_stop.Token).ConfigureAwait(false));
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
            catch
            {
                // 未预期的内部/回调异常仍传播给 Completion；绝不留下“上次有效”的状态。
                lock (_gate) _current = Unavailable("授权监控发生异常，请联系供应商。");
                throw;
            }
            finally
            {
                lock (_gate)
                {
                    if (_stop.IsCancellationRequested) _current = Unavailable("授权监控已停止。");
                }
            }
        });
    }

    public static LicenseMonitor Start(LicenseFileStore store,
        Func<LicenseCheckResult, CancellationToken, Task> onChecked,
        TimeSpan? interval = null, string? requiredFeature = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(onChecked);
        var period = interval ?? TimeSpan.FromMinutes(1);
        if (period < TimeSpan.FromMilliseconds(1) || period.TotalMilliseconds > uint.MaxValue - 1)
            throw new ArgumentOutOfRangeException(nameof(interval));
        return new LicenseMonitor(store, onChecked, period, requiredFeature);
    }

    public LicenseCheckResult Current
    {
        get
        {
            lock (_gate) return _disposeTask == null ? _current : Unavailable("授权监控正在停止或已停止。");
        }
    }

    /// <summary>正常情况下持续运行到释放；内部或宿主回调异常时失败，宿主必须观察。</summary>
    public Task Completion => _loop;

    public ValueTask DisposeAsync()
    {
        lock (_gate) return new ValueTask(_disposeTask ??= StopAsync());
    }

    private async Task StopAsync()
    {
        _stop.Cancel();
        try { await _loop.ConfigureAwait(false); }
        finally { _stop.Dispose(); }
    }

    private static LicenseCheckResult Unavailable(string message) =>
        new(new LicenseVerifyResult { Status = LicenseStatus.TimeStateInvalid, Message = message }, false);
}
