using FlowEditor.Models;
using FlowEditor.Nodes;
using FlowEditor.Nodes.FlowControl;

namespace FlowEditor.Core;

/// <summary>保存从编辑画布筛选出的强类型可执行子图。</summary>
/// <typeparam name="TNode">实现最小运行契约的节点类型。</typeparam>
public sealed record ExecutionGraphScope<TNode>(
    IReadOnlyList<TNode> Nodes,
    IReadOnlyList<ConnectionModel> Connections)
    where TNode : INodeExecutable;

/// <summary>从编辑画布创建不包含停放节点及其连线的可执行子图。</summary>
public static class ExecutionGraphScope
{
    /// <summary>
    /// 从指定节点创建只包含其自身及所有可达后继的执行子图。
    /// 指向起点的入边会自然被移除，因此起点可作为恢复或跳转入口立即执行。
    /// </summary>
    public static ExecutionGraphScope<TNode> CreateReachable<TNode>(
        IEnumerable<TNode> nodes,
        IEnumerable<ConnectionModel> connections,
        string startNodeId)
        where TNode : INodeExecutable
    {
        var nodeList = nodes.Where(node => !node.IsExcludedFromExecution).ToList();
        if (nodeList.All(node => node.Id != startNodeId))
            throw new ArgumentException("恢复起点不属于当前执行作用域。", nameof(startNodeId));

        var connectionList = connections.ToList();
        var reachableIds = new HashSet<string> { startNodeId };
        var pending = new Queue<string>();
        pending.Enqueue(startNodeId);
        while (pending.Count > 0)
        {
            var current = pending.Dequeue();
            foreach (var targetId in connectionList
                         .Where(connection => connection.SourceNodeId == current)
                         .Select(connection => connection.TargetNodeId))
            {
                if (reachableIds.Add(targetId))
                    pending.Enqueue(targetId);
            }
        }

        var reachableNodes = nodeList.Where(node => reachableIds.Contains(node.Id)).ToList();
        var actualIds = reachableNodes.Select(node => node.Id).ToHashSet();
        var reachableConnections = connectionList.Where(connection =>
                actualIds.Contains(connection.SourceNodeId) &&
                actualIds.Contains(connection.TargetNodeId))
            .ToList();
        return new ExecutionGraphScope<TNode>(reachableNodes, reachableConnections);
    }

    /// <summary>
    /// 创建顶层可执行图，并把穿过循环容器边界的原始控制流连线映射到循环容器。
    /// 画布仍保存并显示“外部节点 → 循环体入口 / 循环体出口 → 外部节点”，
    /// 调度器只看到一个可执行的循环复合节点。
    /// </summary>
    public static ExecutionGraphScope<TNode> CreateTopLevel<TNode>(
        IEnumerable<TNode> nodes,
        IEnumerable<ConnectionModel> connections,
        Func<TNode, NodeModel> modelSelector)
        where TNode : INodeExecutable
    {
        var activeNodes = nodes.Where(node => !node.IsExcludedFromExecution).ToList();
        var nodeMap = activeNodes.ToDictionary(node => node.Id);
        var modelMap = activeNodes.ToDictionary(node => node.Id, modelSelector);
        var topNodes = activeNodes.Where(node => node.ParentExecutionContainerId == null).ToList();
        var topIds = topNodes.Select(node => node.Id).ToHashSet();
        var topLevelLoops = topNodes
            .Where(node => modelSelector(node) is LoopContainerNode)
            .ToDictionary(node => node.Id);
        var effective = new List<ConnectionModel>();

        foreach (var connection in connections)
        {
            if (!nodeMap.ContainsKey(connection.SourceNodeId) ||
                !nodeMap.ContainsKey(connection.TargetNodeId)) continue;
            var sourceModel = modelMap[connection.SourceNodeId];
            var targetModel = modelMap[connection.TargetNodeId];

            if (topIds.Contains(connection.SourceNodeId) && topIds.Contains(connection.TargetNodeId))
            {
                effective.Add(connection);
                continue;
            }

            // 数据边暂不改变作用域；循环边界只消费控制流令牌。
            if (sourceModel.FindPortById(connection.SourcePortId)?.DataType != PortType.Flow)
                continue;

            var sourceId = topIds.Contains(connection.SourceNodeId)
                ? connection.SourceNodeId
                : ResolveOwningTopLoop(sourceModel, topLevelLoops);
            var targetId = topIds.Contains(connection.TargetNodeId)
                ? connection.TargetNodeId
                : ResolveOwningTopLoop(targetModel, topLevelLoops);
            if (sourceId == null || targetId == null || sourceId == targetId)
                continue;

            effective.Add(new ConnectionModel
            {
                Id = $"{connection.Id}:loop-boundary",
                SourceNodeId = sourceId,
                SourcePortId = sourceId == connection.SourceNodeId
                    ? connection.SourcePortId
                    : $"{sourceId}_Next_Out",
                TargetNodeId = targetId,
                TargetPortId = targetId == connection.TargetNodeId
                    ? connection.TargetPortId
                    : $"{targetId}_Next_IN"
            });
        }

        var distinct = effective
            .GroupBy(connection => new
            {
                connection.SourceNodeId,
                connection.SourcePortId,
                connection.TargetNodeId,
                connection.TargetPortId
            })
            .Select(group => group.First())
            .ToList();
        return new ExecutionGraphScope<TNode>(topNodes, distinct);
    }

    private static string? ResolveOwningTopLoop<TNode>(
        NodeModel model,
        IReadOnlyDictionary<string, TNode> topLevelLoops)
        where TNode : INodeExecutable =>
        model.ParentExecutionContainerId != null &&
        topLevelLoops.ContainsKey(model.ParentExecutionContainerId)
            ? model.ParentExecutionContainerId
            : null;

    /// <summary>筛选可参与编译、校验和运行的节点与连线。</summary>
    /// <typeparam name="TNode">调用方使用的具体节点执行类型。</typeparam>
    /// <param name="nodes">编辑画布中的全部节点。</param>
    /// <param name="connections">编辑画布中的全部连线。</param>
    /// <returns>保留原节点类型的可执行子图。</returns>
    public static ExecutionGraphScope<TNode> Create<TNode>(
        IEnumerable<TNode> nodes,
        IEnumerable<ConnectionModel> connections,
        string? parentContainerId = null,
        string? laneId = null)
        where TNode : INodeExecutable
    {
        var activeNodes = nodes.Where(node =>
                !node.IsExcludedFromExecution &&
                string.Equals(node.ParentExecutionContainerId, parentContainerId, StringComparison.Ordinal) &&
                (laneId == null || string.Equals(node.ExecutionLaneId, laneId, StringComparison.Ordinal)))
            .ToList();
        var activeIds = activeNodes.Select(node => node.Id).ToHashSet();
        var activeConnections = connections
            .Where(connection =>
                activeIds.Contains(connection.SourceNodeId) &&
                activeIds.Contains(connection.TargetNodeId))
            .ToList();

        return new ExecutionGraphScope<TNode>(activeNodes, activeConnections);
    }
}
