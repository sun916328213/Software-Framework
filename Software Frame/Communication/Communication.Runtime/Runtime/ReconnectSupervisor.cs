// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：ReconnectSupervisor。成员说明可结合下方具体实现阅读。
namespace Communication.Runtime;

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
    /// <remarks>
    /// 执行资源释放步骤；对应入口为 DisposeAsync。
    /// 正文中的关键协作调用包括 ConfigureAwait、StopAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
    /// </remarks>
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

    /// <summary>
    /// 运行当前处理流程；对应入口为 RunAsync。
    /// 正文中的关键协作调用包括 _shouldReconnect、Yield、ThrowIfCancellationRequested、ConfigureAwait、Delay；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
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
