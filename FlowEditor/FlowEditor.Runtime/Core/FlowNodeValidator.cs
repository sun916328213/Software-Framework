using FlowEditor.Models;

namespace FlowEditor.Core;

/// <summary>
/// 整图验证入口（P1）。先做结构分析（<see cref="FlowValidator.Analyze"/>：缺连线、类型不兼容、
/// 悬空端口、孤立节点等），再逐节点调用 <see cref="IFlowNodeValidatable.ValidateAsync"/>
/// 做自身配置验证（缺设备、参数错误、缺插件等），合并为一份报告。
/// <para/>
/// 验收：Workbench 的"验证流程"调用此入口，在真正执行设备动作前列出全部可发现问题。
/// </summary>
public static class FlowNodeValidator
{
    /// <summary>验证整图：结构问题 + 各节点自身问题合并返回。</summary>
    /// <param name="nodes">待验证节点。</param>
    /// <param name="connections">待验证连线。</param>
    /// <param name="services">受限服务容器（透传给各节点验证上下文，可为空）。</param>
    /// <param name="variables">流程变量快照（透传，可为空）。</param>
    /// <param name="ct">取消令牌。</param>
    public static async Task<FlowValidationReport> ValidateAsync<TNode>(
        IReadOnlyList<TNode> nodes,
        IReadOnlyList<ConnectionModel> connections,
        IServiceProvider? services = null,
        IReadOnlyDictionary<string, object?>? variables = null,
        CancellationToken ct = default) where TNode : INodeExecutable
    {
        var report = FlowValidator.Analyze(
            nodes.Cast<INodeExecutable>().ToList(), connections);
        if (nodes.Count == 0) return report;

        foreach (var node in nodes)
        {
            ct.ThrowIfCancellationRequested();
            if (node is not IFlowNodeValidatable validatable) continue;
            var ctx = new NodeValidationContext(node.Id, node.Name, services, variables, ct);
            IEnumerable<FlowDiagnostic> diagnostics;
            try
            {
                diagnostics = await validatable.ValidateAsync(ctx);
            }
            catch (Exception ex)
            {
                // 验证器自身不得炸掉整图验证：转为一条错误诊断。
                report.Diagnostics.Add(new FlowDiagnostic(
                    FlowDiagnosticSeverity.Error, "E-VALIDATE-CRASH",
                    $"节点 [{node.Name}] 验证器异常：{ex.GetType().Name}: {ex.Message}"));
                continue;
            }
            if (diagnostics == null) continue;
            foreach (var diagnostic in diagnostics)
            {
                if (diagnostic != null)
                    report.Diagnostics.Add(diagnostic);
            }
        }

        return report;
    }
}
