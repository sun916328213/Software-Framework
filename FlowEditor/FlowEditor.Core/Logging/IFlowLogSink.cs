namespace FlowEditor.Core;

/// <summary>接收结构化流程运行日志的宿主 seam。</summary>
public interface IFlowLogSink
{
    /// <summary>写入一条已经补全流程上下文和等级的日志记录。</summary>
    /// <param name="record">待写入的结构化日志。</param>
    void Write(FlowLogRecord record);
}

/// <summary>描述一次流程、节点或调度事件的完整日志上下文。</summary>
public sealed record FlowLogRecord
{
    /// <summary>创建不可变流程日志记录。</summary>
    public FlowLogRecord(
        string? processName,
        string? flowName,
        string? nodeName,
        long runId,
        string message,
        FlowLogLevel level)
    {
        ProcessName = processName;
        FlowName = flowName;
        NodeName = nodeName;
        RunId = runId;
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Level = level;
    }

    /// <summary>流程容器名称。</summary>
    public string? ProcessName { get; }

    /// <summary>流程标签页或任务名称。</summary>
    public string? FlowName { get; }

    /// <summary>产生日志的节点名称；流程级事件为空。</summary>
    public string? NodeName { get; }

    /// <summary>当前流程运行序号。</summary>
    public long RunId { get; }

    /// <summary>已补全运行序号和节点前缀的日志正文。</summary>
    public string Message { get; }

    /// <summary>日志等级。</summary>
    public FlowLogLevel Level { get; }

    /// <summary>结构化流程日志路由；任务名为空时不存在。</summary>
    public FlowLogRoute? Route => FlowLogRoute.TryCreate(ProcessName, FlowName, out var route)
        ? route
        : null;

    /// <summary>用于宿主选择日志配置实例的“流程容器\任务”键。</summary>
    public string? LogKey => Route?.DisplayKey;
}

/// <summary>无头运行或未配置外部日志时使用的无副作用 sink。</summary>
public sealed class NullFlowLogSink : IFlowLogSink
{
    /// <summary>共享无副作用实例。</summary>
    public static NullFlowLogSink Instance { get; } = new();

    /// <inheritdoc />
    public void Write(FlowLogRecord record)
    {
    }
}
