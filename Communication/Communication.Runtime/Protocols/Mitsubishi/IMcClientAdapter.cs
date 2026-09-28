using CommunicationNew.Abstractions;

namespace CommunicationNew.Protocols.Mitsubishi;

/// <summary>
/// MC 客户端后端 adapter 的最小 interface。
/// 实现可以使用 SLMP Socket、MX Component 桥接进程或测试替身，且不得向调用方泄漏厂商 SDK 类型。
/// </summary>
public interface IMcClientAdapter : IConnection
{
    /// <summary>执行一个已经完成地址、数量和负载校验的 MC 请求。</summary>
    /// <param name="request">与具体传输实现无关的 MC 语义请求。</param>
    /// <param name="cancellationToken">取消等待操作的令牌。</param>
    /// <returns>经过后端状态码校验的 MC 响应。</returns>
    ValueTask<McResponse> ExecuteAsync(
        McRequest request,
        CancellationToken cancellationToken = default);
}
