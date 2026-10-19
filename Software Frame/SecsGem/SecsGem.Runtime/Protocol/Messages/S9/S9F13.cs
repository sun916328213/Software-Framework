using SecsGem.Core.Order;
using Secs4Net;

namespace SecsGem.Runtime.Order.S9;

/// <summary>会话超时：Order 对应 MEXP，Message 对应 EDID；保留原有字段名兼容调用方。</summary>
public struct SS9F13
{
    public string Order { get; set; }
    public string Message { get; set; }
}

/// <summary>S9F13：会话超时，L[2] A MEXP、A EDID，W=0；不是 T3 的别名。</summary>
[SecsOrder("S9F13")]
internal sealed class S9F13 : SecsOrderBase
{
    public override Item GetSendItem(params object[] data)
    {
        if (data.Length != 1 || data[0] is not SS9F13 value || value.Order is null || value.Message is null) return null!;
        var item = Item.L(Item.A(value.Order), Item.A(value.Message));
        try
        {
            using var message = new SecsMessage(9, 13) { SecsItem = item };
            S9ProtocolCodec.Describe(message);
        }
        catch (ArgumentException) { return null!; }
        // 返回自有副本，不返回上方已释放的验证 Item。
        return Item.L(Item.A(value.Order), Item.A(value.Message));
    }

    public override Item GetReplyItem(SecsMessage message)
    {
        S9ProtocolCodec.Describe(message);
        return null!;
    }
}
