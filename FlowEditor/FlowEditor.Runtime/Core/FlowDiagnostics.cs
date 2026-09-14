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

/// <summary>单条流程诊断。</summary>
/// <param name="Severity">级别。</param>
/// <param name="Code">稳定诊断代码。</param>
/// <param name="Message">人类可读描述。</param>
public sealed record FlowDiagnostic(FlowDiagnosticSeverity Severity, string Code, string Message)
{
    /// <summary>生成包含稳定代码的显示文本。</summary>
    public override string ToString() => $"[{Code}] {Message}";
}

/// <summary>一次静态分析产生的完整诊断报告。</summary>
public sealed class FlowValidationReport
{
    /// <summary>获取全部诊断，按分析发现顺序排列。</summary>
    public List<FlowDiagnostic> Diagnostics { get; } = [];

    /// <summary>获取报告是否包含阻止执行的错误。</summary>
    public bool HasErrors => Diagnostics.Any(diagnostic =>
        diagnostic.Severity == FlowDiagnosticSeverity.Error);

    /// <summary>获取阻止执行的错误数量。</summary>
    public int ErrorCount => Diagnostics.Count(diagnostic =>
        diagnostic.Severity == FlowDiagnosticSeverity.Error);
}

/// <summary>能提供自身结构诊断的复合流程节点。</summary>
public interface IFlowStructureValidationSource
{
    /// <summary>校验节点自身配置，并返回不依赖外部连线的诊断。</summary>
    IEnumerable<FlowDiagnostic> ValidateStructure();
}

/// <summary>需要执行前结构校验的节点附加能力。</summary>
public interface IFlowValidationNode : INodeExecutable
{
    /// <summary>获取节点是否要求连接标准控制流输入端口。</summary>
    bool RequiresFlowInput { get; }
}

/// <summary>标识需要验证多分支收束规则的并行分叉节点。</summary>
public interface IParallelForkNode : INodeExecutable
{
}
