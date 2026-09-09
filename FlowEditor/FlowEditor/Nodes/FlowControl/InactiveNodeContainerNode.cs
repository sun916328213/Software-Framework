using FlowEditor.Core;

namespace FlowEditor.Nodes.FlowControl;

/// <summary>画布上的不执行节点容器；容器及其内部节点不会进入执行子图。</summary>
[FlowTool("不执行容器", "ArchiveOffOutline", ToolGroup = "流程控制", SortIndex = 90,
    Description = "把暂不参与流程的节点拖入此容器，保留配置但跳过校验与执行")]
public sealed class InactiveNodeContainerNode : NodeModel
{
    public const double DefaultWidth = 420;
    public const double DefaultHeight = 520;

    private double _containerWidth = DefaultWidth;
    public double ContainerWidth
    {
        get => _containerWidth;
        set => SetProperty(ref _containerWidth, Math.Clamp(value, 260, 2400));
    }

    private double _containerHeight = DefaultHeight;
    public double ContainerHeight
    {
        get => _containerHeight;
        set => SetProperty(ref _containerHeight, Math.Clamp(value, 180, 1800));
    }

    public InactiveNodeContainerNode()
    {
        Name = "不执行容器";
        IsInEnabled = false;
        IsOutEnabled = false;
        IsExcludedFromExecution = true;
    }

    public override Task ExecuteAsync(CancellationToken ct) => Task.CompletedTask;
}
