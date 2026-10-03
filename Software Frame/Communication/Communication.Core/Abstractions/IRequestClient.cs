// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：IRequestClient。成员说明可结合下方具体实现阅读。
namespace Communication.Core.Abstractions;

/// <summary>执行类型化请求并返回类型化响应的能力接口。</summary>
public interface IRequestClient<in TRequest, TResponse>
{
    /// <summary>
    /// 执行一次请求。
    /// 调用者取消抛出 <see cref="OperationCanceledException"/>，session 总超时抛出 <see cref="TimeoutException"/>。
    /// </summary>
    ValueTask<TResponse> ExecuteAsync(
        TRequest request,
        CancellationToken cancellationToken = default);
}
