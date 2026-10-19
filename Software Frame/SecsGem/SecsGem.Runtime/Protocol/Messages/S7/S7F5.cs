using SecsGem.Core.Order;
using Secs4Net;

namespace SecsGem.Runtime.Order.S7;

/// <summary>
/// S7F5：读取指定 PPID 的二进制配方，不修改或切换配方。
/// 本接收方向：S7F5 H→E / S7F6 E→H（标准允许双向事务）。
/// 请求：A PPID；回复：L[2] PPID、B PPBODY；未找到回复 L[0]。
/// </summary>
/// <remarks>
/// 指令格式示例：读取工作台 DEMO-FAST；请求是裸 ASCII PPID，不套 L[1]。
/// <code><![CDATA[
/// 'S7F5'W                         // Host→设备，请求读取二进制配方
/// <A [9] 'DEMO-FAST'>              // PPID：要读取的编号
/// .
/// 'S7F6'                          // 设备→Host，返回配方
/// <L [2]                          // PPID、PPBODY
///     <A [9] 'DEMO-FAST'>          // PPID：实际返回的编号
///     <B [62] 0x7B 0x22 0x76 0x65 0x72 0x73 0x69 0x6F 0x6E 0x22 0x3A 0x31 0x2C 0x22 0x65 0x78 0x70 0x6F 0x73 0x75 0x72 0x65 0x55 0x73 0x22 0x3A 0x35 0x30 0x30 0x30 0x2C 0x22 0x67 0x61 0x69 0x6E 0x22 0x3A 0x31 0x2E 0x35 0x2C 0x22 0x73 0x70 0x65 0x65 0x64 0x4D 0x6D 0x50 0x65 0x72 0x53 0x65 0x63 0x22 0x3A 0x31 0x30 0x30 0x7D> // PPBODY：示例配方的完整 JSON 字节
/// >
/// .
/// ]]></code>
/// 未找到时 S7F6 正文为 L[0]；不是 B[0] 或伪造空配方。正文长度由实际存储数据决定，读取不切换配方。
/// </remarks>
[SecsOrder("S7F5")]
class S7F5 : SecsOrderBase
{
    /// <summary>组包：字符串 PPID，直接编码为 A；不发送。</summary>
    public override Item GetSendItem(params object[] data)
    {
        try
        {
            if (data.Length != 1 || data[0] is not string id) return null!;
            ProcessProgramCodec.ReadId(Item.A(id));
            return Item.A(id);
        }
        catch (ArgumentException) { return null!; }
    }

    /// <summary>读取内存正文并回复 S7F6；非法 PPID 返回 null，走 S9F7。</summary>
    public override Item GetReplyItem(SecsMessage message)
    {
        try
        {
            var id = ProcessProgramCodec.ReadId(message.SecsItem);
            var body = DeviceManager?.GetUnformattedProcessProgram(id);
            return body == null ? Item.L() : Item.L(Item.A(id), Item.B(body));
        }
        catch (ArgumentException) { return null!; }
    }
}
