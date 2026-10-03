// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：EndNode。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、FlowEditor.Controls.Nodes、FlowEditor.Core.Nodes、FlowEditor.Controls.Core、FlowEditor.Core、FlowEditor.Runtime。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using FlowEditor.Controls.Nodes;
using FlowEditor.Core.Nodes;
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;
using FlowEditor.Controls.Models;
using FlowEditor.Core.Models;

namespace FlowEditor.Controls.Nodes.FlowControl;

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
    /// <remarks>
    /// 执行当前入口的处理流程；对应入口为 ExecuteAsync。
    /// </remarks>
    public override Task ExecuteAsync(CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}
