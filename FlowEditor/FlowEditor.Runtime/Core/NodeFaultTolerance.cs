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

/// <summary>
/// 重试边界（P2）。三层各管一层，禁止多层重试指数放大：
/// <list type="number">
/// <item>L0 协议级恢复：通信/设备/SDK 适配器内部只做单次传输恢复（重连一次、单次读超时），
/// 不得循环重试业务动作； terminal 结果用 <see cref="FlowNodeException"/> 分类上报。</item>
/// <item>L1 业务动作重试：引擎按 <see cref="INodeFaultPolicy"/>（RetryCount/RetryDelayMs）执行，
/// 仅当失败可重试（<see cref="FlowNodeException.IsRetryable"/>）时继续；配置/视觉/取消类失败直接终结本轮重试。</item>
/// <item>L2 流程容错：<see cref="NodeErrorPolicy"/> 决定失败后流程继续还是终止，不触发新的业务重试。</item>
/// </list>
/// </summary>
public static class RetryBoundary
{
    /// <summary>判断失败后是否允许开始下一次业务重试。</summary>
    /// <param name="failure">本轮失败（null 表示成功）。</param>
    /// <param name="attempt">刚完成的 attempt 序号（≥1）。</param>
    /// <param name="maxAttempts">最大 attempt 数（1 + RetryCount）。</param>
    /// <returns>true 表示可继续重试；false 表示终结重试、进入容错处理。</returns>
    public static bool ShouldRetry(Exception? failure, int attempt, int maxAttempts)
    {
        if (failure == null) return false;
        if (attempt >= maxAttempts) return false;
        // L1 规则：明确不可重试的分类失败（配置/视觉/取消）不再浪费重试次数。
        if (failure is FlowNodeException flow && !flow.IsRetryable) return false;
        return true;
    }

    /// <summary>提取失败的稳定错误码；未知异常返回 null（遥测只记录已知分类）。</summary>
    public static string? GetErrorCode(Exception? failure) =>
        failure is FlowNodeException flow ? flow.ErrorCode : null;
}
