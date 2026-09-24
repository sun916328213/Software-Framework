namespace CommunicationNew.Abstractions;

/// <summary>
/// 逐条消费被动接收数据的最小能力接口。
/// 多个消费者共享同一收件箱，每条数据只会交付给其中一个消费者，不执行广播复制。
/// </summary>
/// <typeparam name="TPacket">一次接收产生的数据类型。</typeparam>
public interface IReceiveSource<TPacket>
{
    /// <summary>
    /// 等待并取出下一条数据。
    /// 调用取消时抛出 <see cref="OperationCanceledException"/>；连接代际结束时读取也会结束。
    /// </summary>
    ValueTask<TPacket> ReceiveAsync(CancellationToken cancellationToken = default);
}
