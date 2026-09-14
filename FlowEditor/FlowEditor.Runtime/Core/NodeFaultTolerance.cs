namespace FlowEditor.Core;

/// <summary>
/// 节点失败处理策略（参考 Node-RED catch 节点与 Blueprint 错误处理流）。
/// </summary>
public enum NodeErrorPolicy
{
    /// <summary>默认：任一节点失败立即终止整个流程（原有行为）。</summary>
    FailFast,

    /// <summary>
    /// 记录错误后流程继续：该节点标记 Error，其下游照常调度（使用上游遗留的输入值）。
    /// </summary>
    ContinueOnError,

    /// <summary>
    /// 错误路由：仅激活名为 "Error" 的输出端口（可携带异常消息作为端口值），
    /// 正常出边全部剪枝；Error 端口未接线时退化为 ContinueOnError。
    /// </summary>
    RouteToErrorPort
}

/// <summary>节点执行超出 TimeoutMs 限制时抛出。</summary>
public class NodeTimeoutException : Exception
{
    /// <summary>使用超时诊断消息创建异常。</summary>
    /// <param name="message">描述节点及超时限制的消息。</param>
    public NodeTimeoutException(string message) : base(message) { }
}
