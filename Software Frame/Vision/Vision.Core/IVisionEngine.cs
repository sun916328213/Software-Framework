#nullable enable
using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Models;
using Vision.Core.Results;

namespace Vision.Core;

/// <summary>
/// 传统视觉与人工智能推理的统一业务入口。
/// Core 只定义执行契约，具体算法和运行库适配由实现层负责。
/// </summary>
/// <remarks>
/// 装配时冻结一个传统视觉实现，以及可选的一个人工智能实现。
/// 请求不能自行选择后端，也不能在失败后静默切换实现或设备。
/// 上下文释放必须等待实际调用结束，取消等待不授权提前销毁原生资源。
/// 每次返回的 ValueTask 只能消费一次；需要共享等待时，只调用一次 AsTask 并复用所得 Task。
/// 关闭等待取消后应重新调用释放方法，不能重复等待已消费的 ValueTask。
/// </remarks>
public interface IVisionEngine : IAsyncDisposable
{
    /// <summary>
    /// 已选实现及其操作能力的只读描述，不通过该属性探测未选运行库。
    /// </summary>
    VisionEngineDescriptor Descriptor
    {
        get;
    }

    /// <summary>
    /// 创建独立的工具上下文。调用者拥有上下文，编辑器只能借用。
    /// </summary>
    ValueTask<VisionContext> CreateContextAsync(
        CancellationToken cancellationToken);

    /// <summary>
    /// 校验请求参数、能力和运行条件，不执行训练、模型加载或算法处理。
    /// </summary>
    ValueTask<IReadOnlyList<VisionIssue>> ValidateAsync(
        VisionContext context,
        VisionOperation operation,
        CancellationToken cancellationToken);

    /// <summary>
    /// 准备或加载模型。成功后原子替换当前模型，并使原引用失效。
    /// 失败时清理候选资源，保留当前模型。
    /// </summary>
    ValueTask<VisionModelReference> PrepareModelAsync(
        VisionContext context,
        VisionModelDefinition definition,
        long revision,
        CancellationToken cancellationToken);

    /// <summary>
    /// 执行强类型请求，只返回本次结果。失败、取消和超时不能携带之前的数据。
    /// </summary>
    /// <remarks>
    /// 调用方取消可以抛出 OperationCanceledException。
    /// 等待超时不表示原生调用已经停止，实现仍须正确保留资源所有权。
    /// </remarks>
    ValueTask<VisionOutcome<TResult>> ExecuteAsync<TResult>(
        VisionContext context,
        VisionOperation<TResult> operation,
        CancellationToken cancellationToken)
        where TResult : class;

    /// <summary>
    /// 停止上下文接收请求，等待实际执行结束后释放其拥有的模型。
    /// 重复释放必须幂等；取消等待后，调用者仍能继续等待或重试关闭。
    /// </summary>
    ValueTask ReleaseContextAsync(
        VisionContext context,
        CancellationToken cancellationToken);
}
