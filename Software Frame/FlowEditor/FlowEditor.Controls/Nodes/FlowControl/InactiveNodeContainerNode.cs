// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：InactiveNodeContainerNode。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、FlowEditor.Controls.Nodes、FlowEditor.Core.Nodes、FlowEditor.Controls.Core、FlowEditor.Core、FlowEditor.Runtime。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using FlowEditor.Controls.Nodes;
using FlowEditor.Core.Nodes;
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;

namespace FlowEditor.Controls.Nodes.FlowControl;

/// <summary>画布上的不执行节点容器；容器及其内部节点不会进入执行子图。</summary>
[FlowTool("不执行容器", "ArchiveOffOutline", ToolGroup = "流程控制", SortIndex = 90,
    Description = "把暂不参与流程的节点拖入此容器，保留配置但跳过校验与执行")]
public sealed class InactiveNodeContainerNode : NodeModel
{
    /// <summary>新容器的默认宽度。</summary>
    public const double DefaultWidth = 420;
    /// <summary>新容器的默认高度。</summary>
    public const double DefaultHeight = 520;

    private double _containerWidth = DefaultWidth;
    /// <summary>不执行容器在画布上的宽度。</summary>
    public double ContainerWidth
    {
        get => _containerWidth;
        set => SetProperty(ref _containerWidth, Math.Clamp(value, 260, 2400));
    }

    private double _containerHeight = DefaultHeight;
    /// <summary>不执行容器在画布上的高度。</summary>
    public double ContainerHeight
    {
        get => _containerHeight;
        set => SetProperty(ref _containerHeight, Math.Clamp(value, 180, 1800));
    }

    /// <summary>创建不参与校验和执行的画布容器。</summary>
    public InactiveNodeContainerNode()
    {
        Name = "不执行容器";
        IsInEnabled = false;
        IsOutEnabled = false;
        IsExcludedFromExecution = true;
    }

    /// <inheritdoc />
    /// <remarks>
    /// 执行当前入口的处理流程；对应入口为 ExecuteAsync。
    /// </remarks>
    public override Task ExecuteAsync(CancellationToken ct) => Task.CompletedTask;
}
