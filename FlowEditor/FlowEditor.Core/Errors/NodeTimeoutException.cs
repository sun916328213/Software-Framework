namespace FlowEditor.Core;

/// <summary>节点执行超出 TimeoutMs 限制时抛出。</summary>
public class NodeTimeoutException : Exception
{
    /// <summary>使用超时诊断消息创建异常。</summary>
    /// <param name="message">描述节点及超时限制的消息。</param>
    public NodeTimeoutException(string message) : base(message) { }
}
