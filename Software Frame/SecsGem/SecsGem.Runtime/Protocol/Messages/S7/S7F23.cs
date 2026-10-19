using SecsGem.Core.Order;
using Secs4Net;

namespace SecsGem.Runtime.Order.S7;

/// <summary>
/// S7F23：传送格式化配方；本实现新建或覆盖内存命令，不切换配方。
/// 本接收方向：S7F23 H→E / S7F24 E→H（标准允许双向事务）。
/// 请求：PPID、MDLN、SOFTREV、命令列表；回复：ACKC7，0=接受。仅校验结构，不校验工艺含义。
/// </summary>
/// <remarks>
/// 指令格式示例：工作台简单参数块模型，命令 1 下四个参数；不是所有机型的固定配方格式。
/// <code><![CDATA[
/// 'S7F23'W                        // Host→设备，下载格式化配方
/// <L [4]                          // PPID、MDLN、SOFTREV、命令列表
///     <A [9] 'DEMO-FAST'>          // PPID：配方编号
///     <A [9] 'WORKBENCH'>          // MDLN：配方关联机型文本（此处以 ASCII 为例）
///     <A [3] '1.0'>                // SOFTREV：配方关联版本文本（此处以 ASCII 为例）
///     <L [1]                      // 本例一条命令，实际可为任意条数
///         <L [2]                  // CCODE、PPARM 列表
///             <U2 [1] 1>          // CCODE：本例命令号；Runtime 不限定其类型
///             <L [4]              // PPARM：本例四个参数
///                 <U4 [1] 1>      // Version：工作台示例版本
///                 <U4 [1] 5000>   // ExposureUs：曝光时间，微秒
///                 <F8 [1] 1.5>    // Gain：增益
///                 <F8 [1] 100>    // SpeedMmPerSec：速度，毫米/秒
///             >
///         >
///     >
/// >
/// .
/// 'S7F24'                         // 设备→Host，格式化配方下载应答
/// <B [1] 0x00>                    // ACKC7：0=协议存储接受，不代表业务转换/应用成功
/// .
/// ]]></code>
/// 命令/参数数量与类型由项目 Codec 约定，可含字符串、向量和嵌套列表；接收不自动切换当前配方。
/// </remarks>
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
