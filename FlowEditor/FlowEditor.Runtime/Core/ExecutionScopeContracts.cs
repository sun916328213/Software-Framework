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

/// <summary>允许结构化节点读取当前完整执行图的运行期能力。</summary>
public interface IExecutionScopeRuntimeAware
{
    /// <summary>获取或设置当前执行会话的完整图快照。</summary>
    ExecutionScopeRuntime? ExecutionScopeRuntime { get; set; }
}
