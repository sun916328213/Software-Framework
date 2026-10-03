// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：BoundedEventDispatcher、QueuedEvent。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Threading.Channels。导入不代表所有类型都会在运行时加载。
using System.Threading.Channels;

namespace Communication.Runtime;

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
    /// <remarks>
    /// 执行资源释放步骤；对应入口为 DisposeAsync。
    /// 正文中的关键协作调用包括 TryComplete、ConfigureAwait；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </remarks>
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

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 DispatchAsync。
    /// 正文中的关键协作调用包括 ConfigureAwait、ReadAllAsync、RecordQueuedEvent、_handler、_errorHandler；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
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

    /// <summary>
    /// QueuedEvent 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
    /// </summary>
    private readonly record struct QueuedEvent(
        T Item,
        CommunicationDiagnosticContext Context);
}
