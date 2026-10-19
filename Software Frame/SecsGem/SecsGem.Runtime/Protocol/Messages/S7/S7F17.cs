using SecsGem.Core.Order;
using Secs4Net;

namespace SecsGem.Runtime.Order.S7;

/// <summary>
/// S7F17：删除指定内存配方；空列表删除全部，需谨慎操作。
/// 方向：S7F17 H→E / S7F18 E→H。
/// 请求：L[n] PPID；回复：ACKC7，0=接受、4=PPID 不存在且整批不删除。尚无生产删除保护。
/// </summary>
[SecsOrder("S7F17")]
class S7F17 : SecsOrderBase
{
    /// <summary>组包：PPID 参数列表或 string[]；空参数表示全部。不执行或发送删除。</summary>
    public override Item GetSendItem(params object[] data)
    {
        try
        {
            string[] ids;
            if (data.Length == 1 && data[0] is string[] list) ids = list;
            else if (data.All(value => value is string)) ids = data.Cast<string>().ToArray();
            else return null!;
            foreach (var id in ids) ProcessProgramCodec.ReadId(Item.A(id));
            return Item.L(ids.Select(Item.A));
        }
        catch (ArgumentException) { return null!; }
    }

    /// <summary>校验后删除内存配方并回复 S7F18；非法结构返回 null，走 S9F7。</summary>
    public override Item GetReplyItem(SecsMessage message)
    {
        try
        {
            if (message.SecsItem?.Format != SecsFormat.List) return null!;
            // 完整解析后才修改；错误请求不得造成部分删除。
            var ids = message.SecsItem.Items.Select(ProcessProgramCodec.ReadId).ToArray();
            return Item.B(DeviceManager?.DeleteProcessPrograms(ids) ?? (byte)1);
        }
        catch (ArgumentException) { return null!; }
    }
}
