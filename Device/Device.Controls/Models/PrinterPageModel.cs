using CommunityToolkit.Mvvm.ComponentModel;
using Device.Core.Devices;
using Device.Core.Printers;

namespace Device.Controls.Models;

/// <summary>网络打印机档案（对应 <see cref="PrinterOptions"/>）。</summary>
public partial class PrinterProfile : ObservableObject, IDeviceProfile
{
    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string driverId = string.Empty;

    [ObservableProperty]
    private string ipAddress = "127.0.0.1";

    [ObservableProperty]
    private int port = 9_100;

    [ObservableProperty]
    private int maximumReadTimeoutMilliseconds = 3_000;

    public PrinterOptions ToOptions() => new()
    {
        Name = Name,
        DriverId = new DeviceDriverId(DriverId),
        IpAddress = IpAddress,
        Port = Port,
        MaximumReadTimeoutMilliseconds = MaximumReadTimeoutMilliseconds,
    };
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
