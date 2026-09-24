using System.Threading.Channels;

namespace CommunicationNew.Runtime;

/// <summary>
/// 在单独的有界队列中按顺序调用用户处理器，隔离 IO loop 与订阅者执行时间。
/// 队列满时丢弃最旧通知；可靠请求结果必须通过独立路径完成。
/// </summary>
internal sealed class BoundedEventDispatcher<T> : IAsyncDisposable
{
    private readonly Channel<QueuedEvent> _channel;
    private readonly Action<T> _handler;
    private readonly Action<Exception> _errorHandler;
    private readonly string _eventType;
    private readonly Task _dispatchTask;
    private volatile bool _disposed;

    /// <summary>创建一个单消费者事件调度器。</summary>
    public BoundedEventDispatcher(
        int capacity,
        Action<T> handler,
        Action<Exception> errorHandler,
        string eventType)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(errorHandler);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);

        _handler = handler;
        _errorHandler = errorHandler;
        _eventType = eventType;
        _channel = Channel.CreateBounded<QueuedEvent>(
            new BoundedChannelOptions(capacity)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.DropOldest,
                AllowSynchronousContinuations = false,
            },
            dropped =>
            {
                CommunicationDiagnostics.RecordQueuedEvent(-1, dropped.Context);
                CommunicationDiagnostics.RecordDroppedEvent(_eventType, dropped.Context);
            });
        _dispatchTask = DispatchAsync();
    }

    /// <summary>尝试发布通知；释放后返回 false。</summary>
    public bool TryPublish(T item, CommunicationDiagnosticContext context)
    {
        if (_disposed)
        {
            return false;
        }

        CommunicationDiagnostics.RecordQueuedEvent(1, context);
        if (_channel.Writer.TryWrite(new QueuedEvent(item, context)))
        {
            return true;
        }

        // Dispose 可能与发布并发完成 writer；写入失败必须回滚预记的队列深度。
        CommunicationDiagnostics.RecordQueuedEvent(-1, context);
        return false;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _channel.Writer.TryComplete();
        await _dispatchTask.ConfigureAwait(false);
    }

    private async Task DispatchAsync()
    {
        await foreach (var queued in _channel.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            CommunicationDiagnostics.RecordQueuedEvent(-1, queued.Context);
            try
            {
                _handler(queued.Item);
            }
            catch (Exception ex)
            {
                try
                {
                    _errorHandler(ex);
                }
                catch
                {
                    // 用户错误处理器也失败时不能终止整个调度循环。
                }
            }
        }
    }

    private readonly record struct QueuedEvent(
        T Item,
        CommunicationDiagnosticContext Context);
}
