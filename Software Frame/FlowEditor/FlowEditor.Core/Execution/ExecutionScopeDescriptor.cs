// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：ExecutionScopeKind、ExecutionLaneDefinition、IExecutionScopeDescriptor。成员说明可结合下方具体实现阅读。
namespace FlowEditor.Core;

/// <summary>结构化执行作用域的调度类型。</summary>
public enum ExecutionScopeKind
{
    /// <summary>所有泳道并发运行。</summary>
    Parallel,

    /// <summary>单一泳道按配置轮次运行。</summary>
    Loop
}

/// <summary>结构化执行作用域中的稳定泳道定义。</summary>
public sealed class ExecutionLaneDefinition
{
    /// <summary>泳道持久化标识。</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>泳道显示名称。</summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Runtime 理解结构化容器所需的最小执行契约；不包含编辑器几何和视觉属性。
/// </summary>
public interface IExecutionScopeDescriptor
{
    /// <summary>获取容器的调度类型。</summary>
    ExecutionScopeKind ScopeKind { get; }

    /// <summary>获取容器拥有的稳定泳道。</summary>
    IReadOnlyList<ExecutionLaneDefinition> ExecutionLanes { get; }
}
