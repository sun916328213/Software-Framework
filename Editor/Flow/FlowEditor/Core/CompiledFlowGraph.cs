using FlowEditor.Models;

namespace FlowEditor.Core;

/// <summary>
/// 编译后的流程图（执行期只读）。
/// 在每次执行前一次性构建邻接表、入边索引、回边集合、前驱表与拓扑分层，
/// 将运行期图查询从 O(N×E) 降为 O(1) 查表；构建本身为 O(N+E)。
/// 对节点类型泛型化：引擎只依赖 INodeExecutable 执行面，不感知 ViewModel。
/// </summary>
public sealed class CompiledFlowGraph<TNode> where TNode : INodeExecutable
{
    private CompiledFlowGraph(
        IReadOnlyList<TNode> nodes,
        Dictionary<string, TNode> nodeMap,
        Dictionary<string, List<string>> successors,
        Dictionary<string, List<ConnectionModel>> successorConnections,
        Dictionary<string, List<string>> predecessors,
        Dictionary<string, List<ConnectionModel>> incomingConnections,
        HashSet<(string Source, string Target)> backEdges,
        List<List<TNode>> layers)
    {
        Nodes = nodes;
        NodeMap = nodeMap;
        Successors = successors;
        SuccessorConnections = successorConnections;
        Predecessors = predecessors;
        IncomingConnections = incomingConnections;
        BackEdges = backEdges;
        Layers = layers;

        var order = new List<TNode>(nodes.Count);
        foreach (var layer in layers)
            order.AddRange(layer);
        TopologicalOrder = order;

        var entries = new List<TNode>();
        foreach (var n in nodes)
        {
            if (predecessors[n.Id].Count == 0)
                entries.Add(n);
        }
        EntryNodes = entries.Count > 0 ? entries : nodes.ToList();
    }

    /// <summary>参与编译的节点列表。</summary>
    public IReadOnlyList<TNode> Nodes { get; }

    /// <summary>节点 ID → 节点映射。</summary>
    public IReadOnlyDictionary<string, TNode> NodeMap { get; }

    /// <summary>后继节点表（含回边）。</summary>
    public IReadOnlyDictionary<string, List<string>> Successors { get; }

    /// <summary>出边连线索引（按源节点分组，含回边；激活边过滤需要逐边判断 SourcePortId）。</summary>
    public IReadOnlyDictionary<string, List<ConnectionModel>> SuccessorConnections { get; }

    /// <summary>前驱节点表（不含回边，用于依赖计数）。</summary>
    public IReadOnlyDictionary<string, List<string>> Predecessors { get; }

    /// <summary>入边连线索引（按目标节点分组，保持原始连线顺序）。</summary>
    public IReadOnlyDictionary<string, List<ConnectionModel>> IncomingConnections { get; }

    /// <summary>回边集合（DFS 中指向递归栈内节点的边，即构成环路的边）。</summary>
    public IReadOnlyCollection<(string Source, string Target)> BackEdges { get; }

    /// <summary>是否存在环路。</summary>
    public bool HasCycle => BackEdges.Count > 0;

    /// <summary>拓扑分层（依赖深度分组，环路残留节点归入最后一层）。</summary>
    public IReadOnlyList<List<TNode>> Layers { get; }

    /// <summary>拓扑序（Layers 展平为一维列表）。</summary>
    public List<TNode> TopologicalOrder { get; }

    /// <summary>入口节点（无前驱）；全环等退化情况下回退为全部节点。</summary>
    public IReadOnlyList<TNode> EntryNodes { get; }

    /// <summary>编译流程图：单遍构建邻接表 + 迭代 DFS 检测回边 + Kahn 分层。复杂度 O(N+E)。</summary>
    public static CompiledFlowGraph<TNodeItem> Compile<TNodeItem>(
        IReadOnlyList<TNodeItem> nodes,
        IReadOnlyList<ConnectionModel> connections) where TNodeItem : INodeExecutable
    {
        var count = nodes.Count;
        var nodeMap = new Dictionary<string, TNodeItem>(count);
        var successors = new Dictionary<string, List<string>>(count);
        var successorConns = new Dictionary<string, List<ConnectionModel>>(count);
        var predecessors = new Dictionary<string, List<string>>(count);
        var incoming = new Dictionary<string, List<ConnectionModel>>(count);

        foreach (var n in nodes)
        {
            nodeMap[n.Id] = n;
            successors[n.Id] = [];
            successorConns[n.Id] = [];
            predecessors[n.Id] = [];
            incoming[n.Id] = [];
        }

        // 循环回边归一化：为循环节点补全未显式连接的回边（循环体末端悬空即可）。
        // 合成连线仅存在于编译结果中，不写回画布，老图（已画回边）自动跳过。
        var effectiveConnections = LoopNormalizer.Normalize(nodes, connections);

        // 单遍扫描建立出边/入边索引
        foreach (var conn in effectiveConnections)
        {
            if (!nodeMap.ContainsKey(conn.SourceNodeId) || !nodeMap.ContainsKey(conn.TargetNodeId))
                continue;
            successors[conn.SourceNodeId].Add(conn.TargetNodeId);
            successorConns[conn.SourceNodeId].Add(conn);
            incoming[conn.TargetNodeId].Add(conn);
        }

        // 迭代 DFS 三色标记检测回边（0=未访问 1=在栈中 2=已完成）。
        // 显式栈避免大图深递归栈溢出；邻接表保持连线原始顺序，回边判定与旧递归实现一致。
        var backEdges = new HashSet<(string Source, string Target)>();
        var state = new Dictionary<string, int>(count);
        foreach (var n in nodes)
            state[n.Id] = 0;

        foreach (var root in nodes)
        {
            if (state[root.Id] != 0) continue;
            state[root.Id] = 1;
            var stack = new Stack<(string Node, int Index)>();
            stack.Push((root.Id, 0));
            while (stack.Count > 0)
            {
                var (current, index) = stack.Pop();
                var adjacency = successors[current];
                if (index < adjacency.Count)
                {
                    stack.Push((current, index + 1));
                    var target = adjacency[index];
                    var ts = state[target];
                    if (ts == 1)
                        backEdges.Add((current, target));
                    else if (ts == 0)
                    {
                        state[target] = 1;
                        stack.Push((target, 0));
                    }
                }
                else
                {
                    state[current] = 2;
                }
            }
        }

        // 前驱表：排除回边（保证环路存在时仍有入口可调度）
        foreach (var conn in effectiveConnections)
        {
            if (!nodeMap.ContainsKey(conn.SourceNodeId) || !nodeMap.ContainsKey(conn.TargetNodeId))
                continue;
            if (!backEdges.Contains((conn.SourceNodeId, conn.TargetNodeId)))
                predecessors[conn.TargetNodeId].Add(conn.SourceNodeId);
        }

        return new CompiledFlowGraph<TNodeItem>(nodes, nodeMap, successors, successorConns, predecessors, incoming, backEdges,
            ComputeLayers(nodes, nodeMap, predecessors, successors));
    }

    /// <summary>Kahn 分层：深度 = max(前驱深度)+1；前驱永不归零的环路节点归入 maxDepth+1 层。</summary>
    private static List<List<TNodeItem>> ComputeLayers<TNodeItem>(
        IReadOnlyList<TNodeItem> nodes,
        Dictionary<string, TNodeItem> nodeMap,
        Dictionary<string, List<string>> predecessors,
        Dictionary<string, List<string>> successors) where TNodeItem : INodeExecutable
    {
        var depth = new Dictionary<string, int>(nodes.Count);
        var inDegree = new Dictionary<string, int>(nodes.Count);
        var queue = new Queue<string>();

        foreach (var n in nodes)
        {
            inDegree[n.Id] = predecessors[n.Id].Count;
            depth[n.Id] = 0;
            if (inDegree[n.Id] == 0)
                queue.Enqueue(n.Id);
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var currentDepth = depth.GetValueOrDefault(current, 0);
            foreach (var neighbor in successors[current])
            {
                var newDepth = Math.Max(depth.GetValueOrDefault(neighbor, 0), currentDepth + 1);
                depth[neighbor] = newDepth;
                inDegree[neighbor]--;
                if (inDegree[neighbor] == 0)
                    queue.Enqueue(neighbor);
            }
        }

        var maxDepth = depth.Count > 0 ? depth.Values.Max() : 0;
        foreach (var (id, degree) in inDegree)
        {
            if (degree > 0)
                depth[id] = maxDepth + 1;
        }

        var layers = new List<List<TNodeItem>>();
        foreach (var group in depth.GroupBy(kv => kv.Value).OrderBy(g => g.Key))
            layers.Add(group.Select(kv => nodeMap[kv.Key]).ToList());

        return layers;
    }
}
