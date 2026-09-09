using FlowEditor.Models;
using FlowEditor.ViewModels;

namespace FlowEditor.Core;

/// <summary>
/// 从编辑画布提取可执行子图。被放入“不执行节点容器”的节点仍可保存和编辑，
/// 但节点本身及与其相连的边不会进入校验、编译或运行阶段。
/// </summary>
public sealed record ExecutionGraphScope(
    IReadOnlyList<NodeViewModel> Nodes,
    IReadOnlyList<ConnectionModel> Connections)
{
    public static ExecutionGraphScope Create(
        IEnumerable<NodeViewModel> nodes,
        IEnumerable<ConnectionModel> connections)
    {
        var activeNodes = nodes.Where(n => !n.IsExcludedFromExecution).ToList();
        var activeIds = activeNodes.Select(n => n.Id).ToHashSet();
        var activeConnections = connections
            .Where(c => activeIds.Contains(c.SourceNodeId) && activeIds.Contains(c.TargetNodeId))
            .ToList();
        return new ExecutionGraphScope(activeNodes, activeConnections);
    }
}
