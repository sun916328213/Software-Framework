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

/// <summary>S9F9：事务应答超时，B[10] SHEAD，W=0；T3 时长由通信配置决定。</summary>
[SecsOrder("S9F9")]
internal sealed class S9F9 : S9HeaderError { }
