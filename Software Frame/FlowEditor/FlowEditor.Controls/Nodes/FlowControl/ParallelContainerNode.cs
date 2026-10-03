// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：ParallelContainerNode。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、FlowEditor.Controls.Nodes、FlowEditor.Core.Nodes、System.Text.Json.Serialization、FlowEditor.Controls.Core、FlowEditor.Core。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using FlowEditor.Controls.Nodes;
using FlowEditor.Core.Nodes;
using System.Text.Json.Serialization;
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;
using WPFCustomControls.PropertyGrid.Core.Models;

namespace FlowEditor.Controls.Nodes.FlowControl;

/// <summary>主画布结构化并行容器；所有泳道同时启动，全部完成后才继续。</summary>
// 兼容已经保存的早期流程；新流程从工具箱使用 ParallelForkNode + AND BranchMergeNode。
public sealed class ParallelContainerNode : NodeModel, IExecutionScopeContainer, IFlowStructureValidationSource
{
    /// <summary>新容器的默认宽度。</summary>
    public const double DefaultWidth = 720;
    /// <summary>新容器的默认高度。</summary>
    public const double DefaultHeight = 420;

    private double _containerWidth = DefaultWidth;
    private double _containerHeight = DefaultHeight;
    private List<ExecutionLaneDefinition> _lanes = CreateDefaultLanes();

    /// <inheritdoc />
    [NodeProperty(DisplayName = "容器宽度", Group = "0.容器", Order = 0,
        IsDisplay = false, PortConfigurable = false)]
    public double ContainerWidth
    {
        get => _containerWidth;
        set => SetProperty(ref _containerWidth, Math.Clamp(value, 420, 3200));
    }

    /// <inheritdoc />
    [NodeProperty(DisplayName = "容器高度", Group = "0.容器", Order = 1,
        IsDisplay = false, PortConfigurable = false)]
    public double ContainerHeight
    {
        get => _containerHeight;
        set => SetProperty(ref _containerHeight, Math.Clamp(value, 260, 2400));
    }

    /// <summary>并行执行使用的持久化泳道定义。</summary>
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

    /// <summary>并行容器当前的泳道数量。</summary>
    [JsonIgnore]
    [NodeProperty(DisplayName = "分支数量", Group = "1.基础信息", Order = 0,
        Min = 2, Max = 12, Step = 1, PortConfigurable = false,
        Description = "纵向泳道数量；所有泳道同时启动并自动汇聚")]
    public int BranchCount
    {
        get => Lanes.Count;
        set => ResizeLanes(Math.Clamp(value, 2, 12));
    }

    /// <inheritdoc />
    public ExecutionScopeKind ScopeKind => ExecutionScopeKind.Parallel;
    /// <inheritdoc />
    public bool ShowBoundaryPorts => true;
    /// <inheritdoc />
    public IReadOnlyList<ExecutionLaneDefinition> ExecutionLanes => Lanes;

    /// <summary>创建带两个默认泳道的结构化并行容器。</summary>
    public ParallelContainerNode()
    {
        Name = "并行容器";
        IsInEnabled = true;
        IsOutEnabled = true;
    }

    /// <inheritdoc />
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 ValidateStructure。
    /// 正文中的关键协作调用包括 Where、GroupBy、Count；具体条件和先后次序见方法体。
    /// </remarks>
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

    /// <inheritdoc />
    /// <remarks>
    /// 执行当前入口的处理流程；对应入口为 ExecuteAsync。
    /// 正文中的关键协作调用包括 ToList、Where、Create、Add、ExecuteAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </remarks>
    public override async Task ExecuteAsync(CancellationToken ct)
    {
        var runtime = ExecutionScopeRuntime ?? (Service == null
            ? null
            : new ExecutionScopeRuntime(
                Service.Nodes.ToList(),
                Service.Connections.ToList(),
                new FlowChartExecutionEngine()));
        if (runtime == null)
            throw new InvalidOperationException($"并行容器 [{Name}] 缺少运行图上下文。");

        var members = runtime.Nodes
            .Where(node => node.ParentExecutionContainerId == Id)
            .ToList();
        var lanes = new List<ParallelFlowLane<INodeExecutable>>(Lanes.Count);
        foreach (var lane in Lanes)
        {
            var scope = ExecutionGraphScope.Create(members, runtime.Connections, Id, lane.Id);
            if (scope.Nodes.Count == 0)
                throw new InvalidOperationException(
                    $"并行容器 [{Name}] 的泳道 [{lane.Name}] 没有节点。");
            lanes.Add(new ParallelFlowLane<INodeExecutable>(lane.Id, lane.Name, scope.Nodes, scope.Connections));
        }

        await ParallelFlowRunner.ExecuteAsync(
            lanes,
            ct,
            message => Console.WriteLine($"[{Name}] {message}"),
            runtime.Engine);
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
        await (runtime.Engine?.CreateIsolated() ?? new FlowChartExecutionEngine()).ExecuteAsync(
            recovery.Nodes, recovery.Connections, ct, log);
    }

    /// <summary>在不删除仍含节点的泳道前提下调整泳道数量。</summary>
    /// <param name="count">目标泳道数量。</param>
    private void ResizeLanes(int count)
    {
        if (count == Lanes.Count) return;
        if (count < Lanes.Count && Service != null)
        {
            var removedIds = Lanes.Skip(count).Select(lane => lane.Id).ToHashSet();
            if (Service.Nodes.Any(node => node.ParentExecutionContainerId == Id &&
                                          node.ExecutionLaneId != null &&
                                          removedIds.Contains(node.ExecutionLaneId)))
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

    /// <summary>创建新并行容器的两个默认泳道。</summary>
    private static List<ExecutionLaneDefinition> CreateDefaultLanes() => [NewLane(1), NewLane(2)];

    /// <summary>创建指定序号的泳道定义。</summary>
    /// <param name="index">用于生成默认名称的一基序号。</param>
    private static ExecutionLaneDefinition NewLane(int index) => new() { Name = $"分支{index}" };
}
