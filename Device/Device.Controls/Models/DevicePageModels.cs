using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Device.Controls.Models;

/// <summary>采集卡页状态：连续采集帧计数与最新帧预览。</summary>
public partial class FrameGrabberPageModel : DevicePageModelBase<FrameGrabberProfile>
{
    public FrameGrabberPageModel()
        : base("FrameGrabbers.json")
    {
    }

    /// <summary>本次连接已收到的帧数。</summary>
    [ObservableProperty]
    private long frameCount;

    /// <summary>最近一帧的渲染结果。</summary>
    [ObservableProperty]
    private ImageSource? preview;

    /// <summary>最近一帧的规格说明。</summary>
    [ObservableProperty]
    private string frameInfo = "尚无图像";

    /// <summary>收到新帧时更新预览与计数（UI 线程调用）。</summary>
    public void ShowFrame(ImageSource image, int width, int height, string pixelFormat, long sequence)
    {
        Preview = image;
        FrameCount++;
        FrameInfo = $"{width}×{height} {pixelFormat} · 帧号 {sequence} · 共 {FrameCount} 帧";
    }
}

/// <summary>光源页状态：通道号/亮度微调值。</summary>
public partial class LightPageModel : DevicePageModelBase<LightProfile>
{
    public LightPageModel()
        : base("Lights.json")
    {
    }

    /// <summary>单通道设置的通道号（从 1 开始）。</summary>
    [ObservableProperty]
    private int channel = 1;

    /// <summary>单通道设置的亮度值。</summary>
    [ObservableProperty]
    private int intensity = 128;

    /// <summary>全通道统一亮度值。</summary>
    [ObservableProperty]
    private int allIntensity = 128;
}

/// <summary>一条扫码记录。</summary>
public sealed record ScanRecord(string ReceivedAt, string Value);

/// <summary>扫码器页状态：被动收件箱。</summary>
public partial class ScannerPageModel : DevicePageModelBase<ScannerProfile>
{
    public ScannerPageModel()
        : base("Scanners.json")
    {
    }

    /// <summary>收到的扫码记录（最新在前，上限 200 条）。</summary>
    public ObservableCollection<ScanRecord> Scans { get; } = [];

    public void AddScan(ScanRecord record)
    {
        Scans.Insert(0, record);
        while (Scans.Count > 200)
            Scans.RemoveAt(Scans.Count - 1);
    }
}

/// <summary>打印机页状态：原始指令与状态摘要。</summary>
public partial class PrinterPageModel : DevicePageModelBase<PrinterProfile>
{
    public PrinterPageModel()
        : base("Printers.json")
    {
    }

    /// <summary>待发送的原始打印指令（ZPL / SBPL 文本）。</summary>
    [ObservableProperty]
    private string rawData = "^XA^FDHello^FS^XZ";

    /// <summary>最近一次状态查询摘要。</summary>
    [ObservableProperty]
    private string statusSummary = "尚未查询";
}

/// <summary>RFID 页状态：读写参数与结果文本。</summary>
public partial class RfidPageModel : DevicePageModelBase<RfidProfile>
{
    public RfidPageModel()
        : base("Rfids.json")
    {
    }

    [ObservableProperty]
    private int startPage = 1;

    [ObservableProperty]
    private int pageCount = 2;

    /// <summary>最近一次读卡结果（去除末尾空字符的文本）。</summary>
    [ObservableProperty]
    private string readResult = string.Empty;

    /// <summary>待写入内容，一行一页。</summary>
    [ObservableProperty]
    private string writeText = "PAGE1\nPAGE2";
}

/// <summary>标签模板的一个可编辑字段行。</summary>
public partial class LabelTemplateFieldRow : ObservableObject
{
    public LabelTemplateFieldRow(string name, string value)
    {
        _name = name;
        _value = value;
    }

    private readonly string _name;

    /// <summary>模板字段名（只读展示）。</summary>
    public string Name => _name;

    [ObservableProperty]
    private string _value;
}

/// <summary>标签打印页状态：模板路径、字段表、打印机选择与预览。</summary>
public partial class LabelPrintPageModel : DevicePageModelBase<LabelPrintProfile>
{
    public LabelPrintPageModel()
        : base("LabelPrinters.json")
    {
    }

    /// <summary>当前打开的模板路径（连接后由「打开模板」写入）。</summary>
    [ObservableProperty]
    private string templatePath = string.Empty;

    [ObservableProperty]
    private int copies = 1;

    /// <summary>最近一次模板渲染的 PNG 预览。</summary>
    [ObservableProperty]
    private ImageSource? preview;

    /// <summary>当前模板的命名字段（值可编辑）。</summary>
    public ObservableCollection<LabelTemplateFieldRow> Fields { get; } = [];

    /// <summary>系统打印机列表（来自 GetInstalledPrintersAsync）。</summary>
    public ObservableCollection<string> Printers { get; } = [];

    [ObservableProperty]
    private string? selectedPrinter;
}
