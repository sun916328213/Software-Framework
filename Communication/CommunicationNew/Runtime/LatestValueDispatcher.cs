namespace CommunicationNew.Runtime;

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
