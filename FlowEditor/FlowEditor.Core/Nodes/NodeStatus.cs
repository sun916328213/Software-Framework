namespace FlowEditor.Nodes;

/// <summary>节点执行状态。</summary>
public enum NodeStatus
{
    /// <summary>空闲，未执行。</summary>
    Idle,

    /// <summary>正在执行。</summary>
    Running,

    /// <summary>执行成功。</summary>
    Success,

    /// <summary>执行异常。</summary>
    Error
}
