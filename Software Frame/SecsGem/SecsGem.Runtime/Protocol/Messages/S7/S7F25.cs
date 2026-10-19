using SecsGem.Core.Order;
using Secs4Net;

namespace SecsGem.Runtime.Order.S7;

/// <summary>
/// S7F25：读取指定 PPID 的格式化配方，不切换配方。
/// 本接收方向：S7F25 H→E / S7F26 E→H（标准允许双向事务）。
/// 请求：A PPID；回复：PPID、MDLN、SOFTREV、命令列表；未找到回复 L[0]。
/// </summary>
[SecsOrder("S7F25")]
class S7F25 : SecsOrderBase
{
    /// <summary>S7F25 发送参数，仅指定要读取的 PPID，不包含配方正文。</summary>
    public class S7F25SendData
    {
        /// <summary>ASCII 配方标识符；数字形式的编号也按字符串编码并保留前导零。</summary>
        public string PPID { get; set; } = string.Empty;
    }

    /// <summary>组包：字符串 PPID 或 S7F25SendData，编码为 A；不发送。</summary>
    public override Item GetSendItem(params object[] pData)
    {
        try
        {
            var id = pData.FirstOrDefault() switch
            {
                string value => value,
                S7F25SendData data => data.PPID,
                _ => string.Empty
            };
            ProcessProgramCodec.ReadId(Item.A(id));
            return Item.A(id);
        }
        catch (ArgumentException) { return null!; }
    }

    /// <summary>读取并回复 S7F26；传入有效 S7F26 时更新本地缓存。非法结构返回 null。</summary>
    public override Item GetReplyItem(SecsMessage message)
    {
        try
        {
            if (message.F == 26)
            {
                if (message.SecsItem?.Format == SecsFormat.List && message.SecsItem.Count == 0)
                    return message.SecsItem; // 未找到，不更新本地库
                var received = ProcessProgramCodec.ReadFormatted(message.SecsItem);
                DeviceManager?.UpdateProcessProgramData(received);
                return message.SecsItem!;
            }
            var id = ProcessProgramCodec.ReadId(message.SecsItem);
            var program = DeviceManager?.GetProcessProgramData(id);
            return program == null ? Item.L() : ProcessProgramCodec.WriteFormatted(program);
        }
        catch (ArgumentException ex) { return Fail(ex); }
    }
}

/// <summary>S7F23/S7F26 格式化配方；PPBODY 每项为 L[2] CCODE Item、L[PPARM]，CCODE 不限定类型；不是二进制正文。</summary>
public class ProcessProgramData
{
    /// <summary>配方标识符，当前实现为 1–120 个 ASCII 字符。</summary>
    public string PPID { get; set; } = string.Empty;
    /// <summary>配方关联的设备型号，当前实现为最多 20 个 ASCII 字符；不代表已通过机型兼容校验。</summary>
    public string MDLN { get; set; } = string.Empty;
    /// <summary>配方关联的软件版本，当前实现为最多 20 个 ASCII 字符。</summary>
    public string SOFTREV { get; set; } = string.Empty;
    /// <summary>结构化命令集合；CCODE 和 PPARM 的实际业务含义由双方通信规格约定。</summary>
    public List<object> PPBODY { get; set; } = new();
    /// <summary>本地接收时间元数据，不是 S7F23/S7F26 的线上字段。</summary>
    public DateTime ReceivedTime { get; set; }
}
