using SecsGem.Core.Order;
using Secs4Net;

namespace SecsGem.Runtime.Order.S7;

/// <summary>
/// S7F5：读取指定 PPID 的二进制配方，不修改或切换配方。
/// 本接收方向：S7F5 H→E / S7F6 E→H（标准允许双向事务）。
/// 请求：A PPID；回复：L[2] PPID、B PPBODY；未找到回复 L[0]。
/// </summary>
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
