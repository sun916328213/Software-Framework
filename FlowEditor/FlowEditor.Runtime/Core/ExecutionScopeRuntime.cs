using FlowEditor.Models;

namespace FlowEditor.Core;

/// <summary>
/// 一次运行所拥有的完整节点图。结构化容器用它提取自己的子作用域，
/// 因而不依赖编辑器 ViewModel 或窗口服务，也可在 FlowInstance 中运行。
/// </summary>
public sealed record ExecutionScopeRuntime(
    IReadOnlyList<INodeExecutable> Nodes,
    IReadOnlyList<ConnectionModel> Connections,
    FlowChartExecutionEngine? Engine = null);
