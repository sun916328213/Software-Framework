using FlowEditor.Models;

namespace FlowEditor.Core;

/// <summary>
/// 一套与编辑器对象生命周期无关的流程运行状态。节点物化由宿主完成，本模块拥有
/// 活动图筛选、结构化作用域注入、调试门闩、执行追踪和引擎调用。
/// </summary>
/// <typeparam name="TNode">宿主提供的运行节点类型。</typeparam>
public sealed class FlowRuntimeInstance<TNode> where TNode : INodeExecutable
{
    private readonly FlowChartExecutionEngine _engine;

    /// <summary>创建一套独立运行状态。</summary>
    /// <param name="name">流程名称。</param>
    /// <param name="nodes">已经物化且归本实例所有的节点。</param>
    /// <param name="connections">归本实例所有的连线。</param>
    /// <param name="engine">归本实例所有的执行引擎。</param>
    public FlowRuntimeInstance(
        string name,
        IReadOnlyList<TNode> nodes,
        IReadOnlyList<ConnectionModel> connections,
        FlowChartExecutionEngine engine)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(connections);
        ArgumentNullException.ThrowIfNull(engine);
        Name = name;
        Nodes = nodes;
        Connections = connections;
        _engine = engine;
    }

    /// <summary>获取流程名称。</summary>
    public string Name { get; }

    /// <summary>获取本实例独占的节点集合。</summary>
    public IReadOnlyList<TNode> Nodes { get; }

    /// <summary>获取本实例独占的连线集合。</summary>
    public IReadOnlyList<ConnectionModel> Connections { get; }

    /// <summary>获取节点事件和连线传值追踪器。</summary>
    public ExecutionJournal Journal { get; } = new();

    /// <summary>
    /// P1：在不执行任何节点的前提下验证活动顶层图（结构 + 各节点自身配置）。
    /// Workbench 的"验证流程"调用此方法，在真正执行设备动作前列出全部可发现问题。
    /// </summary>
    /// <param name="services">受限服务容器（透传给各节点验证上下文，可为空）。</param>
    /// <param name="variables">流程变量快照（透传，可为空）。</param>
    /// <param name="ct">取消令牌。</param>
    public Task<FlowValidationReport> ValidateAsync(
        IServiceProvider? services = null,
        IReadOnlyDictionary<string, object?>? variables = null,
        CancellationToken ct = default)
    {
        var activeNodes = Nodes.Where(node => !node.IsExcludedFromExecution).ToList();
        var activeIds = activeNodes.Select(node => node.Id).ToHashSet();
        var activeConnections = Connections.Where(connection =>
                activeIds.Contains(connection.SourceNodeId) && activeIds.Contains(connection.TargetNodeId))
            .ToList();
        return FlowNodeValidator.ValidateAsync(activeNodes, activeConnections, services, variables, ct);
    }

    /// <summary>获取或设置可选的断点与单步控制器。</summary>
    public DebugController? Debug { get; set; }

    /// <summary>执行活动顶层图，并为结构化节点注入同一次运行的完整图快照。</summary>
    /// <param name="ct">取消令牌。</param>
    /// <param name="log">可选日志回调。</param>
    public async Task RunAsync(CancellationToken ct, Action<string>? log = null)
    {
        Journal.Clear();
        var activeNodes = Nodes.Where(node => !node.IsExcludedFromExecution).ToList();
        var activeIds = activeNodes.Select(node => node.Id).ToHashSet();
        var activeConnections = Connections.Where(connection =>
                activeIds.Contains(connection.SourceNodeId) && activeIds.Contains(connection.TargetNodeId))
            .ToList();
        var runtime = new ExecutionScopeRuntime(
            activeNodes.Cast<INodeExecutable>().ToList(), activeConnections, _engine);
        foreach (var node in activeNodes.OfType<IExecutionScopeRuntimeAware>())
            node.ExecutionScopeRuntime = runtime;
        var topLevel = ExecutionGraphScope.CreateTopLevel(activeNodes, activeConnections);

        await _engine.ExecuteAsync(
            topLevel.Nodes,
            topLevel.Connections,
            ct,
            log ?? (_ => { }),
            onBeforeNode: Debug == null ? null : nodeId => Debug.WaitIfPausedAsync(nodeId, ct),
            executionOrder: null,
            journal: Journal);
    }
}
