using FlowEditor.Models;
using FlowEditor.Nodes;
using FlowEditor.Nodes.FlowControl;

namespace FlowEditor.Services;

/// <summary>
/// 连接领域模块：集中执行端口兼容性校验、连线规划、环路检测和数据端口自动绑定。
/// 本模块不修改调用方状态，只返回不可变计划，便于预览、撤销编排与测试。
/// </summary>
public sealed class ConnectionService
{
    /// <summary>规划一组连接，不修改现有节点或连线集合。</summary>
    public ConnectionPlan PlanConnection(
        PortModel sourcePort,
        PortModel targetPort,
        IReadOnlyCollection<NodeModel> nodes,
        IReadOnlyCollection<ConnectionModel> existingConnections)
    {
        var error = GetRejectionReason(sourcePort, targetPort, existingConnections, nodes);
        if (error != null)
            return ConnectionPlan.Rejected(error);

        var createsCycle = WouldCreateCycle(
            sourcePort.OwnerNodeId,
            targetPort.OwnerNodeId,
            existingConnections);

        var connection = new ConnectionModel
        {
            SourceNodeId = sourcePort.OwnerNodeId,
            SourcePortId = sourcePort.Id,
            TargetNodeId = targetPort.OwnerNodeId,
            TargetPortId = targetPort.Id
        };
        var plannedConnections = new List<ConnectionModel> { connection };
        if (sourcePort.DataType == PortType.Flow)
            plannedConnections.AddRange(PlanDataBindings(
                sourcePort.OwnerNodeId,
                targetPort.OwnerNodeId,
                nodes,
                existingConnections));

        return ConnectionPlan.Accepted(connection, plannedConnections, createsCycle);
    }

    /// <summary>返回连接拒绝原因；返回 <see langword="null"/> 表示当前可以连接。</summary>
    public string? GetRejectionReason(
        PortModel sourcePort,
        PortModel targetPort,
        IEnumerable<ConnectionModel> existingConnections,
        IEnumerable<NodeModel>? nodes = null)
    {
        if (sourcePort.OwnerNodeId == targetPort.OwnerNodeId)
            return "不能连接同一节点的端口";

        if (sourcePort.Direction != PortDirection.Output || targetPort.Direction != PortDirection.Input)
            return "只能从输出端口连到输入端口";

        if (!targetPort.IsTypeCompatibleWith(sourcePort.DataType))
            return $"端口类型不兼容：{sourcePort.DataType} → {targetPort.DataType}";

        if (existingConnections.Any(c => c.SourcePortId == sourcePort.Id && c.TargetPortId == targetPort.Id))
            return "这两个端口之间已存在连线";

        if (sourcePort.DataType == PortType.Flow && existingConnections.Any(c => c.SourcePortId == sourcePort.Id))
            return $"流程输出端口「{sourcePort.Name}」只能连接一个输入；需要并行时请使用并行分叉";

        if (existingConnections.Any(c => c.TargetPortId == targetPort.Id))
            return targetPort.DataType == PortType.Flow
                ? $"流程输入端口「{targetPort.Name}」只能接受一条连线；需要汇聚时请使用分支汇聚的独立输入"
                : $"数据端口「{targetPort.Name}」已连接，仅允许一条连线";

        if (nodes != null)
        {
            var sourceNode = nodes.FirstOrDefault(node => node.Id == sourcePort.OwnerNodeId);
            var targetNode = nodes.FirstOrDefault(node => node.Id == targetPort.OwnerNodeId);
            if (sourceNode != null && targetNode != null &&
                (sourceNode.ParentExecutionContainerId != targetNode.ParentExecutionContainerId ||
                 sourceNode.ExecutionLaneId != targetNode.ExecutionLaneId) &&
                !IsLoopBoundaryConnection(sourceNode, targetNode, nodes))
                return "不能跨越执行容器或泳道连线，请连接容器自身的 IN/OUT";
        }

        return null;
    }

    private static bool IsLoopBoundaryConnection(
        NodeModel source,
        NodeModel target,
        IEnumerable<NodeModel> nodes)
    {
        var nodeList = nodes as IReadOnlyCollection<NodeModel> ?? nodes.ToList();
        return IsInsideToOutside(source, target, nodeList) ||
               IsInsideToOutside(target, source, nodeList);
    }

    private static bool IsInsideToOutside(
        NodeModel inside,
        NodeModel outside,
        IEnumerable<NodeModel> nodes)
    {
        if (inside.ParentExecutionContainerId == null) return false;
        var loop = nodes.FirstOrDefault(node =>
            node.Id == inside.ParentExecutionContainerId && node is LoopContainerNode);
        return loop != null &&
               outside.ParentExecutionContainerId == loop.ParentExecutionContainerId &&
               outside.ExecutionLaneId == loop.ExecutionLaneId;
    }

    private static bool WouldCreateCycle(
        string sourceNodeId,
        string targetNodeId,
        IEnumerable<ConnectionModel> connections)
    {
        var adjacency = connections
            .GroupBy(c => c.SourceNodeId)
            .ToDictionary(g => g.Key, g => g.Select(c => c.TargetNodeId).Distinct().ToArray());
        var visited = new HashSet<string>();
        var queue = new Queue<string>();
        queue.Enqueue(targetNodeId);

        while (queue.TryDequeue(out var current))
        {
            if (current == sourceNodeId)
                return true;
            if (!visited.Add(current) || !adjacency.TryGetValue(current, out var successors))
                continue;
            foreach (var successor in successors)
                queue.Enqueue(successor);
        }

        return false;
    }

    private static IReadOnlyList<ConnectionModel> PlanDataBindings(
        string sourceNodeId,
        string targetNodeId,
        IEnumerable<NodeModel> nodes,
        IReadOnlyCollection<ConnectionModel> existingConnections)
    {
        var sourceNode = nodes.FirstOrDefault(n => n.Id == sourceNodeId);
        var targetNode = nodes.FirstOrDefault(n => n.Id == targetNodeId);
        if (sourceNode == null || targetNode == null)
            return Array.Empty<ConnectionModel>();

        var planned = new List<ConnectionModel>();
        foreach (var output in sourceNode.OutputPorts.Where(p => p.DataType != PortType.Flow))
        {
            var propertyName = output.PropertyName ?? output.Name;
            if (string.IsNullOrWhiteSpace(propertyName))
                continue;

            var input = targetNode.InputPorts.FirstOrDefault(p =>
                p.DataType != PortType.Flow &&
                p.IsTypeCompatibleWith(output.DataType) &&
                (p.PropertyName == propertyName || p.Name == propertyName));
            if (input == null ||
                existingConnections.Any(c => c.TargetPortId == input.Id) ||
                planned.Any(c => c.TargetPortId == input.Id))
                continue;

            planned.Add(new ConnectionModel
            {
                SourceNodeId = sourceNodeId,
                SourcePortId = output.Id,
                TargetNodeId = targetNodeId,
                TargetPortId = input.Id
            });
        }

        return planned;
    }
}

/// <summary>连接领域模块的单一返回结果。</summary>
public sealed record ConnectionPlan(
    bool Success,
    string? Error,
    ConnectionModel? PrimaryConnection,
    IReadOnlyList<ConnectionModel> Connections,
    bool CreatesCycle,
    int AutoBoundCount)
{
    internal static ConnectionPlan Rejected(string error)
        => new(false, error, null, Array.Empty<ConnectionModel>(), false, 0);

    internal static ConnectionPlan Accepted(
        ConnectionModel connection,
        IReadOnlyList<ConnectionModel> connections,
        bool createsCycle) => new(
            true,
            null,
            connection,
            connections,
            createsCycle,
            Math.Max(0, connections.Count - 1));
}
