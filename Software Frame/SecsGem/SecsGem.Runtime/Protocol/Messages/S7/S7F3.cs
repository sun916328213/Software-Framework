using SecsGem.Core.Order;
using Secs4Net;

namespace SecsGem.Runtime.Order.S7;

/// <summary>
/// S7F3：传送二进制配方；本实现新建或覆盖内存正文，不切换配方。
/// 本接收方向：S7F3 H→E / S7F4 E→H（标准允许双向事务）。
/// 请求：PPID、B PPBODY；回复：ACKC7，0=接受。不解析 JSON、不持久化、不自动发变更事件。
/// </summary>
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
