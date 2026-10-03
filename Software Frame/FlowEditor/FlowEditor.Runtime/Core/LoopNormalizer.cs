// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：LoopNormalizer。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Core、FlowEditor.Core.Models。导入不代表所有类型都会在运行时加载。
using FlowEditor.Core;
using FlowEditor.Core.Models;

namespace FlowEditor.Runtime;

/// <summary>
/// 循环回边归一化器（编译期预处理）。
/// <para>
/// 背景：早期循环依赖用户在画布上手动把循环体末端接回循环节点的 Next_IN（goto 式回边），
/// 连线横跨整张画布、易断难读。主流做法（Unreal Blueprint 的 ForEachLoop、n8n 的 Loop Over Items）
/// 是循环体末端悬空即可，回边由引擎在编译期自动补全。
/// </para>
/// <para>
/// 本归一化器在 <see cref="CompiledFlowGraph{TNode}.Compile"/> 里最先执行：为每个实现了
/// <see cref="ILoopControlNode"/> 的循环节点做循环体可达性分析，给循环体末端合成虚拟回边。
/// 合成连线只存在于编译结果中（不写入 .flow、不出现在画布上），因此不会污染用户的图。
/// </para>
/// <para>
/// 嵌套循环：按循环体规模从小到大处理（内层优先），同一末端节点只归属最先处理到的循环；
/// 串联的多个循环互不影响。
/// </para>
/// </summary>
public static class LoopNormalizer
{
    /// <summary>合成回边 ID 前缀（运行期与真实连线等价，仅便于日志/诊断区分）。</summary>
    public const string SyntheticConnectionPrefix = "__loopback_";

    /// <summary>内置直通输出端口名，普通节点作循环体末端时优先用它作为回边源端口。</summary>
    private const string NextOutPortName = "Next_Out";

    /// <summary>
    /// 为循环节点补全缺失的回边，返回"实际参与编译"的连线列表（原连线 + 合成回边）。
    /// 图中不存在循环节点时原样返回，零开销。
    /// </summary>
    public static List<ConnectionModel> Normalize<TNode>(
        IReadOnlyList<TNode> nodes,
        IReadOnlyList<ConnectionModel> connections) where TNode : INodeExecutable
    {
        var result = new List<ConnectionModel>(connections);
        if (nodes.Count == 0 || connections.Count == 0) return result;

        var nodeMap = new Dictionary<string, TNode>(nodes.Count);
        foreach (var n in nodes) nodeMap[n.Id] = n;

        // 出边索引：按源端口 / 源节点分组，供循环体可达性分析使用
        var outByPort = new Dictionary<string, List<ConnectionModel>>();
        var outByNode = new Dictionary<string, List<ConnectionModel>>();
        foreach (var c in connections)
        {
            if (!nodeMap.ContainsKey(c.SourceNodeId) || !nodeMap.ContainsKey(c.TargetNodeId))
                continue;

            if (!outByPort.TryGetValue(c.SourcePortId, out var byPort))
                outByPort[c.SourcePortId] = byPort = [];
            byPort.Add(c);

            if (!outByNode.TryGetValue(c.SourceNodeId, out var byNode))
                outByNode[c.SourceNodeId] = byNode = [];
            byNode.Add(c);
        }

        // 1. 收集所有循环节点及其循环体
        var loops = new List<(TNode Node, ILoopControlNode Loop, HashSet<string> Body)>();
        foreach (var node in nodes)
        {
            if (node is not ILoopControlNode loop) continue;

            string loopBackPortId = loop.LoopBackPortId;
            if (string.IsNullOrEmpty(loopBackPortId)) continue;
            if (node.FindPortById(loopBackPortId) == null) continue;

            var bodyPortIds = loop.BodyPortIds;
            if (bodyPortIds == null || bodyPortIds.Count == 0) continue;

            var body = CollectBody(node, bodyPortIds, outByPort, outByNode, nodeMap);
            if (body.Count == 0) continue;

            loops.Add((node, loop, body));
        }

        if (loops.Count == 0) return result;

        // 2. 内层循环优先：循环体越小越内层，先占位可让串联/嵌套场景取到正确归属
        loops.Sort((a, b) => a.Body.Count.CompareTo(b.Body.Count));

        // 3. 为循环体末端合成回边
        var claimed = new HashSet<string>();          // 已归属某循环的末端节点
        var synthetic = new HashSet<(string, string, string)>();

        foreach (var (node, loop, body) in loops)
        {
            string loopBackPortId = loop.LoopBackPortId;

            // 已显式画了回边的末端（老图）不重复合成
            var explicitBack = new HashSet<string>();
            foreach (var c in connections)
            {
                if (c.TargetNodeId == node.Id && body.Contains(c.SourceNodeId))
                    explicitBack.Add(c.SourceNodeId);
            }

            foreach (string endId in body)
            {
                if (explicitBack.Contains(endId)) continue;
                if (!claimed.Add(endId)) continue;

                // 末端判定：不存在指向循环体内部（含循环节点自身）的出边。
                // 指向循环体外部的出边不影响回边合成，仍保证循环可以继续。
                if (outByNode.TryGetValue(endId, out var outs))
                {
                    bool hasInnerEdge = false;
                    foreach (var c in outs)
                    {
                        if (c.TargetNodeId == node.Id || body.Contains(c.TargetNodeId))
                        {
                            hasInnerEdge = true;
                            break;
                        }
                    }
                    if (hasInnerEdge) continue;
                }

                foreach (string portId in PickLoopBackSourcePorts(nodeMap[endId]))
                {
                    if (!synthetic.Add((endId, portId, node.Id))) continue;
                    result.Add(new ConnectionModel
                    {
                        Id = SyntheticConnectionPrefix + Guid.NewGuid().ToString("N"),
                        SourceNodeId = endId,
                        SourcePortId = portId,
                        TargetNodeId = node.Id,
                        TargetPortId = loopBackPortId
                    });
                }
            }
        }

        return result;
    }

    /// <summary>
    /// 从循环体的 Body 端口出发做可达性分析，得到循环体节点集合（不含循环节点自身）。
    /// 遇到回到循环头的连线（显式回边）不再展开，避免把循环头误判为循环体成员。
    /// </summary>
    private static HashSet<string> CollectBody<TNode>(
        TNode loopNode,
        IReadOnlyCollection<string> bodyPortIds,
        Dictionary<string, List<ConnectionModel>> outByPort,
        Dictionary<string, List<ConnectionModel>> outByNode,
        Dictionary<string, TNode> nodeMap) where TNode : INodeExecutable
    {
        var body = new HashSet<string>();
        var queue = new Queue<string>();

        foreach (string portId in bodyPortIds)
        {
            if (!outByPort.TryGetValue(portId, out var conns)) continue;
            foreach (var c in conns)
            {
                if (c.TargetNodeId == loopNode.Id) continue;
                if (!nodeMap.ContainsKey(c.TargetNodeId)) continue;
                if (body.Add(c.TargetNodeId)) queue.Enqueue(c.TargetNodeId);
            }
        }

        while (queue.Count > 0)
    {
        string current = queue.Dequeue();
        if (!outByNode.TryGetValue(current, out var conns)) continue;

        foreach (var c in conns)
        {
            if (c.TargetNodeId == loopNode.Id) continue;
            if (!nodeMap.ContainsKey(c.TargetNodeId)) continue;

            // 只沿控制流边（PortType.Flow）展开，避免 Error/数据端口将退出路径误纳入循环体
            var sourceNode = nodeMap[c.SourceNodeId];
            var sourcePort = sourceNode.FindPortById(c.SourcePortId);
            if (sourcePort == null || sourcePort.DataType != PortType.Flow) continue;

            if (body.Add(c.TargetNodeId)) queue.Enqueue(c.TargetNodeId);
        }
    }

        return body;
    }

    /// <summary>选择末端节点用作回边源的输出端口。</summary>
    private static List<string> PickLoopBackSourcePorts<TNode>(TNode end) where TNode : INodeExecutable
    {
        var ports = new List<string>();

        // 声明了控制流端口的节点（条件分支、循环等）：为每个控制流输出端口各合成一条回边，
        // 由激活边语义决定本次走哪条，避免分支节点作循环体末端时循环被剪枝中断。
        var controlFlow = end.ControlFlowPorts;
        if (controlFlow is { Count: > 0 })
        {
            foreach (string name in controlFlow)
            {
                var port = end.OutputPorts.FirstOrDefault(p => p.Name == name);
                if (port != null) ports.Add(port.Id);
            }
            if (ports.Count > 0) return ports;
        }

        // 普通节点：优先内置 Next_Out，其次第一个输出端口
        var next = end.OutputPorts.FirstOrDefault(p => p.Name == NextOutPortName)
                   ?? end.OutputPorts.FirstOrDefault();
        if (next != null) ports.Add(next.Id);
        return ports;
    }
}
