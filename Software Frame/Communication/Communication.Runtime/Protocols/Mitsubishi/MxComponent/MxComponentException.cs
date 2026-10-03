// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：MxComponentException。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core.Protocols.Mitsubishi。导入不代表所有类型都会在运行时加载。
using Communication.Core.Protocols.Mitsubishi;
namespace Communication.Runtime.Protocols.Mitsubishi;

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

    /// <summary>
    /// 创建当前实现提供的对象或结果；对应入口为 CreateMessage。
    /// 正文中的关键协作调用包括 IsNullOrWhiteSpace、Trim；具体条件和先后次序见方法体。
    /// </summary>
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
