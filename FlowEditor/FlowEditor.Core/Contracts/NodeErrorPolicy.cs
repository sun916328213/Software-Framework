namespace FlowEditor.Core;

/// <summary>节点失败处理策略。</summary>
public enum NodeErrorPolicy
{
    /// <summary>默认：节点失败后终止当前流程。</summary>
    FailFast,

    /// <summary>记录错误后继续调度下游节点。</summary>
    ContinueOnError,

    /// <summary>仅激活错误输出端口；未连接时退化为继续执行。</summary>
    RouteToErrorPort
}
