using SecsGem.Core.Order;
using Secs4Net;

namespace SecsGem.Runtime.Order.S9;

/// <summary>兼容旧发送参数；ID 必须是超时事务的 System Bytes，不能取全局最后一条消息编号。</summary>
public struct SS9F9
{
    public SecsMessage SecsMessage { get; set; }
    public ushort DeviceID { get; set; }
    public int ID { get; set; }
}

/// <summary>S9F9：事务应答超时（T3）。</summary>
/// <remarks>
/// 指令格式示例：设备发出 S6F11 W，但未在 T3 内收到对应确认。
/// <code><![CDATA[
/// 'S9F9'                          // 设备→Host，W=0，无应答
/// <B [10] 0x00 0x00 0x86 0x0B 0x00 0x00 0x12 0x34 0x56 0x78> // SHEAD：原发送 S6F11 W 的十字节报头
/// .
/// ]]></code>
/// 字节 0–1：Device ID=0；2：W=1/S=6；3：0x0B 为 F=11；4–5：PType/SType=0；6–9：原 System Bytes。
/// SHEAD 必须取自实际超时发送事务，不是接收报头或全局最后消息编号，不含四字节长度前缀。
/// T3 时长由通信配置决定；与跨事务会话超时 S9F13 不同。
/// </remarks>
[SecsOrder("S9F9")]
internal sealed class S9F9 : S9HeaderError { }
