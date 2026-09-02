using Extension.Struct;
using FlowEditor.Core;
using WPFCustomControls.PropertyGrid.Models;

namespace FlowEditor.Nodes.Common;

/// <summary>
/// 日志节点：输出日志信息到输出面板与流程日志文件。
/// 写入流程日志文件经 FlowLogContext.Write（带 #运行序号 [节点名] 前缀与所选日志等级），
/// 落盘 FileSink 由宿主注册路由——无需依赖 FlowNodes 的 NodeLog，FlowEditor 内原语即可实现。
/// </summary>
[FlowTool("日志输出", "TextBoxOutline", ToolGroup = "通用", SortIndex = 10, Description = "输出日志信息到控制台与流程日志文件")]
public class LogNode : NodeModel
{
    private string _message = "Hello World";
    [NodeProperty(DisplayName = "信息", Group = "1.基础信息", Order = 1)]
    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    private ELogLevel _level = ELogLevel.Information;
    [NodeProperty(DisplayName = "信息等级", Group = "1.基础信息", Order = 2)]
    public ELogLevel Level
    {
        get => _level;
        set => SetProperty(ref _level, value);
    }

    public LogNode()
    {
        Name = "日志输出";
    }

    public override Task ExecuteAsync(CancellationToken ct)
    {
        // 信息级：Console 捕获路径自动完成「面板 + 文件（信息级）」，一句即可
        if (Level == ELogLevel.Information)
        {
            Console.WriteLine(Message);
            return Task.CompletedTask;
        }

        // 非信息级：面板（抑制落盘避免重复）→ 文件（带 #运行序号 [节点名] 前缀与所选等级）
        using (FlowLogContext.SuppressFileSink())
        {
            Console.WriteLine(Message);
        }
        FlowLogContext.Write(Message, Level);
        return Task.CompletedTask;
    }
}
