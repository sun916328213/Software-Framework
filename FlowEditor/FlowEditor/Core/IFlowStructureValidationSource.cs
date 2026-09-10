namespace FlowEditor.Core;

/// <summary>
/// 能提供自身结构诊断的复合流程节点。静态分析器通过此 seam 收集诊断，
/// 无需依赖任何具体容器节点类型。
/// </summary>
public interface IFlowStructureValidationSource
{
    IEnumerable<FlowDiagnostic> ValidateStructure();
}
