#nullable enable

namespace Vision.Core.Results;

/// <summary>
/// 本次操作的执行状态；取消或超时不代表原生调用已经停止。
/// </summary>
public enum VisionExecutionStatus
{
    /// <summary>
    /// 操作完成，并返回本次有效结果。
    /// </summary>
    Success,

    /// <summary>
    /// 操作完成，但未找到满足条件的目标。
    /// </summary>
    NoMatch,

    /// <summary>
    /// 所选实现不支持该请求。
    /// </summary>
    Unsupported,

    /// <summary>
    /// 操作失败，不能返回旧结果。
    /// </summary>
    Failed,

    /// <summary>
    /// 操作取消，不能返回有效结果。
    /// </summary>
    Cancelled,

    /// <summary>
    /// 操作超时，资源释放仍须等待实际调用结束。
    /// </summary>
    TimedOut
}
