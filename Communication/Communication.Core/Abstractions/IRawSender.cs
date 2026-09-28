namespace CommunicationNew.Abstractions;

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
