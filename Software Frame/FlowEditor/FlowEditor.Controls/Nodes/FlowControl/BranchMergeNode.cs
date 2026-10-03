// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：BranchMergeNode、BranchMergeOperator。成员说明可结合下方具体实现阅读。
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
/// 分支汇聚节点：用于承接二选一、三选一等条件分支，也可汇聚普通并行分支。
/// 调度语义由执行引擎统一提供：OR 任一路径完成即继续；AND 要求全部已连接输入都实际完成。
/// </summary>
[FlowTool("分支汇聚", "SourceMerge", ToolGroup = "流程控制", SortIndex = 5,
    Description = "汇聚二选一、三选一或并行分支，可选择 OR 任一完成或 AND 全部完成")]
public class BranchMergeNode : NodeModel
{
    private BranchMergeOperator _mergeOperator = BranchMergeOperator.OR;

    /// <summary>决定任一有效分支或全部有效分支完成后继续。</summary>
    [NodeProperty(DisplayName = "汇聚方式", Group = "1.基础信息", Order = 0,
        PortConfigurable = false,
        Description = "OR：任一分支完成即继续；AND：全部已连接分支都完成才继续")]
    public BranchMergeOperator MergeOperator
    {
        get => _mergeOperator;
        set => SetProperty(ref _mergeOperator, value);
    }

    /// <inheritdoc />
    public override FlowJoinMode FlowJoinMode
        => MergeOperator == BranchMergeOperator.AND
            ? FlowJoinMode.AllActive
            : FlowJoinMode.AnyActive;

    private int _inputCount = 2;

    /// <summary>画布中显示的汇聚输入端口数量。</summary>
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

    /// <summary>初始化至少包含两个控制流输入的分支汇聚节点。</summary>
    public BranchMergeNode()
    {
        Name = "分支汇聚";
        IsInEnabled = false;
        IsOutEnabled = true;
        EnsureInputs(_inputCount);
    }

    /// <summary>完成汇聚节点本身的空操作，实际汇聚条件由执行引擎处理。</summary>
    public override Task ExecuteAsync(CancellationToken ct) => Task.CompletedTask;

    /// <summary>在上限内增加一个汇聚输入。</summary>
    [NodeAction("添加汇聚输入", Order = 0, IconResource = "Plus")]
    public void AddInput()
    {
        if (InputCount < 12) InputCount++;
    }

    /// <summary>删除最后一个未接线输入，但始终保留至少两个输入。</summary>
    [NodeAction("删除末输入", Order = 1, IconResource = "Minus")]
    public void RemoveLastInput()
    {
        if (InputCount > 2) InputCount--;
    }

    /// <summary>确保从分支一到指定数量的控制流输入均存在。</summary>
    private void EnsureInputs(int count)
    {
        for (var index = 1; index <= count; index++)
        {
            var name = $"分支{index}";
            var port = CustomInputPorts.FirstOrDefault(candidate => candidate.Name == name)
                       ?? AddCustomInputPort(name);
            port.DataType = PortType.Flow;
        }
    }

    /// <summary>删除超过保留数量的尾部汇聚输入。</summary>
    private void PruneInputs(int keepCount)
    {
        for (var index = 12; index > keepCount; index--)
        {
            var port = CustomInputPorts.FirstOrDefault(candidate => candidate.Name == $"分支{index}");
            if (port != null) RemoveCustomPort(port.Id);
        }
    }

    /// <summary>判断待裁剪的尾部输入中是否仍存在连线。</summary>
    private bool HasWiredInputAbove(int keepCount)
    {
        if (Service == null) return false;
        return CustomInputPorts.Any(port =>
            TryGetInputIndex(port.Name, out var index) && index > keepCount &&
            Service.Connections.Any(connection => connection.TargetPortId == port.Id));
    }

    /// <summary>从“分支N”端口名解析一基输入序号。</summary>
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
    /// <summary>任一活动分支完成即可继续。</summary>
    OR,
    /// <summary>全部活动分支完成后继续。</summary>
    AND
}
