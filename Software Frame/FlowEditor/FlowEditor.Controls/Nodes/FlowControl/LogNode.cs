// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：LogNode。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、FlowEditor.Controls.Nodes、FlowEditor.Core.Nodes、FlowEditor.Controls.Core、FlowEditor.Core、FlowEditor.Runtime。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using FlowEditor.Controls.Nodes;
using FlowEditor.Core.Nodes;
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;
using WPFCustomControls.PropertyGrid.Core.Models;

namespace FlowEditor.Controls.Nodes.FlowControl;

/// <summary>
/// 日志节点：输出日志信息到输出面板与流程日志文件。
/// 写入流程日志文件经 FlowLogContext.Write（带 #运行序号 [节点名] 前缀与所选日志等级），
/// 落盘由宿主通过 IoC 注入 <see cref="IFlowLogSink"/>，FlowEditor 不依赖具体日志库。
/// </summary>
[FlowTool("日志输出", "TextBoxOutline", ToolGroup = "流程控制", SortIndex = 5, Description = "输出日志信息到控制台与流程日志文件")]
public class LogNode : NodeModel
{
    private string _message = "Hello World";
    /// <summary>写入输出面板和日志文件的消息正文。</summary>
    [NodeProperty(DisplayName = "信息", Group = "1.基础信息", Order = 1)]
    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    private FlowLogLevel _level = FlowLogLevel.Information;
    /// <summary>写入流程日志文件时使用的日志等级。</summary>
    [NodeProperty(DisplayName = "信息等级", Group = "1.基础信息", Order = 2)]
    public FlowLogLevel Level
    {
        get => _level;
        set => SetProperty(ref _level, value);
    }

    /// <summary>创建默认输出信息级日志的节点。</summary>
    public LogNode()
    {
        Name = "日志输出";
    }

    /// <inheritdoc />
    /// <remarks>
    /// 执行当前入口的处理流程；对应入口为 ExecuteAsync。
    /// 正文中的关键协作调用包括 WriteLine、SuppressSink、Write；具体条件和先后次序见方法体。
    /// </remarks>
    public override Task ExecuteAsync(CancellationToken ct)
    {
        // 信息级：Console 捕获路径自动完成「面板 + 文件（信息级）」，一句即可
        if (Level == FlowLogLevel.Information)
        {
            Console.WriteLine(Message);
            return Task.CompletedTask;
        }

        // 非信息级：面板（抑制落盘避免重复）→ 文件（带 #运行序号 [节点名] 前缀与所选等级）
        using (FlowLogContext.SuppressSink())
        {
            Console.WriteLine(Message);
        }
        FlowLogContext.Write(Message, Level);
        return Task.CompletedTask;
    }
}
