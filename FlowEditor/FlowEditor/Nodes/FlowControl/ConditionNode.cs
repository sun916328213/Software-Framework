using FlowEditor.Core;
using FlowEditor.Models;
using WPFCustomControls.PropertyGrid.Models;

namespace FlowEditor.Nodes.FlowControl;

/// <summary>
/// 条件分支节点：根据条件表达式决定执行路径。
/// 激活边语义：执行后只激活 True/False 中被选中的端口，
/// 引擎只调度选中分支的下游，未选中分支整体跳过。
/// 旧图兼容：激活端口无任何出线时（旧图仅用 Next_Out），由引擎回退为全放行。
/// 如需 N 路分发（输入 1 走 OUT1、输入 2 走 OUT2），请使用「条件分发」节点。
/// </summary>
[FlowTool("条件分支", "SourceBranch", ToolGroup = "流程控制", SortIndex = 2,
    Description = "旧版二选一节点，请改用条件分发", VisibleInToolbox = false)]
public class ConditionNode : NodeModel
{
    private bool _condition = true;
    /// <summary>条件为 True 时走 True 分支，否则走 False 分支；可由上游布尔输出绑定。</summary>
    [NodeProperty(DisplayName = "条件", Group = "1.基础信息", Order = 1, IsInput = true,
        Description = "为 True 走 True 分支，否则走 False 分支；可由上游布尔输出绑定")]
    public bool Condition
    {
        get => _condition;
        set => SetProperty(ref _condition, value);
    }

    /// <summary>创建具有 True、False 控制流端口的二选一条件节点。</summary>
    public ConditionNode()
    {
        Name = "条件分支";
        IsInEnabled = true;
        IsOutEnabled = true;
        // 添加 True/False 输出端口
        AddCustomOutputPort("True", nameof(Condition)).DataType = PortType.Flow;
        AddCustomOutputPort("False").DataType = PortType.Flow;
        // 条件允许由上游绑定（端口名须为属性名，SyncInputValues 按名写回属性）；
        // 老图反序列化时无此端口文档，不影响还原，未连线时面板常量生效
        AddCustomInputPort("条件", nameof(Condition));
    }

    /// <inheritdoc />
    public override Task ExecuteAsync(CancellationToken ct)
    {
        ActivateOutput(Condition ? "True" : "False");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override IReadOnlyList<string>? ControlFlowPorts => new[] { "True", "False" };
}
