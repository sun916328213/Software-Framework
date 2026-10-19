using SecsGem.Core.Order;
using Secs4Net;

namespace SecsGem.Runtime.Order.S9;

/// <summary>会话超时：Order 对应 MEXP，Message 对应 EDID；保留原有字段名兼容调用方。</summary>
public struct SS9F13
{
    public string Order { get; set; }
    public string Message { get; set; }
}

/// <summary>S9F13：会话超时，不是 T3 事务应答超时。</summary>
/// <remarks>
/// 指令格式示例：DEMO-FAST 已得到 S7F2 许可，却没有在会话期限内发送 S7F3。
/// <code><![CDATA[
/// 'S9F13'                         // 设备→Host，W=0，无应答
/// <L [2]                          // MEXP、EDID，不是 MHEAD/SHEAD
///     <A [4] 'S7F3'>               // MEXP：预期消息，非空 ASCII；对应 SS9F13.Order
///     <A [9] 'DEMO-FAST'>          // EDID：预期数据标识，本场景为 PPID；对应 SS9F13.Message
/// >
/// .
/// ]]></code>
/// ASCII 数量按实际内容填写；EDID 可空，不是自由异常说明。当前工作台等待 30 秒，项目可显式配置。
/// 本通知自身的 System Bytes 不能用于结束无关 T3；超时不创建或自动切换配方。
/// </remarks>
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
