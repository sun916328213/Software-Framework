using SecsGem.Core.Order;
using Secs4Net;

namespace SecsGem.Runtime.Order.S7;

/// <summary>
/// S7F23：传送格式化配方；本实现新建或覆盖内存命令，不切换配方。
/// 本接收方向：S7F23 H→E / S7F24 E→H（标准允许双向事务）。
/// 请求：PPID、MDLN、SOFTREV、命令列表；回复：ACKC7，0=接受。仅校验结构，不校验工艺含义。
/// </summary>
[SecsOrder("S7F23")]
class S7F23 : SecsOrderBase
{
    /// <summary>格式化配方发送数据，沿用 ProcessProgramData 的四字段与命令列表结构。</summary>
    public class S7F23SendData : ProcessProgramData { }

    /// <summary>组包：ProcessProgramData 或 S7F23SendData；不发送。</summary>
    public override Item GetSendItem(params object[] pData)
    {
        try
        {
            return pData.Length == 1 && pData[0] is ProcessProgramData data
                ? ProcessProgramCodec.WriteFormatted(data) : null!;
        }
        catch (ArgumentException) { return null!; }
    }

    /// <summary>保存 S7F23 并回复 S7F24；传入 S7F24 时仅解析结果。非法结构返回 null。</summary>
    public override Item GetReplyItem(SecsMessage message)
    {
        if (message.F == 24)
        {
            var ack = message.SecsItem;
            if (ack?.Format != SecsFormat.Binary || ack.Count != 1) return null!;
            byte code = ack.GetMemory<byte>().Span[0];
            DeviceManager?.OnProcessProgramSendResult(code == 0, code switch
            {
                0 => "接受", 1 => "无权限", 2 => "长度错误", 3 => "矩阵溢出",
                4 => "PPID 不存在", 5 => "模式不支持", 6 => "稍后执行",
                _ => $"保留应答码 {code}"
            });
            return ack;
        }
        try
        {
            var program = ProcessProgramCodec.ReadFormatted(message.SecsItem);
            if (DeviceManager == null) return Item.B(1);
            DeviceManager.UpdateProcessProgramData(program);
            return Item.B(0);
        }
        catch (ArgumentException ex) { return Fail(ex); } // 记录具体字段原因，仍返回 S9F7。
    }
}
