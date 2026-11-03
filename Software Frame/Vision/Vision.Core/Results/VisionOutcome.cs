#nullable enable
using Vision.Core.Diagnostics;

namespace Vision.Core.Results;

/// <summary>
/// 本次操作的强类型结果，状态、结果值与诊断必须保持一致。
/// </summary>
public sealed class VisionOutcome<TResult> where TResult : class
{
    /// <summary>
    /// 本次操作的执行状态。
    /// </summary>
    public VisionExecutionStatus Status
    {
        get;
    }

    /// <summary>
    /// 本次有效结果；失败、取消和超时时必须为空。
    /// </summary>
    public TResult? Value
    {
        get;
    }

    /// <summary>
    /// 未成功完成时的诊断信息。
    /// </summary>
    public VisionIssue? Issue
    {
        get;
    }

    /// <summary>
    /// 本次ExecuteAsync取得上下文独占权后的单调时钟耗时，含本次校验、执行和发布前检查；
    /// 不含上下文获取、模型准备及调用方后续处理。缺少实测时为null，不用零代替缺失。
    /// 取消以异常传播时不返回结果，也不声明后台原生调用已停止。
    /// </summary>
    public TimeSpan? ExecutionDuration { get; }

    /// <summary>
    /// 根据执行状态创建结果，确保结果值与诊断不会矛盾。
    /// </summary>
    public VisionOutcome(
        VisionExecutionStatus status,
        TResult? value,
        VisionIssue? issue = null,
        TimeSpan? executionDuration = null)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        if (status is VisionExecutionStatus.Success or VisionExecutionStatus.NoMatch)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value), "完成执行必须带本次强类型结果。");
            }

            if (issue != null)
            {
                throw new ArgumentException("完成执行不能同时携带错误。", nameof(issue));
            }
        }
        else
        {
            if (value != null)
            {
                throw new ArgumentException("失败/取消/超时不得携带旧数据。", nameof(value));
            }

            if (issue == null)
            {
                throw new ArgumentNullException(nameof(issue), "未完成必须说明原因。");
            }
        }
        if (executionDuration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(executionDuration));
        }
        ExecutionDuration = executionDuration;
        Status = status;
        Value = value;
        Issue = issue;
    }
}
