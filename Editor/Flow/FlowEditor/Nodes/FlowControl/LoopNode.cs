using FlowEditor.Core;
using FlowEditor.Models;

namespace FlowEditor.Nodes.FlowControl;

/// <summary>
/// 循环节点：与回边配合实现结构化计数循环。
/// 用法：Body → 循环体 → 回边接回本节点；Completed → 循环结束后的下游。
/// 每次被调度（含回边重入）CurrentIndex +1：未达 Iterations 时激活 Body，
/// 达到后激活 Completed 并自动复位计数，环路自然终止。
/// 旧图兼容：Body/Completed 无出线时（旧图仅用 Next_Out 直通），由引擎回退为全放行。
/// </summary>
[FlowTool("循环", "Sync", ToolGroup = "流程控制", SortIndex = 4, Description = "循环执行 Body 分支指定次数后走 Completed")]
public class LoopNode : NodeModel
{
    private int _iterations = 5;
    public int Iterations
    {
        get => _iterations;
        set => SetProperty(ref _iterations, value);
    }

    private int _currentIndex;
    public int CurrentIndex
    {
        get => _currentIndex;
        set => SetProperty(ref _currentIndex, value);
    }

    public LoopNode()
    {
        Name = "循环";
        IsInEnabled = true;
        // 新节点默认不再生成通用 Out 端口，以 Body/Completed 表达循环走向
        IsOutEnabled = false;
        AddCustomOutputPort("Body");
        AddCustomOutputPort("Completed");
    }

    public override Task ExecuteAsync(CancellationToken ct)
    {
        CurrentIndex++;
        if (CurrentIndex <= Math.Max(1, Iterations))
        {
            ActivateOutput("Body");
        }
        else
        {
            ActivateOutput("Completed");
            CurrentIndex = 0; // 本轮循环结束，复位供下一次外部触发
        }
        return Task.CompletedTask;
    }

    public override IReadOnlyList<string>? ControlFlowPorts => new[] { "Body", "Completed" };

    [NodeAction("重置计数", Order = 0, IconResource = "Restart")]
    public void ResetCounter()
    {
        CurrentIndex = 0;
    }

    [NodeAction("预览参数", Order = 1, IconResource = "EyeOutline")]
    public void PreviewParams()
    {
        System.Windows.MessageBox.Show(
            $"循环次数: {Iterations}\n当前索引: {CurrentIndex}",
            "循环参数预览",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
    }
}
