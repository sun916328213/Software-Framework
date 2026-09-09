using FlowEditor.Core;
using FlowEditor.Models;
using WPFCustomControls.PropertyGrid.Models;

namespace FlowEditor.Nodes.FlowControl;

/// <summary>
/// 分支汇聚节点：用于承接二选一、三选一等条件分支，也可汇聚普通并行分支。
/// 调度语义由执行引擎统一提供：OR 任一路径完成即继续；AND 要求全部已连接输入都实际完成。
/// </summary>
[FlowTool("分支汇聚", "SourceMerge", ToolGroup = "流程控制", SortIndex = 4,
    Description = "汇聚二选一、三选一或并行分支，可选择 OR 任一完成或 AND 全部完成")]
public class BranchMergeNode : NodeModel
{
    private BranchMergeOperator _mergeOperator = BranchMergeOperator.OR;

    [NodeProperty(DisplayName = "汇聚方式", Group = "1.基础信息", Order = 0,
        PortConfigurable = false,
        Description = "OR：任一分支完成即继续；AND：全部已连接分支都完成才继续")]
    public BranchMergeOperator MergeOperator
    {
        get => _mergeOperator;
        set => SetProperty(ref _mergeOperator, value);
    }

    public override FlowJoinMode FlowJoinMode
        => MergeOperator == BranchMergeOperator.AND
            ? FlowJoinMode.AllActive
            : FlowJoinMode.AnyActive;

    private int _inputCount = 2;

    [NodeProperty(DisplayName = "汇聚输入数量", Group = "1.基础信息", Order = 1,
        Min = 2, Max = 12, Step = 1, PortConfigurable = false,
        Description = "需要显示的分支输入端口数量，支持二选一到十二选一")]
    public int InputCount
    {
        get => _inputCount;
        set
        {
            var clamped = Math.Clamp(value, 2, 12);
            if (clamped < _inputCount && HasWiredInputAbove(clamped))
            {
                if (Service != null)
                    Service.Model.StatusText = "请先断开要删除的汇聚输入端口连线";
                return;
            }

            if (!SetProperty(ref _inputCount, clamped)) return;
            EnsureInputs(clamped);
            PruneInputs(clamped);
        }
    }

    public BranchMergeNode()
    {
        Name = "分支汇聚";
        IsInEnabled = false;
        IsOutEnabled = true;
        EnsureInputs(_inputCount);
    }

    public override Task ExecuteAsync(CancellationToken ct) => Task.CompletedTask;

    [NodeAction("添加汇聚输入", Order = 0, IconResource = "Plus")]
    public void AddInput()
    {
        if (InputCount < 12) InputCount++;
    }

    [NodeAction("删除末输入", Order = 1, IconResource = "Minus")]
    public void RemoveLastInput()
    {
        if (InputCount > 2) InputCount--;
    }

    private void EnsureInputs(int count)
    {
        for (var index = 1; index <= count; index++)
        {
            var name = $"分支{index}";
            var port = CustomInputPorts.FirstOrDefault(candidate => candidate.Name == name)
                       ?? AddCustomInputPort(name);
            port.DataType = PortType.Flow;
        }
        InvalidatePorts();
    }

    private void PruneInputs(int keepCount)
    {
        for (var index = 12; index > keepCount; index--)
        {
            var port = CustomInputPorts.FirstOrDefault(candidate => candidate.Name == $"分支{index}");
            if (port != null) RemoveCustomPort(port.Id);
        }
        InvalidatePorts();
    }

    private bool HasWiredInputAbove(int keepCount)
    {
        if (Service == null) return false;
        return CustomInputPorts.Any(port =>
            TryGetInputIndex(port.Name, out var index) && index > keepCount &&
            Service.Connections.Any(connection => connection.TargetPortId == port.Id));
    }

    private static bool TryGetInputIndex(string name, out int index)
    {
        index = 0;
        return name.StartsWith("分支", StringComparison.Ordinal) &&
               int.TryParse(name.AsSpan(2), out index);
    }
}

/// <summary>分支汇聚逻辑运算符。</summary>
public enum BranchMergeOperator
{
    OR,
    AND
}
