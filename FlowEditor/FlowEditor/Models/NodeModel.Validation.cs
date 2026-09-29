using FlowEditor.Core;

namespace FlowEditor.Nodes;

/// <summary>NodeModel 的 P1 验证面：默认无自身配置问题，子类按需重写。</summary>
public abstract partial class NodeModel : IFlowNodeValidatable
{
    /// <summary>
    /// P1 验证入口：校验节点自身配置（缺设备、参数错误、缺插件等），不依赖连线。
    /// 默认返回空（验证通过），既有节点零改动兼容。
    /// 实现要求：只读验证，不得执行设备动作、不得修改节点状态。
    /// </summary>
    public virtual Task<IEnumerable<FlowDiagnostic>> ValidateAsync(NodeValidationContext ctx)
        => Task.FromResult<IEnumerable<FlowDiagnostic>>([]);

    /// <summary>构造一条指向本节点的错误诊断。</summary>
    protected FlowDiagnostic ValidationError(string code, string message) =>
        new(FlowDiagnosticSeverity.Error, code, $"节点 [{Name}] {message}");

    /// <summary>构造一条指向本节点的警告诊断。</summary>
    protected FlowDiagnostic ValidationWarning(string code, string message) =>
        new(FlowDiagnosticSeverity.Warning, code, $"节点 [{Name}] {message}");
}
