namespace FlowEditor.Core;

public enum ExecutionScopeKind
{
    Parallel,
    Loop
}

/// <summary>结构化执行容器中的稳定泳道定义。</summary>
public sealed class ExecutionLaneDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// 主画布结构化执行容器的运行时接口。表现层负责几何编辑，Core 只读取作用域和泳道。
/// </summary>
public interface IExecutionScopeContainer
{
    ExecutionScopeKind ScopeKind { get; }

    double ContainerWidth { get; set; }

    double ContainerHeight { get; set; }

    /// <summary>是否在容器边缘显示自身的流程端口。</summary>
    bool ShowBoundaryPorts { get; }

    IReadOnlyList<ExecutionLaneDefinition> ExecutionLanes { get; }
}
