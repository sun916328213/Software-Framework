// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：IReceiveSource。成员说明可结合下方具体实现阅读。
namespace Communication.Core.Abstractions;

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
