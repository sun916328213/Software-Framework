namespace CommunicationNew.Protocols.Modbus;

/// <summary>通过长度和 CRC 校验的 Modbus RTU 响应。</summary>
public sealed record ModbusRtuResponse(
    byte UnitId,
    byte FunctionCode,
    ReadOnlyMemory<byte> Data);
