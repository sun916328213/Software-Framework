// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：LatestValueDispatcher。成员说明可结合下方具体实现阅读。
namespace Communication.Runtime;

/// <summary>
/// 按键合并最新值的有界事件调度器；同一键等待消费期间只保留最后一次发布。
/// </summary>
internal sealed class LatestValueDispatcher<TKey, TValue> : IAsyncDisposable
    where TKey : notnull
{
    private readonly object _gate = new();
    private readonly Queue<TKey> _order = new();
    private readonly Dictionary<TKey, TValue> _pending = new();
    private readonly SemaphoreSlim _available = new(0);
    private readonly int _capacity;
    private readonly Action<TValue> _handler;
    private readonly Action<Exception> _errorHandler;
    private readonly string _eventType;
    private readonly CommunicationDiagnosticContext _diagnosticContext;
    private readonly Task _dispatchTask;
    private bool _disposed;

    /// <summary>创建按键保留最新值的单消费者调度器。</summary>
    public LatestValueDispatcher(
        int capacity,
        Action<TValue> handler,
        Action<Exception> errorHandler,
        string eventType,
        CommunicationDiagnosticContext diagnosticContext)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(errorHandler);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);

        _capacity = capacity;
        _handler = handler;
        _errorHandler = errorHandler;
        _eventType = eventType;
        _diagnosticContext = diagnosticContext;
        _dispatchTask = DispatchAsync();
    }

    /// <summary>发布最新值；释放后返回 false。</summary>
    public bool TryPublish(TKey key, TValue value)
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return false;
            }

            if (_pending.ContainsKey(key))
            {
                _pending[key] = value;
                CommunicationDiagnostics.RecordCoalescedEvent(
                    _eventType,
                    _diagnosticContext);
                return true;
            }

            if (_pending.Count == _capacity)
            {
                var droppedKey = _order.Dequeue();
                _pending.Remove(droppedKey);
                CommunicationDiagnostics.RecordDroppedEvent(
                    _eventType,
                    _diagnosticContext);
            }
            else
            {
                CommunicationDiagnostics.RecordQueuedEvent(1, _diagnosticContext);
                _available.Release();
            }

            _pending.Add(key, value);
            _order.Enqueue(key);
            return true;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// 执行资源释放步骤；对应入口为 DisposeAsync。
    /// 正文中的关键协作调用包括 Release、ConfigureAwait、Dispose；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _available.Release();
        }

        await _dispatchTask.ConfigureAwait(false);
        _available.Dispose();
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 DispatchAsync。
    /// 正文中的关键协作调用包括 ConfigureAwait、WaitAsync、Dequeue、Remove、RecordQueuedEvent；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    private async Task DispatchAsync()
    {
        while (true)
        {
            await _available.WaitAsync().ConfigureAwait(false);

            TValue value;
            lock (_gate)
            {
                if (_order.Count == 0)
                {
                    if (_disposed)
                    {
                        return;
                    }

                    continue;
                }

                var key = _order.Dequeue();
                value = _pending[key];
                _pending.Remove(key);
            }

            CommunicationDiagnostics.RecordQueuedEvent(-1, _diagnosticContext);
            try
            {
                _handler(value);
            }
            catch (Exception ex)
            {
                try
                {
                    _errorHandler(ex);
                }
                catch
                {
                    // 用户错误处理器失败时也不能终止后续点位通知。
                }
            }
        }
    }
}
