// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：PrinterStatus。成员说明可结合下方具体实现阅读。
namespace Device.Core.Printers;

/// <summary>
/// 与厂商 SDK 解耦的打印机状态快照。
/// </summary>
/// <param name="ReadyToPrint">打印机是否可以立即接收打印任务。</param>
/// <param name="Paused">打印机是否暂停。</param>
/// <param name="HeadOpen">打印头是否打开。</param>
/// <param name="PaperOut">纸张或标签是否用尽。</param>
/// <param name="RibbonOut">色带是否用尽。</param>
/// <param name="ReceiveBufferFull">接收缓冲区是否已满。</param>
/// <param name="MediaNearEnd">标签或色带是否接近用尽；厂商未区分具体耗材时表示任一耗材将尽。</param>
/// <param name="ReceiveBufferNearFull">接收缓冲区是否接近满载。</param>
/// <param name="AdditionalFaultCode">无法由通用布尔字段表达的稳定故障码；没有附加故障时为 <see langword="null"/>。</param>
/// <param name="HeadTooHot">打印头温度是否过高。</param>
/// <param name="HeadCold">打印头温度是否过低。</param>
/// <param name="PartialFormatInProgress">打印机是否只接收到部分打印格式。</param>
/// <param name="LabelsRemainingInBatch">当前批次尚未打印的标签数量；驱动不支持时为 0。</param>
/// <param name="FormatsInReceiveBuffer">接收缓冲区中的打印格式数量；驱动不支持时为 0。</param>
/// <param name="LabelLengthInDots">标签长度，单位为点；驱动不支持时为 0。</param>
public sealed record PrinterStatus(
    bool ReadyToPrint,
    bool Paused,
    bool HeadOpen,
    bool PaperOut,
    bool RibbonOut,
    bool ReceiveBufferFull,
    bool MediaNearEnd = false,
    bool ReceiveBufferNearFull = false,
    string? AdditionalFaultCode = null,
    bool HeadTooHot = false,
    bool HeadCold = false,
    bool PartialFormatInProgress = false,
    int LabelsRemainingInBatch = 0,
    int FormatsInReceiveBuffer = 0,
    int LabelLengthInDots = 0)
{
    /// <summary>打印机当前是否存在会影响生产的故障。</summary>
    public bool HasFault =>
        HeadOpen
        || PaperOut
        || RibbonOut
        || ReceiveBufferFull
        || HeadTooHot
        || HeadCold
        || AdditionalFaultCode is not null;

    /// <summary>返回适合日志和界面展示的状态摘要。</summary>
    public string Summary
    {
        get
        {
            var states = new List<string>();
            if (ReadyToPrint) states.Add("Ready");
            if (Paused) states.Add("Paused");
            if (HeadOpen) states.Add("HeadOpen");
            if (PaperOut) states.Add("PaperOut");
            if (RibbonOut) states.Add("RibbonOut");
            if (ReceiveBufferFull) states.Add("ReceiveBufferFull");
            if (MediaNearEnd) states.Add("MediaNearEnd");
            if (ReceiveBufferNearFull) states.Add("ReceiveBufferNearFull");
            if (AdditionalFaultCode is not null) states.Add(AdditionalFaultCode);
            if (HeadTooHot) states.Add("HeadTooHot");
            if (HeadCold) states.Add("HeadCold");
            if (PartialFormatInProgress) states.Add("PartialFormatInProgress");
            return states.Count == 0 ? "Unknown" : string.Join('/', states);
        }
    }
}
