// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：RequestQueueFullException。成员说明可结合下方具体实现阅读。
namespace Communication.Runtime;

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
