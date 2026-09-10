using FlowEditor.Core;
using FlowEditor.Models;
using WPFCustomControls.PropertyGrid.Models;

namespace FlowEditor.Nodes.FlowControl;

/// <summary>
/// 显式并行分叉。进入一次会同时激活所有分支输出；每个输出端口只允许一条控制流连线，
/// 各分支必须在下游汇入同一个 AND 分支汇聚节点。
/// </summary>
[FlowTool("并行分叉", "SourceBranch", ToolGroup = "流程控制", SortIndex = 4,
    Description = "同时启动全部分支；必须在下游使用 AND 分支汇聚收束")]
public sealed class ParallelForkNode : NodeModel, IFlowStructureValidationSource
{
    private int _branchCount = 2;

    [NodeProperty(DisplayName = "并行分支数量", Group = "1.基础信息", Order = 0,
        Min = 2, Max = 12, Step = 1, PortConfigurable = false,
        Description = "同时启动的分支数量；每个分支输出只能连接一个下游输入")]
    public int BranchCount
    {
        get => _branchCount;
        set
        {
            var clamped = Math.Clamp(value, 2, 12);
            if (clamped < _branchCount && HasWiredOutputAbove(clamped))
            {
                if (Service != null)
                    Service.Model.StatusText = "请先断开要删除的并行分支输出连线";
                return;
            }

            if (!SetProperty(ref _branchCount, clamped)) return;
            EnsureOutputs(clamped);
            PruneOutputs(clamped);
        }
    }

    public ParallelForkNode()
    {
        Name = "并行分叉";
        IsInEnabled = true;
        IsOutEnabled = false;
        EnsureOutputs(_branchCount);
    }

    public override IReadOnlyList<string>? ControlFlowPorts =>
        CustomOutputPorts.Where(port => port.DataType == PortType.Flow)
            .Select(port => port.Name)
            .ToList();

    public override Task ExecuteAsync(CancellationToken ct) => Task.CompletedTask;

    public IEnumerable<FlowDiagnostic> ValidateStructure()
    {
        if (CustomOutputPorts.Count(port => port.DataType == PortType.Flow) < 2)
            yield return new FlowDiagnostic(
                FlowDiagnosticSeverity.Error, "E-PAR-BRANCH-COUNT",
                $"并行分叉 [{Name}] 至少需要两个分支输出");
    }

    [NodeAction("添加并行分支", Order = 0, IconResource = "Plus")]
    public void AddBranch()
    {
        if (BranchCount < 12) BranchCount++;
    }

    [NodeAction("删除末分支", Order = 1, IconResource = "Minus")]
    public void RemoveLastBranch()
    {
        if (BranchCount > 2) BranchCount--;
    }

    private void EnsureOutputs(int count)
    {
        for (var index = 1; index <= count; index++)
        {
            var name = $"分支{index}";
            var port = CustomOutputPorts.FirstOrDefault(candidate => candidate.Name == name)
                       ?? AddCustomOutputPort(name);
            port.DataType = PortType.Flow;
        }
        InvalidatePorts();
    }

    private void PruneOutputs(int keepCount)
    {
        for (var index = 12; index > keepCount; index--)
        {
            var port = CustomOutputPorts.FirstOrDefault(candidate => candidate.Name == $"分支{index}");
            if (port != null) RemoveCustomPort(port.Id);
        }
        InvalidatePorts();
    }

    private bool HasWiredOutputAbove(int keepCount)
    {
        if (Service == null) return false;
        return CustomOutputPorts.Any(port =>
            TryGetBranchIndex(port.Name, out var index) && index > keepCount &&
            Service.Connections.Any(connection => connection.SourcePortId == port.Id));
    }

    private static bool TryGetBranchIndex(string name, out int index)
    {
        index = 0;
        return name.StartsWith("分支", StringComparison.Ordinal) &&
               int.TryParse(name.AsSpan(2), out index);
    }
}
