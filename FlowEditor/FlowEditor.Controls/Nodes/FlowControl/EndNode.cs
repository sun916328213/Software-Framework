using FlowEditor.Core;
using FlowEditor.Models;

namespace FlowEditor.Nodes.FlowControl;

/// <summary>
/// 结束节点：流程图的出口点，只有输入端口。
/// </summary>
[FlowTool("结束", "StopCircleOutline", ToolGroup = "流程控制", SortIndex = 1, Description = "流程图的出口点")]
public class EndNode : NodeModel
{
    /// <summary>创建只带流程输入端口的出口节点。</summary>
    public EndNode()
    {
        Name = "结束";
        IsInEnabled = true;
        IsOutEnabled = false;
        Status = NodeStatus.Success;
    }

    /// <inheritdoc />
    public override Task ExecuteAsync(CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}
