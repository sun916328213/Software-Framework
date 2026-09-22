using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Device.Core.Devices;
using Device.Core.LabelPrinting;

namespace Device.Controls.Models;

/// <summary>标签打印引擎档案（对应 <see cref="LabelPrintEngineOptions"/>）。</summary>
public partial class LabelPrintProfile : ObservableObject, IDeviceProfile
{
    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string driverId = string.Empty;

    [ObservableProperty]
    private string? defaultPrinterName;

    [ObservableProperty]
    private int printTimeoutMilliseconds = 10_000;

    [ObservableProperty]
    private int previewDpi = 300;

    public LabelPrintEngineOptions ToOptions() => new()
    {
        Name = Name,
        DriverId = new DeviceDriverId(DriverId),
        DefaultPrinterName = DefaultPrinterName,
        PrintTimeoutMilliseconds = PrintTimeoutMilliseconds,
        PreviewDpi = PreviewDpi,
    };
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
