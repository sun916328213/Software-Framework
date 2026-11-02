namespace Licensing.Native.Client;

/// <summary>拥有授权会话的后台监控。回调串行、可取消，不操作 UI 或设备。</summary>
public sealed class NativeLicenseMonitor : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly object _checkGate = new();
    private readonly string? _requiredFeature;
    private bool _failed;
    private readonly CancellationTokenSource _stop = new();
    private readonly NativeLicenseSession _session;
    private readonly Task _loop;
    private Task? _disposeTask;
    private NativeCheckResult _current = Unavailable("授权检查尚未完成。");

    private NativeLicenseMonitor(NativeLicenseSession session,
        Func<NativeCheckResult, CancellationToken, Task> onChecked, TimeSpan period, string? requiredFeature)
    {
        _session = session;
        _requiredFeature = requiredFeature;
        _loop = Task.Run(async () =>
        {
            try
            {
                using var timer = new PeriodicTimer(period);
                do
                {
                    _stop.Token.ThrowIfCancellationRequested();
                    var result = RunCheck(claimReminder: true);
                    _stop.Token.ThrowIfCancellationRequested();
                    await onChecked(result, _stop.Token).ConfigureAwait(false);
                } while (await timer.WaitForNextTickAsync(_stop.Token).ConfigureAwait(false));
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
            catch
            {
                lock (_gate)
                {
                    _failed = true;
                    _current = Unavailable("授权监控异常，请联系供应商。");
                }
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

    public static NativeLicenseMonitor Start(string trustedPublicKey, string productId,
        Func<NativeCheckResult, CancellationToken, Task> onChecked,
        TimeSpan? interval = null, string? requiredFeature = null)
    {
        ArgumentNullException.ThrowIfNull(onChecked);
        var period = interval ?? TimeSpan.FromMinutes(1);
        if (period < TimeSpan.FromMilliseconds(1) || period.TotalMilliseconds > uint.MaxValue - 1)
            throw new ArgumentOutOfRangeException(nameof(interval));
        var session = new NativeLicenseSession(trustedPublicKey, productId);
        try { return new NativeLicenseMonitor(session, onChecked, period, requiredFeature); }
        catch { session.Dispose(); throw; }
    }

    public NativeCheckResult Current
    {
        get { lock (_gate) return _disposeTask == null ? _current : Unavailable("授权监控正在停止或已停止。"); }
    }

    /// <summary>
    /// 任务入口即时复查，不等待定时器或通知回调、不占用每日提醒。
    /// 返回结果只代表本次检查，不能替代设备安全联锁；异常/取消均不得启动任务。
    /// </summary>
    public async Task<NativeCheckResult> CheckNowAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposeTask != null, this);
            if (_failed) throw new InvalidOperationException("授权监控已发生异常，不能继续放行业务。");
        }
        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = RunCheck(claimReminder: false);
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }, cancellationToken).ConfigureAwait(false);
    }

    private NativeCheckResult RunCheck(bool claimReminder)
    {
        lock (_checkGate)
        {
            lock (_gate)
            {
                if (_stop.IsCancellationRequested || _disposeTask != null) return Unavailable("授权监控已停止。");
                if (_failed) throw new InvalidOperationException("授权监控已发生异常。");
            }
            NativeCheckResult result;
            try
            {
                result = _session.Check(claimReminder, _requiredFeature);
                if (!claimReminder) result = result with { ShouldRemind = false };
            }
            catch
            {
                lock (_gate)
                {
                    _failed = true;
                    _current = Unavailable("即时授权检查失败，请联系供应商。");
                }
                throw;
            }
            lock (_gate)
            {
                if (_stop.IsCancellationRequested || _disposeTask != null) return Unavailable("授权监控已停止。");
                if (_failed) return _current;
                _current = result;
            }
            return result;
        }
    }

    /// <summary>宿主必须观察异常；回调内不要等待释放自身。</summary>
    public Task Completion => _loop;

    public ValueTask DisposeAsync()
    {
        lock (_gate) return new ValueTask(_disposeTask ??= StopAsync());
    }

    private async Task StopAsync()
    {
        _stop.Cancel();
        try { await _loop.ConfigureAwait(false); }
        finally
        {
            _session.Dispose();
            _stop.Dispose();
        }
    }

    private static NativeCheckResult Unavailable(string message) => new()
    {
        Verification = NativeVerifyResult.Failure(-2) with { Message = message }
    };
}
