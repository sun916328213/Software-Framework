using System.Threading.Channels;
using CommunicationNew.Abstractions;

namespace CommunicationNew.Runtime;

/// <summary>有界、按连接代际管理的被动接收收件箱。</summary>
internal sealed class BoundedReceiveInbox : IDisposable
{
    private readonly object _sync = new();
    private readonly int _capacity;
    private readonly ReceiveOverflowPolicy _overflowPolicy;
    private Channel<QueuedPacket>? _channel;
    private long _nextGeneration;
    private long _currentGeneration;
    private bool _disposed;

    /// <summary>创建收件箱；字节流使用拒绝策略，数据报可使用丢弃最旧策略。</summary>
    public BoundedReceiveInbox(int capacity, ReceiveOverflowPolicy overflowPolicy)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        _capacity = capacity;
        _overflowPolicy = overflowPolicy;
    }

    /// <summary>开始新的连接代际；上一个代际中尚未消费的数据会被明确计为丢弃。</summary>
    public long BeginGeneration()
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            CompleteGenerationCore();
            _currentGeneration = ++_nextGeneration;
            _channel = CreateChannel();
            return _currentGeneration;
        }
    }

    /// <summary>结束当前连接代际并解除所有挂起读取。</summary>
    public void EndGeneration(long generation)
    {
        lock (_sync)
        {
            if (generation != _currentGeneration)
            {
                return;
            }

            CompleteGenerationCore();
        }
    }

    /// <summary>发布一条接收数据；可靠队列满时抛出显式异常。</summary>
    public void Publish(ReceivedPacket packet, CommunicationDiagnosticContext context)
    {
        ArgumentNullException.ThrowIfNull(packet);

        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var channel = _channel ??
                throw new InvalidOperationException("当前没有可接收数据的活动连接代际。");

            CommunicationDiagnostics.RecordQueuedReceive(1, context);
            if (channel.Writer.TryWrite(new QueuedPacket(packet, context)))
            {
                return;
            }

            // Writer 已结束或可靠队列已满时，回滚写入前记录的深度。
            CommunicationDiagnostics.RecordQueuedReceive(-1, context);
            throw new ReceiveQueueFullException(_capacity);
        }
    }

    /// <summary>从调用时所在的连接代际取出一条数据。</summary>
    public async ValueTask<ReceivedPacket> ReceiveAsync(
        CancellationToken cancellationToken = default)
    {
        ChannelReader<QueuedPacket> reader;
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            reader = (_channel ??
                throw new InvalidOperationException("设备当前没有活动连接，无法接收数据。"))
                .Reader;
        }

        var queued = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        CommunicationDiagnostics.RecordQueuedReceive(-1, queued.Context);
        return queued.Packet;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            CompleteGenerationCore();
        }
    }

    private Channel<QueuedPacket> CreateChannel()
    {
        var options = new BoundedChannelOptions(_capacity)
        {
            SingleReader = false,
            SingleWriter = false,
            FullMode = _overflowPolicy == ReceiveOverflowPolicy.DropOldest
                ? BoundedChannelFullMode.DropOldest
                : BoundedChannelFullMode.Wait,
            AllowSynchronousContinuations = false,
        };
        return Channel.CreateBounded<QueuedPacket>(options, dropped =>
        {
            CommunicationDiagnostics.RecordQueuedReceive(-1, dropped.Context);
            CommunicationDiagnostics.RecordDroppedReceive(dropped.Context);
        });
    }

    private void CompleteGenerationCore()
    {
        var channel = _channel;
        if (channel is null)
        {
            return;
        }

        _channel = null;
        _currentGeneration = 0;
        channel.Writer.TryComplete();
        while (channel.Reader.TryRead(out var discarded))
        {
            CommunicationDiagnostics.RecordQueuedReceive(-1, discarded.Context);
            CommunicationDiagnostics.RecordDroppedReceive(discarded.Context);
        }
    }

    private readonly record struct QueuedPacket(
        ReceivedPacket Packet,
        CommunicationDiagnosticContext Context);
}

/// <summary>收件箱达到容量时使用的传输语义策略。</summary>
internal enum ReceiveOverflowPolicy
{
    /// <summary>不允许丢失连续字节，队列满时拒绝当前连接。</summary>
    Reject,

    /// <summary>保留最新数据报并丢弃最旧数据报。</summary>
    DropOldest,
}
