using FlowEditor.Core;

namespace FlowEditor.Nodes.FlowControl;

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
    public override Task ExecuteAsync(CancellationToken ct) => Task.CompletedTask;
}
