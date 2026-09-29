using FlowEditor.Core;
using WPFCustomControls.PropertyGrid.Models;

namespace FlowEditor.Nodes.FlowControl;

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
