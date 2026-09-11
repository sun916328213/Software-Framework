using System.Text.Json.Serialization;
using FlowEditor.Core;
using FlowEditor.Models;
using WPFCustomControls.PropertyGrid.Models;

namespace FlowEditor.Nodes.FlowControl;

/// <summary>主画布结构化循环容器；内部节点组成循环体，外层不再绘制回边。</summary>
[FlowTool("循环容器", "Repeat", ToolGroup = "流程控制", SortIndex = 6,
    Description = "把节点拖入容器作为循环体，执行指定次数后继续")]
public sealed class LoopContainerNode : NodeModel, IExecutionScopeContainer, IFlowStructureValidationSource
{
    public const double DefaultWidth = 500;
    public const double DefaultHeight = 300;

    private double _containerWidth = DefaultWidth;
    private double _containerHeight = DefaultHeight;
    private int _iterationCount = 1;
    private int _iterationDelayMs;
    private int _currentIteration;
    private List<ExecutionLaneDefinition> _lanes = [new() { Name = "循环体" }];

    [NodeProperty(DisplayName = "容器宽度", Group = "0.容器", Order = 0,
        IsDisplay = false, PortConfigurable = false)]
    public double ContainerWidth
    {
        get => _containerWidth;
        set => SetProperty(ref _containerWidth, Math.Clamp(value, 420, 3200));
    }

    [NodeProperty(DisplayName = "容器高度", Group = "0.容器", Order = 1,
        IsDisplay = false, PortConfigurable = false)]
    public double ContainerHeight
    {
        get => _containerHeight;
        set => SetProperty(ref _containerHeight, Math.Clamp(value, 240, 2400));
    }

    [NodeProperty(DisplayName = "泳道定义", Group = "0.容器", Order = 2,
        IsDisplay = false, PortConfigurable = false)]
    public List<ExecutionLaneDefinition> Lanes
    {
        get => _lanes;
        set
        {
            _lanes = value is { Count: 1 } ? value : [new() { Name = "循环体" }];
            OnPropertyChanged();
            OnPropertyChanged(nameof(ExecutionLanes));
        }
    }

    [NodeProperty(DisplayName = "循环次数", Group = "1.基础信息", Order = 1,
        Min = -1, Max = 1000000, Step = 1, PortConfigurable = false,
        Description = "完整执行容器内循环体的次数；-1 表示无限循环")]
    public int IterationCount
    {
        get => _iterationCount;
        set => SetProperty(ref _iterationCount, value == -1 ? -1 : Math.Clamp(value, 1, 1_000_000));
    }

    [NodeProperty(DisplayName = "循环延时(ms)", Group = "1.基础信息", Order = 2,
        Min = 0, Max = 86400000, Step = 100, PortConfigurable = false,
        Description = "每轮循环完成后、下一轮开始前等待的毫秒数；0 表示不等待")]
    public int IterationDelayMs
    {
        get => _iterationDelayMs;
        set => SetProperty(ref _iterationDelayMs, Math.Clamp(value, 0, 86_400_000));
    }

    [JsonIgnore]
    public int CurrentIteration
    {
        get => _currentIteration;
        private set => SetProperty(ref _currentIteration, value);
    }

    public ExecutionScopeKind ScopeKind => ExecutionScopeKind.Loop;
    public bool ShowBoundaryPorts => false;
    public IReadOnlyList<ExecutionLaneDefinition> ExecutionLanes => Lanes;

    public LoopContainerNode()
    {
        Name = "循环容器";
        IsInEnabled = true;
        IsOutEnabled = true;
    }

    public IEnumerable<FlowDiagnostic> ValidateStructure()
    {
        if (Lanes.Count != 1)
            yield return new FlowDiagnostic(
                FlowDiagnosticSeverity.Error, "E-LOOP-BODY-COUNT",
                $"循环容器 [{Name}] 必须且只能包含一个循环体");
    }

    public override async Task ExecuteAsync(CancellationToken ct)
    {
        var runtime = ExecutionScopeRuntime ?? (Service == null
            ? null
            : new ExecutionScopeRuntime(
                Service.Nodes.Select(node => node.Model).ToList(),
                Service.Connections.ToList()));
        if (runtime == null)
            throw new InvalidOperationException($"循环容器 [{Name}] 缺少运行图上下文。");

        var lane = Lanes[0];
        var body = ExecutionGraphScope.Create(
            runtime.Nodes, runtime.Connections, Id, lane.Id);
        if (body.Nodes.Count == 0)
            throw new InvalidOperationException($"循环容器 [{Name}] 的循环体没有节点。");

        var memberIds = body.Nodes.Select(node => node.Id).ToHashSet();

        CurrentIteration = 0;
        await LoopFlowRunner.ExecuteAsync(
            new ParallelFlowLane<NodeModel>(lane.Id, lane.Name, body.Nodes, body.Connections),
            IterationCount,
            ct,
            Console.WriteLine,
            (iteration, _) =>
            {
                CurrentIteration = iteration;
                TransferBoundaryData(runtime, memberIds, importIntoBody: true);
            },
            IterationDelayMs);
        TransferBoundaryData(runtime, memberIds, importIntoBody: false);
    }

    /// <summary>
    /// 从循环体中的指定节点恢复当前失败轮次，然后继续剩余轮次。
    /// 选择失败节点表示重试该节点；选择其后节点表示跳过故障点。
    /// </summary>
    public async Task ResumeFromNodeAsync(string startNodeId, CancellationToken ct, Action<string> log)
    {
        var runtime = ExecutionScopeRuntime
            ?? throw new InvalidOperationException($"循环容器 [{Name}] 缺少运行图上下文。");
        var lane = Lanes[0];
        var body = ExecutionGraphScope.Create(runtime.Nodes, runtime.Connections, Id, lane.Id);
        var start = body.Nodes.FirstOrDefault(node => node.Id == startNodeId)
            ?? throw new ArgumentException("恢复节点不属于该循环容器。", nameof(startNodeId));
        var recovery = ExecutionGraphScope.CreateReachable(body.Nodes, body.Connections, startNodeId);
        var memberIds = body.Nodes.Select(node => node.Id).ToHashSet();
        var current = Math.Max(1, CurrentIteration);

        CurrentIteration = current;
        TransferBoundaryData(runtime, memberIds, importIntoBody: true);
        log($"[恢复] 循环第 {current} 轮从节点 [{start.Name}] 继续");
        await new FlowChartExecutionEngine().ExecuteAsync(
            recovery.Nodes, recovery.Connections, ct, log);

        var nextIteration = current + 1;
        if (IterationCount == -1 || nextIteration <= IterationCount)
        {
            if (IterationDelayMs > 0)
                await Task.Delay(IterationDelayMs, ct);
            await LoopFlowRunner.ExecuteAsync(
                new ParallelFlowLane<NodeModel>(lane.Id, lane.Name, body.Nodes, body.Connections),
                IterationCount,
                ct,
                log,
                (iteration, _) =>
                {
                    CurrentIteration = iteration;
                    TransferBoundaryData(runtime, memberIds, importIntoBody: true);
                },
                IterationDelayMs,
                nextIteration);
        }

        TransferBoundaryData(runtime, memberIds, importIntoBody: false);
    }

    /// <summary>
    /// 循环容器是画布上的结构区域，不拥有可见的数据端口。跨边界的数据线仍直接连接
    /// 原节点；执行时在每轮开始前导入数据，在全部迭代结束后导出最终结果。
    /// </summary>
    private static void TransferBoundaryData(
        ExecutionScopeRuntime runtime,
        IReadOnlySet<string> memberIds,
        bool importIntoBody)
    {
        var nodeMap = runtime.Nodes.ToDictionary(node => node.Id);
        foreach (var connection in runtime.Connections)
        {
            var sourceInside = memberIds.Contains(connection.SourceNodeId);
            var targetInside = memberIds.Contains(connection.TargetNodeId);
            if (importIntoBody
                    ? sourceInside || !targetInside
                    : !sourceInside || targetInside)
                continue;
            if (!nodeMap.TryGetValue(connection.SourceNodeId, out var sourceNode) ||
                !nodeMap.TryGetValue(connection.TargetNodeId, out var targetNode))
                continue;

            var sourcePort = sourceNode.FindPortById(connection.SourcePortId);
            var targetPort = targetNode.FindPortById(connection.TargetPortId);
            if (sourcePort == null || targetPort == null || sourcePort.DataType == PortType.Flow)
                continue;
            if (!targetPort.IsTypeCompatibleWith(sourcePort.DataType))
                throw new FlowChartTypeMismatchException(
                    $"源端口 [{sourceNode.Name}].{sourcePort.Name}({sourcePort.DataType}) " +
                    $"与目标端口 [{targetNode.Name}].{targetPort.Name}({targetPort.DataType}) 声明类型不兼容");
            if (!targetPort.IsCompatible(sourcePort.Value))
            {
                var valueType = sourcePort.Value?.GetType().Name ?? "null";
                throw new FlowChartTypeMismatchException(
                    $"源端口 [{sourceNode.Name}].{sourcePort.Name} 的值 " +
                    $"'{sourcePort.Value}'（运行时类型: {valueType}）" +
                    $"无法传入目标端口 [{targetNode.Name}].{targetPort.Name}({targetPort.DataType})");
            }

            targetPort.Value = sourcePort.Value;
        }
    }
}
