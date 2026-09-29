namespace FlowEditor.Core;

/// <summary>
/// 标准节点异常（P0）。至少包含稳定错误码、类别、可重试标记、节点标识与内部异常。
/// 用户界面仍显示中文 <see cref="Exception.Message"/>，但 Runtime 只按
/// <see cref="ErrorCode"/> / <see cref="Category"/> / <see cref="IsRetryable"/> 做决策。
/// </summary>
public class FlowNodeException : Exception
{
    /// <summary>稳定错误码，如 "E-DEVICE-OFFLINE"、"E-VISION-NOTFOUND"、"E-CONFIG-RANGE"。</summary>
    public string ErrorCode { get; }

    /// <summary>错误类别，用于可重试判断、报警分类与 MES/Secs 对接。</summary>
    public NodeErrorCategory Category { get; }

    /// <summary>是否允许 Runtime/调用方重试。取消一律不可重试。</summary>
    public bool IsRetryable { get; }

    /// <summary>抛出异常的节点 Id（可为空，用于尚未绑定节点的构造期错误）。</summary>
    public string? NodeId { get; }

    /// <summary>使用错误码、类别与消息创建异常。</summary>
    public FlowNodeException(
        string errorCode,
        NodeErrorCategory category,
        string message,
        bool retryable = false,
        string? nodeId = null,
        Exception? inner = null)
        : base(message, inner)
    {
        ErrorCode = string.IsNullOrWhiteSpace(errorCode) ? "E-UNKNOWN" : errorCode;
        Category = category;
        IsRetryable = category == NodeErrorCategory.Cancelled ? false : retryable;
        NodeId = nodeId;
    }

    /// <summary>配置错误（参数越界、缺输入、文件缺失等），默认不可重试。</summary>
    public static FlowNodeException Config(string errorCode, string message, string? nodeId = null, Exception? inner = null)
        => new(errorCode, NodeErrorCategory.Configuration, message, retryable: false, nodeId: nodeId, inner: inner);

    /// <summary>设备离线/连接失败，默认可重试（由上层决定重试边界，避免多层指数放大）。</summary>
    public static FlowNodeException DeviceOffline(string message, string? nodeId = null, Exception? inner = null, string errorCode = "E-DEVICE-OFFLINE")
        => new(errorCode, NodeErrorCategory.DeviceOffline, message, retryable: true, nodeId: nodeId, inner: inner);

    /// <summary>超时，默认可重试。</summary>
    public static FlowNodeException Timeout(string message, string? nodeId = null, Exception? inner = null, string errorCode = "E-TIMEOUT")
        => new(errorCode, NodeErrorCategory.Timeout, message, retryable: true, nodeId: nodeId, inner: inner);

    /// <summary>取消，由 <see cref="OperationCanceledException"/> 转换，不重试。</summary>
    public static FlowNodeException Cancelled(string? nodeId = null, Exception? inner = null)
        => new("E-CANCELLED", NodeErrorCategory.Cancelled, "节点执行已取消。", retryable: false, nodeId: nodeId, inner: inner);

    /// <summary>视觉业务失败，默认不可重试。</summary>
    public static FlowNodeException Vision(string errorCode, string message, string? nodeId = null, Exception? inner = null)
        => new(errorCode, NodeErrorCategory.Vision, message, retryable: false, nodeId: nodeId, inner: inner);

    /// <summary>将任意异常归一化为 <see cref="FlowNodeException"/>：已是本类型则原样返回；取消转为 Cancelled；其余转为 Unknown。</summary>
    public static FlowNodeException FromUnknown(Exception ex, string? nodeId = null)
    {
        if (ex is FlowNodeException flow)
            return flow;
        if (ex is OperationCanceledException)
            return Cancelled(nodeId, ex);
        if (ex is NodeTimeoutException timeout)
            return Timeout(timeout.Message, nodeId, timeout);
        return new FlowNodeException("E-UNKNOWN", NodeErrorCategory.Unknown, ex.Message, retryable: false, nodeId: nodeId, inner: ex);
    }
}
