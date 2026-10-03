// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：IRequestReplyClient。成员说明可结合下方具体实现阅读。
namespace Communication.Core.Abstractions;

/// <summary>
/// 发送一次原始请求并等待对应回复的最小能力接口。
/// 实现必须明确限制并发请求，并让取消、超时、断开和释放结束挂起等待。
/// </summary>
public interface IRequestReplyClient
{
    /// <summary>发送原始请求并返回本次请求对应的回复。</summary>
    /// <param name="request">待发送的原始请求。</param>
    /// <param name="cancellationToken">取消当前请求的令牌。</param>
    Task<byte[]> SendAndReceiveAsync(
        byte[] request,
        CancellationToken cancellationToken = default);
}
