using FlowEditor.Models;

namespace FlowEditor.Core;

/// <summary>
/// 流程图静态分析器（P2）。
/// 在运行前一次性完成全部检查并输出结构化报告，替代"跑到一半才崩"：
/// - E-IN-REQ   勾选 IN 的节点缺少 Next_IN 连线（错误）
/// - E-TYPE     连线两端端口声明类型不兼容（错误）
/// - W-PORT     连线引用的端口已不存在（警告，悬空连线）
/// - W-BINDING  已连线端口的绑定属性在节点上不存在或不可写（警告）
/// - W-ORPHAN   从入口不可达的孤立节点（警告）
/// - I-CYCLE    存在环路（提示，环路为合法结构）
/// 全部检查基于编译图 O(N+E) 完成。
/// </summary>
public static class FlowValidator
{
    /// <summary>分析节点模型和连线，并返回执行前诊断报告。</summary>
    /// <param name="nodes">待校验的节点模型。</param>
    /// <param name="connections">待校验的连线。</param>
    /// <returns>包含全部错误、警告和提示的报告。</returns>
    public static FlowValidationReport Analyze(
        IReadOnlyList<INodeExecutable> nodes,
        IReadOnlyList<ConnectionModel> connections)
    {
        var report = new FlowValidationReport();
        if (nodes.Count == 0)
            return report;

        foreach (var node in nodes)
            if (node is IFlowStructureValidationSource structureSource)
                report.Diagnostics.AddRange(structureSource.ValidateStructure());

        ValidateFlowPortCardinality(nodes, connections, report);
        ValidateParallelForks(nodes, connections, report);
        ValidateExecutionContainers(nodes, connections, report);

        var topLevel = ExecutionGraphScope.CreateTopLevel(nodes, connections);
        AnalyzeScope(
            topLevel.Nodes,
            topLevel.Connections,
            report,
            allowImplicitEntry: false,
            scopeName: null);

        foreach (var container in nodes.OfType<IExecutionScopeDescriptor>())
        {
            var containerNode = nodes.First(node => ReferenceEquals(node, container));
            foreach (var lane in container.ExecutionLanes)
            {
                var laneNodes = nodes.Where(node =>
                        node.ParentExecutionContainerId == containerNode.Id &&
                        node.ExecutionLaneId == lane.Id)
                    .ToList();
                AnalyzeScope(
                    laneNodes,
                    connections,
                    report,
                    allowImplicitEntry: true,
                    scopeName: $"{containerNode.Name}/{lane.Name}");
            }
        }

        return report;
    }

    /// <summary>
    /// 控制流不允许用同一个端口表达隐式分叉或隐式汇聚。
    /// 分叉节点通过多个输出端口表达，汇聚节点通过多个输入端口表达。
    /// </summary>
    private static void ValidateFlowPortCardinality<TNode>(
        IReadOnlyList<TNode> nodes,
        IReadOnlyList<ConnectionModel> connections,
        FlowValidationReport report)
        where TNode : INodeExecutable
    {
        var nodeMap = nodes.ToDictionary(node => node.Id);
        foreach (var group in connections.GroupBy(connection => connection.SourcePortId))
        {
            if (group.Count() <= 1) continue;
            var first = group.First();
            if (!nodeMap.TryGetValue(first.SourceNodeId, out var source) ||
                source.FindPortById(first.SourcePortId)?.DataType != PortType.Flow) continue;
            report.Diagnostics.Add(new FlowDiagnostic(
                FlowDiagnosticSeverity.Error, "E-FLOW-FANOUT",
                $"节点 [{source.Name}] 的流程输出端口只能连接一个输入；需要并行时请使用并行分叉"));
        }

        foreach (var group in connections.GroupBy(connection => connection.TargetPortId))
        {
            if (group.Count() <= 1) continue;
            var first = group.First();
            if (!nodeMap.TryGetValue(first.TargetNodeId, out var target) ||
                target.FindPortById(first.TargetPortId)?.DataType != PortType.Flow) continue;
            report.Diagnostics.Add(new FlowDiagnostic(
                FlowDiagnosticSeverity.Error, "E-FLOW-FANIN",
                $"节点 [{target.Name}] 的流程输入端口只能接受一条连线；需要汇聚时请使用分支汇聚的独立输入"));
        }
    }

    /// <summary>并行分叉的全部输出必须有线，并在下游收束到同一个 AND 汇聚。</summary>
    private static void ValidateParallelForks<TNode>(
        IReadOnlyList<TNode> nodes,
        IReadOnlyList<ConnectionModel> connections,
        FlowValidationReport report)
        where TNode : INodeExecutable
    {
        var nodeMap = nodes.ToDictionary(node => node.Id);
        var flowConnections = connections.Where(connection =>
                nodeMap.TryGetValue(connection.SourceNodeId, out var source) &&
                source.FindPortById(connection.SourcePortId)?.DataType == PortType.Flow)
            .ToList();
        var outgoing = flowConnections.GroupBy(connection => connection.SourceNodeId)
            .ToDictionary(group => group.Key, group => group.ToList());

        foreach (var fork in nodes.OfType<IParallelForkNode>())
        {
            var branchPorts = fork.OutputPorts.Where(port => port.DataType == PortType.Flow).ToList();
            var branchStarts = new List<string>(branchPorts.Count);
            foreach (var port in branchPorts)
            {
                var branchConnections = flowConnections
                    .Where(connection => connection.SourceNodeId == fork.Id &&
                                         connection.SourcePortId == port.Id)
                    .ToList();
                if (branchConnections.Count == 0)
                {
                    report.Diagnostics.Add(new FlowDiagnostic(
                        FlowDiagnosticSeverity.Error, "E-PAR-BRANCH-UNCONNECTED",
                        $"并行分叉 [{fork.Name}] 的输出 [{port.Name}] 未连接"));
                    continue;
                }
                branchStarts.Add(branchConnections[0].TargetNodeId);
            }

            if (branchStarts.Count != branchPorts.Count)
                continue;

            HashSet<string>? commonMerges = null;
            foreach (var startId in branchStarts)
            {
                var reachable = FindReachableAndMerges(startId, nodeMap, outgoing);
                if (commonMerges == null)
                    commonMerges = reachable;
                else
                    commonMerges.IntersectWith(reachable);
            }

            if (commonMerges is not { Count: > 0 })
                report.Diagnostics.Add(new FlowDiagnostic(
                    FlowDiagnosticSeverity.Error, "E-PAR-JOIN-MISSING",
                    $"并行分叉 [{fork.Name}] 的全部分支必须汇入同一个 AND 分支汇聚节点"));
        }
    }

    /// <summary>查找从分支入口可达的全部 AND 汇聚节点。</summary>
    private static HashSet<string> FindReachableAndMerges<TNode>(
        string startId,
        IReadOnlyDictionary<string, TNode> nodeMap,
        IReadOnlyDictionary<string, List<ConnectionModel>> outgoing)
        where TNode : INodeExecutable
    {
        var result = new HashSet<string>();
        var visited = new HashSet<string>();
        var queue = new Queue<string>();
        queue.Enqueue(startId);
        while (queue.TryDequeue(out var current) && visited.Count <= nodeMap.Count)
        {
            if (!visited.Add(current)) continue;
            if (nodeMap.TryGetValue(current, out var node) &&
                node.FlowJoinMode == FlowJoinMode.AllActive)
                result.Add(current);
            if (!outgoing.TryGetValue(current, out var next)) continue;
            foreach (var connection in next)
                queue.Enqueue(connection.TargetNodeId);
        }
        return result;
    }

    /// <summary>分析单个可执行作用域的入口、可达性、端口和类型规则。</summary>
    private static void AnalyzeScope<TNode>(
        IReadOnlyList<TNode> nodes,
        IReadOnlyList<ConnectionModel> allConnections,
        FlowValidationReport report,
        bool allowImplicitEntry,
        string? scopeName)
        where TNode : INodeExecutable
    {
        if (nodes.Count == 0) return;
        var ids = nodes.Select(node => node.Id).ToHashSet();
        var connections = allConnections.Where(connection =>
                ids.Contains(connection.SourceNodeId) && ids.Contains(connection.TargetNodeId))
            .ToList();
        var graph = CompiledFlowGraph<TNode>.Compile(nodes, connections);

        // 可达性：从入口沿出边 BFS
        var reachable = new HashSet<string>();
        var queue = new Queue<string>(graph.EntryNodes.Select(n => n.Id));
        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            if (!reachable.Add(id))
                continue;
            foreach (var succ in graph.Successors[id])
                queue.Enqueue(succ);
        }

        foreach (var node in nodes)
        {
            // IN 必连检查
            if (node is IFlowValidationNode { RequiresFlowInput: true })
            {
                var inPort = node.InputPorts.FirstOrDefault(p => p.Name == "Next_IN");
                if (inPort != null && graph.IncomingConnections[node.Id].Count == 0 &&
                    !allowImplicitEntry)
                {
                    report.Diagnostics.Add(new FlowDiagnostic(
                        FlowDiagnosticSeverity.Error, "E-IN-REQ",
                        $"节点 [{node.Name}] 的 IN 端口未连接，请先连线"));
                }
            }

            // 不可达 / 完全孤立节点：
            // - 非入口且从入口不可达
            // - 或无任何连线相连（零度数节点会被入口判定误收，需单独识别）
            var hasOutgoing = graph.Successors[node.Id].Count > 0;
            var hasIncoming = graph.IncomingConnections[node.Id].Count > 0;
            if (!reachable.Contains(node.Id) ||
                (!allowImplicitEntry && !hasOutgoing && !hasIncoming))
            {
                report.Diagnostics.Add(new FlowDiagnostic(
                    FlowDiagnosticSeverity.Warning, "W-ORPHAN",
                    $"节点 [{node.Name}] 从任何入口均不可达，将不会被执行"));
            }

            // 连线级检查：类型兼容 + 端口存在 + 绑定属性有效
            foreach (var conn in graph.IncomingConnections[node.Id])
            {
                if (!graph.NodeMap.TryGetValue(conn.SourceNodeId, out var sourceNode))
                    continue;
                var sourcePort = sourceNode.FindPortById(conn.SourcePortId);
                var targetPort = node.FindPortById(conn.TargetPortId);

                if (sourcePort == null || targetPort == null)
                {
                    report.Diagnostics.Add(new FlowDiagnostic(
                        FlowDiagnosticSeverity.Warning, "W-PORT",
                        $"连线 {Short(sourceNode?.Name, conn.SourceNodeId)} → {node.Name} 引用了不存在的端口，将被忽略"));
                    continue;
                }

                // 声明类型兼容性（与运行期 FillInputPorts 第一道检查一致）
                if (!targetPort.IsTypeCompatibleWith(sourcePort.DataType))
                {
                    report.Diagnostics.Add(new FlowDiagnostic(
                        FlowDiagnosticSeverity.Error, "E-TYPE",
                        $"源端口 [{sourceNode.Name}].{sourcePort.Name}({sourcePort.DataType}) " +
                        $"与目标端口 [{node.Name}].{targetPort.Name}({targetPort.DataType}) 声明类型不兼容"));
                }

                // 输入端口绑定属性可写性（运行期 SyncInputValues 会静默跳过缺失属性）
                var inPropName = targetPort.PropertyName ?? targetPort.Name;
                if (targetPort.Category == PortCategory.Custom && !HasReadableWritableProperty(node, inPropName, writable: true))
                {
                    report.Diagnostics.Add(new FlowDiagnostic(
                        FlowDiagnosticSeverity.Warning, "W-BINDING",
                        $"节点 [{node.Name}] 的输入端口 [{targetPort.Name}] 绑定的属性 {inPropName} 不存在或不可写，传入值将被忽略"));
                }
            }
        }

        // 环路提示（合法结构，仅提示调试/终止语义）
        if (graph.HasCycle)
        {
            report.Diagnostics.Add(new FlowDiagnostic(
                FlowDiagnosticSeverity.Info, "I-CYCLE",
                $"{(scopeName == null ? "流程图" : $"作用域 [{scopeName}]")}包含 {graph.BackEdges.Count} 条回边（环路），将以路径追踪模式执行；请确保环路可自然终止"));
        }
    }

    /// <summary>验证容器归属、泳道入口和跨作用域连线规则。</summary>
    private static void ValidateExecutionContainers<TNode>(
        IReadOnlyList<TNode> nodes,
        IReadOnlyList<ConnectionModel> connections,
        FlowValidationReport report)
        where TNode : INodeExecutable
    {
        var nodeMap = nodes.ToDictionary(node => node.Id);
        var containers = nodes
            .Where(node => node is IExecutionScopeDescriptor)
            .ToDictionary(node => node.Id, node => (Node: node, Scope: (IExecutionScopeDescriptor)node));

        foreach (var node in nodes.Where(node => node.ParentExecutionContainerId != null))
        {
            if (!containers.TryGetValue(node.ParentExecutionContainerId!, out var parent))
            {
                report.Diagnostics.Add(new FlowDiagnostic(
                    FlowDiagnosticSeverity.Error, "E-SCOPE-PARENT",
                    $"节点 [{node.Name}] 所属的执行容器不存在"));
                continue;
            }

            if (node.ExecutionLaneId == null ||
                parent.Scope.ExecutionLanes.All(lane => lane.Id != node.ExecutionLaneId))
                report.Diagnostics.Add(new FlowDiagnostic(
                    FlowDiagnosticSeverity.Error, "E-SCOPE-LANE",
                    $"节点 [{node.Name}] 未归属到 [{parent.Node.Name}] 的有效泳道"));
        }

        foreach (var (containerId, containerEntry) in containers)
        {
            var containerNode = containerEntry.Node;
            var container = containerEntry.Scope;
            foreach (var lane in container.ExecutionLanes)
            {
                var members = nodes.Where(node =>
                        node.ParentExecutionContainerId == containerId &&
                        node.ExecutionLaneId == lane.Id)
                    .ToList();
                if (members.Count == 0)
                {
                    var code = container.ScopeKind == ExecutionScopeKind.Parallel
                        ? "E-PAR-BRANCH-EMPTY"
                        : "E-LOOP-BODY-EMPTY";
                    report.Diagnostics.Add(new FlowDiagnostic(
                        FlowDiagnosticSeverity.Error, code,
                        $"{(container.ScopeKind == ExecutionScopeKind.Parallel ? "并行" : "循环")}容器 [{containerNode.Name}] 的{(container.ScopeKind == ExecutionScopeKind.Parallel ? "泳道" : "循环体")} [{lane.Name}] 没有节点"));
                    continue;
                }

                var memberIds = members.Select(member => member.Id).ToHashSet();
                var internalFlowConnections = connections.Where(connection =>
                {
                    if (!memberIds.Contains(connection.SourceNodeId) ||
                        !memberIds.Contains(connection.TargetNodeId)) return false;
                    var source = nodeMap[connection.SourceNodeId];
                    return source.FindPortById(connection.SourcePortId)?.DataType == PortType.Flow;
                }).ToList();
                var targets = internalFlowConnections.Select(connection => connection.TargetNodeId).ToHashSet();
                var entryCount = members.Count(member => !targets.Contains(member.Id));
                if (entryCount != 1)
                    report.Diagnostics.Add(new FlowDiagnostic(
                        FlowDiagnosticSeverity.Error, "E-SCOPE-ENTRY",
                        $"容器 [{containerNode.Name}] 的泳道 [{lane.Name}] 必须且只能有一个入口节点，当前为 {entryCount} 个"));
            }
        }

        foreach (var connection in connections)
        {
            if (!nodeMap.TryGetValue(connection.SourceNodeId, out var source) ||
                !nodeMap.TryGetValue(connection.TargetNodeId, out var target)) continue;
            if (source.ParentExecutionContainerId == target.ParentExecutionContainerId &&
                source.ExecutionLaneId == target.ExecutionLaneId) continue;
            if (source.ParentExecutionContainerId == null && target.ParentExecutionContainerId == null) continue;
            if (IsLoopBoundaryConnection(source, target, nodeMap)) continue;

            report.Diagnostics.Add(new FlowDiagnostic(
                FlowDiagnosticSeverity.Error, "E-SCOPE-CROSS",
                $"连线 [{source.Name}] → [{target.Name}] 跨越了容器或泳道边界；请连接容器自身的 IN/OUT"));
        }
    }

    /// <summary>判断连线是否为循环容器允许保留的边界连线。</summary>
    private static bool IsLoopBoundaryConnection<TNode>(
        TNode source,
        TNode target,
        IReadOnlyDictionary<string, TNode> nodes)
        where TNode : INodeExecutable =>
        IsInsideToOutsideLoop(source, target, nodes) ||
        IsInsideToOutsideLoop(target, source, nodes);

    /// <summary>判断节点对是否由循环体内部指向其同级外部作用域。</summary>
    private static bool IsInsideToOutsideLoop<TNode>(
        TNode inside,
        TNode outside,
        IReadOnlyDictionary<string, TNode> nodes)
        where TNode : INodeExecutable
    {
        if (inside.ParentExecutionContainerId == null ||
            !nodes.TryGetValue(inside.ParentExecutionContainerId, out var owner) ||
            owner is not IExecutionScopeDescriptor { ScopeKind: ExecutionScopeKind.Loop }) return false;
        return outside.ParentExecutionContainerId == owner.ParentExecutionContainerId &&
               outside.ExecutionLaneId == owner.ExecutionLaneId;
    }

    /// <summary>优先返回节点名称，缺失时回退到稳定标识。</summary>
    private static string Short(string? name, string fallback) => name ?? fallback;

    // 绑定属性检查缓存：(类型, 属性名, 是否要求可写) → 结果
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(Type, string, bool), bool> PropertyCheckCache = new();

    /// <summary>通过缓存反射结果验证端口绑定属性是否具有所需访问能力。</summary>
    private static bool HasReadableWritableProperty(INodeExecutable node, string propertyName, bool writable)
        => PropertyCheckCache.GetOrAdd((node.GetType(), propertyName, writable), static key =>
        {
            for (var t = key.Item1; t != null && t != typeof(object); t = t.BaseType)
            {
                var prop = t.GetProperty(key.Item2,
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);
                if (prop != null)
                    return key.Item3 ? prop.CanWrite : prop.CanRead;
            }
            return false;
        });
}
