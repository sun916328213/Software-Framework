// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：NodeExecutionRecord、NodeRetryRecord、NodeResourceWaitRecord、InMemoryNodeTelemetry。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Core、System.Collections.Concurrent。导入不代表所有类型都会在运行时加载。
using FlowEditor.Core;
using System.Collections.Concurrent;

namespace FlowEditor.Runtime;

/// <summary>单次节点执行的遥测记录。</summary>
/// <param name="NodeId">节点 Id。</param>
/// <param name="NodeName">节点显示名。</param>
/// <param name="ElapsedMs">耗时（毫秒）。</param>
/// <param name="Success">是否成功。</param>
/// <param name="ErrorCode">失败时的稳定错误码；成功为 null。</param>
public sealed record NodeExecutionRecord(
    string NodeId,
    string NodeName,
    long ElapsedMs,
    bool Success,
    string? ErrorCode);

/// <summary>单次重试的遥测记录。</summary>
/// <param name="NodeId">节点 Id。</param>
/// <param name="Attempt">即将开始的 attempt 序号（≥2）。</param>
/// <param name="ErrorCode">触发重试的错误码（可为空）。</param>
public sealed record NodeRetryRecord(string NodeId, int Attempt, string? ErrorCode);

/// <summary>资源锁等待的遥测记录（P1 并发调度/P5 使用）。</summary>
/// <param name="ResourceKey">资源键。</param>
/// <param name="WaitMs">等待耗时（毫秒）。</param>
public sealed record NodeResourceWaitRecord(string ResourceKey, long WaitMs);

/// <summary>
/// 内存遥测收集器（P2）。引擎只记录事实（耗时、重试、等待、失败类型），
/// Workbench 负责展示；单元测试用此实现断言调度行为。
/// 线程安全，可在 DAG 并行调度中并发写入。
/// </summary>
public sealed class InMemoryNodeTelemetry : INodeTelemetry
{
    private readonly ConcurrentQueue<NodeExecutionRecord> _executions = new();
    private readonly ConcurrentQueue<NodeRetryRecord> _retries = new();
    private readonly ConcurrentQueue<NodeResourceWaitRecord> _waits = new();

    /// <inheritdoc />
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 RecordExecution。
    /// 正文中的关键协作调用包括 Enqueue；具体条件和先后次序见方法体。
    /// </remarks>
    public void RecordExecution(string nodeId, string nodeName, long elapsedMs, bool success, string? errorCode) =>
        _executions.Enqueue(new NodeExecutionRecord(nodeId, nodeName, elapsedMs, success, errorCode));

    /// <inheritdoc />
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 RecordResourceWait。
    /// 正文中的关键协作调用包括 Enqueue；具体条件和先后次序见方法体。
    /// </remarks>
    public void RecordResourceWait(string resourceKey, long waitMs) =>
        _waits.Enqueue(new NodeResourceWaitRecord(resourceKey, waitMs));

    /// <inheritdoc />
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 RecordRetry。
    /// 正文中的关键协作调用包括 Enqueue；具体条件和先后次序见方法体。
    /// </remarks>
    public void RecordRetry(string nodeId, int attempt) =>
        _retries.Enqueue(new NodeRetryRecord(nodeId, attempt, ErrorCode: null));

    /// <summary>记录带错误码的重试（引擎实际调用此重载）。</summary>
    public void RecordRetry(string nodeId, int attempt, string? errorCode) =>
        _retries.Enqueue(new NodeRetryRecord(nodeId, attempt, errorCode));
    /// <summary>获取全部执行记录（按记录顺序）。</summary>
    public IReadOnlyList<NodeExecutionRecord> Executions => [.. _executions];

    /// <summary>获取全部重试记录。</summary>
    public IReadOnlyList<NodeRetryRecord> Retries => [.. _retries];

    /// <summary>获取全部资源等待记录。</summary>
    public IReadOnlyList<NodeResourceWaitRecord> ResourceWaits => [.. _waits];

    /// <summary>清空全部记录。</summary>
    public void Clear()
    {
        while (_executions.TryDequeue(out _)) { }
        while (_retries.TryDequeue(out _)) { }
        while (_waits.TryDequeue(out _)) { }
    }
}
