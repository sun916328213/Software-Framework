// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：ModbusTcpResponse。成员说明可结合下方具体实现阅读。
namespace Communication.Runtime.Protocols.Modbus;

/// <summary>已验证关联号、协议标识和长度的 Modbus TCP 响应。</summary>
public sealed record ModbusTcpResponse(
    byte UnitId,
    byte FunctionCode,
    ReadOnlyMemory<byte> Data);
