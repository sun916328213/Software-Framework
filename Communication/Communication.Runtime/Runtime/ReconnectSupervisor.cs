namespace CommunicationNew.Runtime;

/// <summary>
/// 持有单个自动重连循环，并保证停止操作会等待循环退出。
/// 状态机和实际连接动作仍由调用模块负责。
/// </summary>
internal sealed class ReconnectSupervisor : IAsyncDisposable
{
    private readonly ConnectionReconnectOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly Func<CancellationToken, ValueTask<bool>> _tryReconnect;
    private readonly Func<bool> _shouldReconnect;
    private readonly CommunicationDiagnosticContext _diagnosticContext;
    private readonly object _sync = new();
    private CancellationTokenSource? _cancellation;
    private Task? _task;
    private bool _disposed;

    /// <summary>创建一个按固定间隔执行的重连 supervisor。</summary>
    public ReconnectSupervisor(
        ConnectionReconnectOptions options,
        TimeProvider timeProvider,
        Func<CancellationToken, ValueTask<bool>> tryReconnect,
        Func<bool> shouldReconnect,
        CommunicationDiagnosticContext diagnosticContext)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(tryReconnect);
        ArgumentNullException.ThrowIfNull(shouldReconnect);
        _options = options;
        _timeProvider = timeProvider;
        _tryReconnect = tryReconnect;
        _shouldReconnect = shouldReconnect;
        _diagnosticContext = diagnosticContext;
    }

    /// <summary>在尚无活动循环且调用方仍期望在线时启动恢复。</summary>
    public void Start()
    {
        lock (_sync)
        {
            if (_disposed || !_shouldReconnect() || _task is { IsCompleted: false })
            {
                return;
            }

            _cancellation?.Dispose();
            _cancellation = new CancellationTokenSource();
            _task = RunAsync(_cancellation.Token);
        }
    }

    /// <summary>取消当前恢复循环，并等待它不再访问连接资源。</summary>
    public async ValueTask StopAsync()
    {
        Task? task;
        lock (_sync)
        {
            _cancellation?.Cancel();
            task = _task;
        }

        if (task is not null)
        {
            await task.ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
        }

        await StopAsync().ConfigureAwait(false);
        lock (_sync)
        {
            _cancellation?.Dispose();
            _cancellation = null;
            _task = null;
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (_shouldReconnect() && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (_options.Delay == TimeSpan.Zero)
                {
                    // 同步失败配合零间隔会形成不可取消的热循环；每轮至少让出一次执行权。
                    await Task.Yield();
                    cancellationToken.ThrowIfCancellationRequested();
                }
                else
                {
                    await Task.Delay(_options.Delay, _timeProvider, cancellationToken)
                        .ConfigureAwait(false);
                }
                var connected = await _tryReconnect(cancellationToken).ConfigureAwait(false);
                CommunicationDiagnostics.RecordReconnectAttempt(
                    connected ? "success" : "faulted",
                    _diagnosticContext);
                if (connected)
                {
                    return;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch
            {
                CommunicationDiagnostics.RecordReconnectAttempt("faulted", _diagnosticContext);
                // 具体连接模块负责报告异常；supervisor 只维持下一次恢复机会。
            }
        }
    }
}
