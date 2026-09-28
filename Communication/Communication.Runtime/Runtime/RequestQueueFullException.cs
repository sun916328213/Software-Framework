namespace CommunicationNew.Runtime;

/// <summary>表示连接的有界请求队列已满，新请求未进入执行流水线。</summary>
public sealed class RequestQueueFullException : InvalidOperationException
{
    /// <summary>使用请求队列容量创建异常。</summary>
    public RequestQueueFullException(int capacity)
        : base($"请求队列已达容量上限 {capacity}，新请求已拒绝。")
    {
        Capacity = capacity;
    }

    /// <summary>发生拒绝时的请求队列容量。</summary>
    public int Capacity { get; }
}
