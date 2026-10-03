// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：ModbusProtocolException。成员说明可结合下方具体实现阅读。
namespace Communication.Runtime.Protocols.Modbus;

/// <summary>表示设备返回 Modbus 异常响应或收到无效协议帧。</summary>
public sealed class ModbusProtocolException : IOException
{
    /// <summary>使用错误说明创建协议异常。</summary>
    public ModbusProtocolException(string message)
        : base(message)
    {
    }

    /// <summary>设备返回的 Modbus 异常码；本地帧校验错误时为空。</summary>
    public byte? ExceptionCode { get; init; }
}
