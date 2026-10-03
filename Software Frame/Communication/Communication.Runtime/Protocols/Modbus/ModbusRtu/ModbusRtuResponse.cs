// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：ModbusRtuResponse。成员说明可结合下方具体实现阅读。
namespace Communication.Runtime.Protocols.Modbus;

/// <summary>通过长度和 CRC 校验的 Modbus RTU 响应。</summary>
public sealed record ModbusRtuResponse(
    byte UnitId,
    byte FunctionCode,
    ReadOnlyMemory<byte> Data);
