using System.Text.Json.Serialization;
using FlowEditor.Core;
using WPFCustomControls.PropertyGrid.Models;

namespace FlowEditor.Nodes.FlowControl;

/// <summary>主画布结构化并行容器；所有泳道同时启动，全部完成后才继续。</summary>
// 兼容已经保存的早期流程；新流程从工具箱使用 ParallelForkNode + AND BranchMergeNode。
public sealed class ParallelContainerNode : NodeModel, IExecutionScopeContainer, IFlowStructureValidationSource
{
    public const double DefaultWidth = 720;
    public const double DefaultHeight = 420;

    private double _containerWidth = DefaultWidth;
    private double _containerHeight = DefaultHeight;
    private List<ExecutionLaneDefinition> _lanes = CreateDefaultLanes();

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
        set => SetProperty(ref _containerHeight, Math.Clamp(value, 260, 2400));
    }

    [NodeProperty(DisplayName = "泳道定义", Group = "0.容器", Order = 2,
        IsDisplay = false, PortConfigurable = false)]
    public List<ExecutionLaneDefinition> Lanes
    {
        get => _lanes;
        set
        {
            _lanes = value is { Count: > 0 } ? value : CreateDefaultLanes();
            OnPropertyChanged();
            OnPropertyChanged(nameof(ExecutionLanes));
            OnPropertyChanged(nameof(BranchCount));
        }
    }

    [JsonIgnore]
    [NodeProperty(DisplayName = "分支数量", Group = "1.基础信息", Order = 0,
        Min = 2, Max = 12, Step = 1, PortConfigurable = false,
        Description = "纵向泳道数量；所有泳道同时启动并自动汇聚")]
    public int BranchCount
    {
        get => Lanes.Count;
        set => ResizeLanes(Math.Clamp(value, 2, 12));
    }

    public ExecutionScopeKind ScopeKind => ExecutionScopeKind.Parallel;
    public bool ShowBoundaryPorts => true;
    public IReadOnlyList<ExecutionLaneDefinition> ExecutionLanes => Lanes;

    public ParallelContainerNode()
    {
        Name = "并行容器";
        IsInEnabled = true;
        IsOutEnabled = true;
    }

    public IEnumerable<FlowDiagnostic> ValidateStructure()
    {
        if (Lanes.Count < 2)
            yield return new FlowDiagnostic(
                FlowDiagnosticSeverity.Error, "E-PAR-BRANCH-COUNT",
                $"并行容器 [{Name}] 至少需要两个泳道");

        foreach (var duplicate in Lanes.GroupBy(lane => lane.Id).Where(group => group.Count() > 1))
            yield return new FlowDiagnostic(
                FlowDiagnosticSeverity.Error, "E-PAR-BRANCH-ID",
                $"并行容器 [{Name}] 存在重复泳道标识 [{duplicate.Key}]");
    }

    public override async Task ExecuteAsync(CancellationToken ct)
    {
        var runtime = ExecutionScopeRuntime ?? (Service == null
            ? null
            : new ExecutionScopeRuntime(
                Service.Nodes.Select(node => node.Model).ToList(),
                Service.Connections.ToList()));
        if (runtime == null)
            throw new InvalidOperationException($"并行容器 [{Name}] 缺少运行图上下文。");

        var members = runtime.Nodes
            .Where(node => node.ParentExecutionContainerId == Id)
            .ToList();
        var lanes = new List<ParallelFlowLane<NodeModel>>(Lanes.Count);
        foreach (var lane in Lanes)
        {
            var scope = ExecutionGraphScope.Create(members, runtime.Connections, Id, lane.Id);
            if (scope.Nodes.Count == 0)
                throw new InvalidOperationException(
                    $"并行容器 [{Name}] 的泳道 [{lane.Name}] 没有节点。");
            lanes.Add(new ParallelFlowLane<NodeModel>(lane.Id, lane.Name, scope.Nodes, scope.Connections));
        }

        await ParallelFlowRunner.ExecuteAsync(
            lanes, ct, message => Console.WriteLine($"[{Name}] {message}"));
    }

    /// <summary>从指定泳道成员开始重新执行该成员及其可达后继，不重复已完成的兄弟泳道。</summary>
    public async Task ResumeFromNodeAsync(string startNodeId, CancellationToken ct, Action<string> log)
    {
        var runtime = ExecutionScopeRuntime
            ?? throw new InvalidOperationException($"并行容器 [{Name}] 缺少运行图上下文。");
        var start = runtime.Nodes.FirstOrDefault(node => node.Id == startNodeId &&
                                                        node.ParentExecutionContainerId == Id)
            ?? throw new ArgumentException("恢复节点不属于该并行容器。", nameof(startNodeId));
        var lane = Lanes.FirstOrDefault(item => item.Id == start.ExecutionLaneId)
            ?? throw new InvalidOperationException($"恢复节点 [{start.Name}] 所在泳道不存在。");
        var scope = ExecutionGraphScope.Create(runtime.Nodes, runtime.Connections, Id, lane.Id);
        var recovery = ExecutionGraphScope.CreateReachable(scope.Nodes, scope.Connections, startNodeId);

        log($"[恢复] 并行分支 [{lane.Name}] 从节点 [{start.Name}] 继续");
        await new FlowChartExecutionEngine().ExecuteAsync(
            recovery.Nodes, recovery.Connections, ct, log);
    }

    private void ResizeLanes(int count)
    {
        if (count == Lanes.Count) return;
        if (count < Lanes.Count && Service != null)
        {
            var removedIds = Lanes.Skip(count).Select(lane => lane.Id).ToHashSet();
            if (Service.Nodes.Any(node => node.Model.ParentExecutionContainerId == Id &&
                                          node.Model.ExecutionLaneId != null &&
                                          removedIds.Contains(node.Model.ExecutionLaneId)))
            {
                Service.Model.StatusText = "请先移出要删除泳道中的节点";
                return;
            }
        }

        var resized = Lanes.Take(count).ToList();
        while (resized.Count < count)
            resized.Add(NewLane(resized.Count + 1));
        Lanes = resized;
    }

    private static List<ExecutionLaneDefinition> CreateDefaultLanes() => [NewLane(1), NewLane(2)];
    private static ExecutionLaneDefinition NewLane(int index) => new() { Name = $"分支{index}" };
}
