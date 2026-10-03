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

/// <summary>NodeModel 的 P1 并发面：默认不约束（保持现有调度行为），领域节点按需重写。</summary>
public abstract partial class NodeModel : INodeExecutionAffinity, INodeConcurrency
{
    /// <summary>P1：节点执行亲和性，默认后台执行（与引擎 Task.Run 调度一致）。</summary>
    public virtual NodeExecutionAffinity Affinity => NodeExecutionAffinity.Background;

    /// <summary>P1：节点并发模式，默认可重入（与现有行为一致）。</summary>
    public virtual NodeConcurrencyMode ConcurrencyMode => NodeConcurrencyMode.Reentrant;

    /// <summary>P1：占用的资源键；ResourceExclusive 模式下相同键全局串行。默认空。</summary>
    public virtual IEnumerable<string> GetResourceKeys() => [];
}
