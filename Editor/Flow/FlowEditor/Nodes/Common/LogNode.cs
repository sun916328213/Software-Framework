using FlowEditor.Core;

namespace FlowEditor.Nodes.Common;

/// <summary>
/// 日志节点：输出日志信息。
/// </summary>
[FlowTool("日志输出", "TextBoxOutline", ToolGroup = "通用", SortIndex = 10, Description = "输出日志信息到控制台")]
public class LogNode : NodeModel
{
    private string _message = "Hello World";
    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    public LogNode()
    {
        Name = "日志输出";
        IsInEnabled = true;
        IsOutEnabled = true;
    }

    public override Task ExecuteAsync(CancellationToken ct)
    {
        Console.WriteLine($"[LogNode] {Message}");
        return Task.CompletedTask;
    }
}
