// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：DelayNode。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、FlowEditor.Controls.Nodes、FlowEditor.Core.Nodes、WPFCustomControls.PropertyGrid、FlowEditor.Controls.Core、FlowEditor.Core。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using FlowEditor.Controls.Nodes;
using FlowEditor.Core.Nodes;
using WPFCustomControls.PropertyGrid;
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;
using FlowEditor.Controls.Models;
using FlowEditor.Core.Models;
using WPFCustomControls.PropertyGrid.Core.Models;

namespace FlowEditor.Controls.Nodes.FlowControl;

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
    /// <remarks>
    /// 执行当前入口的处理流程；对应入口为 ExecuteAsync。
    /// 正文中的关键协作调用包括 Delay；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </remarks>
    public override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Delay(DelayMs, ct);
    }
}
