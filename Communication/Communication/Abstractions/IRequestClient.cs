namespace CommunicationNew.Abstractions;

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
