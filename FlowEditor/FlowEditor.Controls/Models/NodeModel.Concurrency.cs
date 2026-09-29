using FlowEditor.Core;

namespace FlowEditor.Nodes;

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
