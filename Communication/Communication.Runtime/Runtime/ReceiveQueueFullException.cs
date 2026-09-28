namespace CommunicationNew.Runtime;

/// <summary>表示可靠字节流的有界收件箱已满，新数据不能被静默丢弃。</summary>
public sealed class ReceiveQueueFullException : InvalidOperationException
{
    /// <summary>使用收件箱容量创建异常。</summary>
    public ReceiveQueueFullException(int capacity)
        : base($"接收队列已达容量上限 {capacity}；为避免丢失字节，当前连接必须停止。")
    {
        Capacity = capacity;
    }

    /// <summary>发生拒绝时的收件箱容量。</summary>
    public int Capacity { get; }
}
