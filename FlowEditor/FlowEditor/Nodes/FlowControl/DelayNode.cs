using WPFCustomControls.PropertyGrid;
using FlowEditor.Core;
using FlowEditor.Models;
using WPFCustomControls.PropertyGrid.Models;

namespace FlowEditor.Nodes.FlowControl;

/// <summary>
/// 延时节点：等待指定毫秒数后继续执行。
/// </summary>
[FlowTool("延时", "TimerOutline", ToolGroup = "流程控制", SortIndex = 3, Description = "等待指定毫秒数后继续执行")]
public class DelayNode : NodeModel
{
    private int _delayMs = 1000;
    /// <summary>延时毫秒数。</summary>
    [NodeProperty(DisplayName = "延时(ms)", Group = "1.基础信息", Order = 2, Description = "等待的毫秒数")]
    public int DelayMs
    {
        get => _delayMs;
        set => SetProperty(ref _delayMs, value);
    }

    /// <summary>创建默认延时一秒的流程节点。</summary>
    public DelayNode()
    {
        Name = "延时";
        IsInEnabled = true;
        IsOutEnabled = true;
    }

    /// <inheritdoc />
    public override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Delay(DelayMs, ct);
    }
}
