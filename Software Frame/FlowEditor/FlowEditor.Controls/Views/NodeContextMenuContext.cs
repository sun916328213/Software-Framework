// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：NodeContextMenuContext。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、System.Windows.Input、FlowEditor.Controls.Nodes.FlowControl、FlowEditor.Controls.ViewModels。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using System.Windows.Input;
using FlowEditor.Controls.Nodes.FlowControl;
using FlowEditor.Controls.ViewModels;

namespace FlowEditor.Controls.Views;

/// <summary>向节点右键菜单公开当前节点及其可执行命令所需的最小只读上下文。</summary>
internal sealed class NodeContextMenuContext
{
    /// <summary>初始化节点菜单上下文。</summary>
    /// <param name="flowChart">当前流程视图模型。</param>
    /// <param name="node">被点击的节点。</param>
    public NodeContextMenuContext(FlowChartViewModel flowChart, NodeViewModel node)
    {
        FlowChart = flowChart;
        Node = node;
    }

    /// <summary>获取当前流程视图模型。</summary>
    public FlowChartViewModel FlowChart { get; }

    /// <summary>获取被点击的节点。</summary>
    public NodeViewModel Node { get; }

    /// <summary>获取节点是否允许编辑端口、复制和删除。</summary>
    public bool IsEditable => Node.IsEditable;

    /// <summary>获取节点是否允许在执行画布与不执行容器之间移动。</summary>
    public bool CanMoveToInactiveContainer => Node.Model is not InactiveNodeContainerNode;

    /// <summary>获取移动菜单项应显示的文本。</summary>
    public string MoveHeader => Node.IsExcludedFromExecution ? "移回执行画布" : "移入不执行节点容器";

    /// <summary>获取与当前节点位置匹配的移动命令。</summary>
    public ICommand MoveCommand => Node.IsExcludedFromExecution
        ? FlowChart.RestoreFromInactiveContainerCommand
        : FlowChart.MoveToInactiveContainerCommand;
}
