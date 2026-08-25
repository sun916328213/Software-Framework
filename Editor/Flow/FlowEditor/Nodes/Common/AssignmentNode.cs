using FlowEditor.Core;
using FlowEditor.Models;

namespace FlowEditor.Nodes.Common;

/// <summary>
/// 赋值节点：设置变量值，支持动态输出端口绑定。
/// </summary>
[FlowTool("赋值", "Variable", ToolGroup = "通用", SortIndex = 11, Description = "设置变量值，支持动态输出端口绑定")]
public class AssignmentNode : NodeModel
{
    private string _variableName = "result";
    public string VariableName
    {
        get => _variableName;
        set => SetProperty(ref _variableName, value);
    }

    public AssignmentNode()
    {
        Name = "赋值";
        IsInEnabled = true;
        IsOutEnabled = true;
    }

    public override Task ExecuteAsync(CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}
