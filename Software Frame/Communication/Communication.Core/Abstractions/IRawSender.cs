// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：IRawSender。成员说明可结合下方具体实现阅读。
namespace Communication.Core.Abstractions;

/// <summary>
/// 发送原始字节且不隐式等待回复的最小能力接口。
/// 该能力只适用于保留原始报文语义的 TCP、UDP 和串口 adapter。
/// </summary>
public interface IRawSender
{
    /// <summary>发送一段原始数据。</summary>
    /// <param name="data">待发送的原始字节。</param>
    /// <param name="cancellationToken">取消当前发送操作的令牌。</param>
    /// <returns>数据成功交给底层传输时返回 <see langword="true"/>。</returns>
    Task<bool> SendAsync(
        byte[] data,
        CancellationToken cancellationToken = default);
}
