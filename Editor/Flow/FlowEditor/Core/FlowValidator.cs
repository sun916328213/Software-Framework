using FlowEditor.Models;
using FlowEditor.Nodes;
using FlowEditor.ViewModels;

namespace FlowEditor.Core;

/// <summary>诊断严重级别。</summary>
public enum FlowDiagnosticSeverity
{
    /// <summary>错误：阻止执行。</summary>
    Error,
    /// <summary>警告：不阻止执行。</summary>
    Warning,
    /// <summary>提示：仅供参考。</summary>
    Info
}

/// <summary>单条诊断。</summary>
/// <param name="Severity">级别。</param>
/// <param name="Code">诊断代码（如 E-IN-REQ）。</param>
/// <param name="Message">人类可读描述。</param>
public sealed record FlowDiagnostic(FlowDiagnosticSeverity Severity, string Code, string Message)
{
    public override string ToString() => $"[{Code}] {Message}";
}

/// <summary>静态分析报告。</summary>
public sealed class FlowValidationReport
{
    public List<FlowDiagnostic> Diagnostics { get; } = new();

    public bool HasErrors => Diagnostics.Any(d => d.Severity == FlowDiagnosticSeverity.Error);

    public int ErrorCount => Diagnostics.Count(d => d.Severity == FlowDiagnosticSeverity.Error);
}

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
    public static FlowValidationReport Analyze(
        IReadOnlyList<NodeViewModel> nodes,
        IReadOnlyList<ConnectionModel> connections)
    {
        var report = new FlowValidationReport();
        if (nodes.Count == 0)
            return report;

        var graph = CompiledFlowGraph<NodeViewModel>.Compile(nodes, connections);

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

        foreach (var nodeVm in nodes)
        {
            var node = nodeVm.Model;

            // IN 必连检查
            if (node.IsInEnabled)
            {
                var inPort = node.InputPorts.FirstOrDefault(p => p.Name == "Next_IN");
                if (inPort != null && graph.IncomingConnections[nodeVm.Id].Count == 0)
                {
                    report.Diagnostics.Add(new FlowDiagnostic(
                        FlowDiagnosticSeverity.Error, "E-IN-REQ",
                        $"节点 [{node.Name}] 的 IN 端口未连接，请先连线"));
                }
            }

            // 不可达 / 完全孤立节点：
            // - 非入口且从入口不可达
            // - 或无任何连线相连（零度数节点会被入口判定误收，需单独识别）
            var hasOutgoing = graph.Successors[nodeVm.Id].Count > 0;
            var hasIncoming = graph.IncomingConnections[nodeVm.Id].Count > 0;
            if (!reachable.Contains(nodeVm.Id) || (!hasOutgoing && !hasIncoming))
            {
                report.Diagnostics.Add(new FlowDiagnostic(
                    FlowDiagnosticSeverity.Warning, "W-ORPHAN",
                    $"节点 [{node.Name}] 从任何入口均不可达，将不会被执行"));
            }

            // 连线级检查：类型兼容 + 端口存在 + 绑定属性有效
            foreach (var conn in graph.IncomingConnections[nodeVm.Id])
            {
                if (!graph.NodeMap.TryGetValue(conn.SourceNodeId, out var sourceNode))
                    continue;
                var sourcePort = sourceNode.FindPort(conn.SourcePortId);
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
                $"流程图包含 {graph.BackEdges.Count} 条回边（环路），将以路径追踪模式执行；请确保环路可通过循环节点或分支自然终止"));
        }

        return report;
    }

    private static string Short(string? name, string fallback) => name ?? fallback;

    // 绑定属性检查缓存：(类型, 属性名, 是否要求可写) → 结果
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(Type, string, bool), bool> PropertyCheckCache = new();

    private static bool HasReadableWritableProperty(NodeModel node, string propertyName, bool writable)
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
