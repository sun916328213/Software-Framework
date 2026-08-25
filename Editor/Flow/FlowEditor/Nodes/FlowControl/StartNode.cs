
using FlowEditor.Core;
using FlowEditor.Models;

namespace FlowEditor.Nodes.FlowControl;

/// <summary>
/// 开始节点：流程图的入口点，默认勾选 IN、Out。
/// </summary>
[FlowTool("开始", "PlayCircleOutline", ToolGroup = "流程控制", SortIndex = 0, Description = "流程图的入口点，每个流程必须有一个开始节点")]
public class StartNode : NodeModel
{
    public StartNode()
    {
        Name = "开始";
        IsInEnabled = false; // 开始节点无输入
        IsOutEnabled = true;
    }

    public override Task ExecuteAsync(CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}
