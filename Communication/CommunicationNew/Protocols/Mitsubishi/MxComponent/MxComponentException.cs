namespace CommunicationNew.Protocols.Mitsubishi;

/// <summary>表示 MX Component 返回了非零厂商错误码或 COM HRESULT。</summary>
public sealed class MxComponentException : IOException
{
    /// <summary>使用操作名称和原始错误码创建异常。</summary>
    public MxComponentException(int errorCode, string operation)
        : this(errorCode, operation, vendorMessage: null)
    {
    }

    /// <summary>使用操作名称、原始错误码和厂商错误文本创建异常。</summary>
    public MxComponentException(int errorCode, string operation, string? vendorMessage)
        : base(CreateMessage(errorCode, operation, vendorMessage))
    {
        ErrorCode = errorCode;
    }

    /// <summary>MX Component 原始返回码。</summary>
    public int ErrorCode { get; }

    private static string CreateMessage(
        int errorCode,
        string operation,
        string? vendorMessage)
    {
        const int classNotRegistered = unchecked((int)0x80040154);
        var detail = !string.IsNullOrWhiteSpace(vendorMessage)
            ? $"MX Component 消息: {vendorMessage.Trim()}。"
            : errorCode == classNotRegistered
                ? "MX Component COM 类未注册；请安装并授权 MX Component V5 x64，且确认逻辑站已配置。"
                : "请根据 MX Component 错误码表检查逻辑站、授权和 PLC 状态。";
        return $"MX Component 操作 {operation} 失败，错误码: 0x{errorCode:X8}。{detail}";
    }
}
