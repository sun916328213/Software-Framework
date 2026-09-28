namespace CommunicationNew.Protocols.Modbus;

/// <summary>已验证关联号、协议标识和长度的 Modbus TCP 响应。</summary>
public sealed record ModbusTcpResponse(
    byte UnitId,
    byte FunctionCode,
    ReadOnlyMemory<byte> Data);
