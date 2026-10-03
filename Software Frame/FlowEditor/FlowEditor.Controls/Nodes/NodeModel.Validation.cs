// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：NodeModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、FlowEditor.Core.Nodes、FlowEditor.Controls.Core、FlowEditor.Core、FlowEditor.Runtime。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using FlowEditor.Core.Nodes;
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;

namespace FlowEditor.Controls.Nodes;

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
