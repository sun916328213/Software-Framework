using FlowEditor.Core;
using FlowEditor.Models;

namespace FlowEditor.Nodes.FlowControl;

/// <summary>
/// 条件分支节点：根据条件表达式决定执行路径。
/// 激活边语义：执行后只激活 True/False 中被选中的端口，
/// 引擎只调度选中分支的下游，未选中分支整体跳过。
/// 旧图兼容：激活端口无任何出线时（旧图仅用 Next_Out），由引擎回退为全放行。
/// </summary>
[FlowTool("条件分支", "SourceBranch", ToolGroup = "流程控制", SortIndex = 2, Description = "根据条件表达式决定执行路径")]
public class ConditionNode : NodeModel
{
    private bool _condition = true;
    public bool Condition
    {
        get => _condition;
        set => SetProperty(ref _condition, value);
    }

    public ConditionNode()
    {
        Name = "条件分支";
        IsInEnabled = true;
        IsOutEnabled = true;
        // 添加 True/False 输出端口
        AddCustomOutputPort("True", nameof(Condition));
        AddCustomOutputPort("False");
    }

    public override Task ExecuteAsync(CancellationToken ct)
    {
        ActivateOutput(Condition ? "True" : "False");
        return Task.CompletedTask;
    }

    public override IReadOnlyList<string>? ControlFlowPorts => new[] { "True", "False" };
}
