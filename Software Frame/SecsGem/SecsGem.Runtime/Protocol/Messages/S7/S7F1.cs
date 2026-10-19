using SecsGem.Core.Order;
using Secs4Net;

namespace SecsGem.Runtime.Order.S7;

/// <summary>
/// S7F1：询问是否允许接收配方，不传正文、不切换配方。
/// 本接收方向：S7F1 H→E / S7F2 E→H（标准允许双向事务）。
/// 请求：PPID、字节长度；回复：PPGNT，0=允许。本实现仅做基本许可判断。
/// </summary>
/// <remarks>
/// 指令格式示例：申请下载工作台示例配方的 62 字节 JSON 正文；许可不表示已入库或应用。
/// <code><![CDATA[
/// 'S7F1'W                         // Host→设备，申请配方下载，请求应答
/// <L [2]                          // PPID、LENGTH
///     <A [9] 'DEMO-FAST'>          // PPID：ASCII 配方编号，保留前导零
///     <U4 [1] 62>                 // LENGTH：PPBODY 实际字节数，不含 Item/HSMS 头
/// >
/// .
/// 'S7F2'                          // 设备→Host，下载许可应答
/// <B [1] 0x00>                    // PPGNT：0=允许；长度拒绝为 2
/// .
/// ]]></code>
/// 类型 U4 为本例；当前 LENGTH 接受单个无符号数。启用容量策略时可预拒绝，获准后会话等待由宿主配置。
/// </remarks>
[SecsOrder("S7F1")]
class S7F1 : SecsOrderBase
{
    /// <summary>组包：字符串 PPID、uint 字节长度；不发送。</summary>
    public override Item GetSendItem(params object[] data)
    {
        if (data.Length != 2 || data[0] is not string id || data[1] is not uint length) return null!;
        try
        {
            ProcessProgramCodec.ReadId(Item.A(id));
            return Item.L(Item.A(id), Item.U4(length));
        }
        catch (ArgumentException) { return null!; }
    }

    /// <summary>校验加载询问并回复 S7F2；非法结构返回 null，走 S9F7。</summary>
    public override Item GetReplyItem(SecsMessage message)
    {
        try
        {
            ProcessProgramCodec.RequireList(message.SecsItem, 2);
            var items = message.SecsItem!.Items;
            ProcessProgramCodec.ReadId(items[0]);
            if (items[1].Count != 1) return null!;
            ulong length = items[1].Format switch
            {
                SecsFormat.U1 => items[1].GetMemory<byte>().Span[0],
                SecsFormat.U2 => items[1].GetMemory<ushort>().Span[0],
                SecsFormat.U4 => items[1].GetMemory<uint>().Span[0],
                SecsFormat.U8 => items[1].GetMemory<ulong>().Span[0],
                _ => throw new ArgumentException("LENGTH 必须为单个无符号整数")
            };
            // 仅表明内存配方服务可接收；不创建空配方，不选择或执行配方。
            return Item.B(DeviceManager == null ? (byte)5 : length > (ulong)Array.MaxLength ? (byte)2 : (byte)0);
        }
        catch (ArgumentException) { return null!; }
    }
}
