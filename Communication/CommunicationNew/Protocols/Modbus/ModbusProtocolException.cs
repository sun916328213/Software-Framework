namespace CommunicationNew.Protocols.Modbus;

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
