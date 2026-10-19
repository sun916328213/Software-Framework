using SecsGem.Core.Order;
using Secs4Net;

namespace SecsGem.Runtime.Order.S7;

/// <summary>
/// S7F3：传送二进制配方；本实现新建或覆盖内存正文，不切换配方。
/// 本接收方向：S7F3 H→E / S7F4 E→H（标准允许双向事务）。
/// 请求：PPID、B PPBODY；回复：ACKC7，0=接受。不解析 JSON、不持久化、不自动发变更事件。
/// </summary>
/// <remarks>
/// 指令格式示例：下面 B[62] 为工作台 DEMO-FAST 的完整 UTF-8 JSON，不是 A 字符串。
/// <code><![CDATA[
/// 'S7F3'W                         // Host→设备，发送配方正文，请求确认
/// <L [2]                          // PPID、PPBODY
///     <A [9] 'DEMO-FAST'>          // PPID：配方编号
///     <B [62] 0x7B 0x22 0x76 0x65 0x72 0x73 0x69 0x6F 0x6E 0x22 0x3A 0x31 0x2C 0x22 0x65 0x78 0x70 0x6F 0x73 0x75 0x72 0x65 0x55 0x73 0x22 0x3A 0x35 0x30 0x30 0x30 0x2C 0x22 0x67 0x61 0x69 0x6E 0x22 0x3A 0x31 0x2E 0x35 0x2C 0x22 0x73 0x70 0x65 0x65 0x64 0x4D 0x6D 0x50 0x65 0x72 0x53 0x65 0x63 0x22 0x3A 0x31 0x30 0x30 0x7D> // PPBODY：62 个二进制字节
/// >
/// .
/// 'S7F4'                          // 设备→Host，下载应答
/// <B [1] 0x00>                    // ACKC7：0=协议入库接受，不代表应用工艺
/// .
/// ]]></code>
/// 解码内容：{"version":1,"exposureUs":5000,"gain":1.5,"speedMmPerSec":100}。
/// JSON 只是工作台模型示例，Runtime 对 PPBODY 不限制为 JSON；不能把 B[62] 当成 62 个参数。下载不切换当前配方。
/// </remarks>
[SecsOrder("S7F3")]
class S7F3 : SecsOrderBase
{
    /// <summary>组包：字符串 PPID、byte[] 正文；不发送。</summary>
    public override Item GetSendItem(params object[] data)
    {
        if (data.Length != 2 || data[0] is not string id || data[1] is not byte[] body) return null!;
        try
        {
            ProcessProgramCodec.ReadId(Item.A(id));
            return Item.L(Item.A(id), Item.B(body.ToArray()));
        }
        catch (ArgumentException) { return null!; }
    }

    /// <summary>保存内存正文并回复 S7F4；非法结构返回 null，走 S9F7。</summary>
    public override Item GetReplyItem(SecsMessage message)
    {
        try
        {
            ProcessProgramCodec.RequireList(message.SecsItem, 2);
            var items = message.SecsItem!.Items;
            var id = ProcessProgramCodec.ReadId(items[0]);
            if (items[1].Format != SecsFormat.Binary) return null!;
            if (DeviceManager == null) return Item.B(1);
            DeviceManager.StoreUnformattedProcessProgram(id, items[1].GetMemory<byte>().ToArray());
            return Item.B(0);
        }
        catch (ArgumentException) { return null!; }
    }
}
