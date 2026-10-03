// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：ParallelForkNode。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、FlowEditor.Controls.Nodes、FlowEditor.Core.Nodes、FlowEditor.Controls.Core、FlowEditor.Core、FlowEditor.Runtime。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using FlowEditor.Controls.Nodes;
using FlowEditor.Core.Nodes;
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;
using FlowEditor.Controls.Models;
using FlowEditor.Core.Models;
using WPFCustomControls.PropertyGrid.Core.Models;

namespace FlowEditor.Controls.Nodes.FlowControl;

/// <summary>
/// 显式并行分叉。进入一次会同时激活所有分支输出；每个输出端口只允许一条控制流连线，
/// 各分支必须在下游汇入同一个 AND 分支汇聚节点。
/// </summary>
[FlowTool("并行分叉", "SourceBranch", ToolGroup = "流程控制", SortIndex = 4,
    Description = "同时启动全部分支；必须在下游使用 AND 分支汇聚收束")]
public sealed class ParallelForkNode : NodeModel, IFlowStructureValidationSource, IParallelForkNode
{
    private int _branchCount = 2;

    /// <summary>进入节点时同时激活的并行分支数量。</summary>
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

    /// <summary>初始化至少包含两个控制流输出的并行分叉节点。</summary>
    public ParallelForkNode()
    {
        Name = "并行分叉";
        IsInEnabled = true;
        IsOutEnabled = false;
        EnsureOutputs(_branchCount);
    }

    /// <inheritdoc />
    public override IReadOnlyList<string>? ControlFlowPorts =>
        CustomOutputPorts.Where(port => port.DataType == PortType.Flow)
            .Select(port => port.Name)
            .ToList();

    /// <summary>完成分叉节点本身的空操作，实际并行调度由执行引擎处理。</summary>
    public override Task ExecuteAsync(CancellationToken ct) => Task.CompletedTask;

    /// <summary>验证并行分叉至少保留两个控制流输出。</summary>
    public IEnumerable<FlowDiagnostic> ValidateStructure()
    {
        if (CustomOutputPorts.Count(port => port.DataType == PortType.Flow) < 2)
            yield return new FlowDiagnostic(
                FlowDiagnosticSeverity.Error, "E-PAR-BRANCH-COUNT",
                $"并行分叉 [{Name}] 至少需要两个分支输出");
    }

    /// <summary>在上限内增加一个并行输出分支。</summary>
    [NodeAction("添加并行分支", Order = 0, IconResource = "Plus")]
    public void AddBranch()
    {
        if (BranchCount < 12) BranchCount++;
    }

    /// <summary>删除最后一个未接线分支，但始终保留至少两个分支。</summary>
    [NodeAction("删除末分支", Order = 1, IconResource = "Minus")]
    public void RemoveLastBranch()
    {
        if (BranchCount > 2) BranchCount--;
    }

    /// <summary>确保从分支一到指定数量的控制流输出均存在。</summary>
    private void EnsureOutputs(int count)
    {
        for (var index = 1; index <= count; index++)
        {
            var name = $"分支{index}";
            var port = CustomOutputPorts.FirstOrDefault(candidate => candidate.Name == name)
                       ?? AddCustomOutputPort(name);
            port.DataType = PortType.Flow;
        }
    }

    /// <summary>删除超过保留数量的尾部并行输出。</summary>
    private void PruneOutputs(int keepCount)
    {
        for (var index = 12; index > keepCount; index--)
        {
            var port = CustomOutputPorts.FirstOrDefault(candidate => candidate.Name == $"分支{index}");
            if (port != null) RemoveCustomPort(port.Id);
        }
    }

    /// <summary>判断待裁剪的尾部输出中是否仍存在连线。</summary>
    private bool HasWiredOutputAbove(int keepCount)
    {
        if (Service == null) return false;
        return CustomOutputPorts.Any(port =>
            TryGetBranchIndex(port.Name, out var index) && index > keepCount &&
            Service.Connections.Any(connection => connection.SourcePortId == port.Id));
    }

    /// <summary>从“分支N”端口名解析一基分支序号。</summary>
    private static bool TryGetBranchIndex(string name, out int index)
    {
        index = 0;
        return name.StartsWith("分支", StringComparison.Ordinal) &&
               int.TryParse(name.AsSpan(2), out index);
    }
}
