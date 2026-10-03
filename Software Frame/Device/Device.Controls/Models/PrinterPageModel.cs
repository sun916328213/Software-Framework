// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：PrinterProfile、PrinterPageModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Controls、CommunityToolkit.Mvvm.ComponentModel、Device.Core.Devices、Device.Core.Printers。导入不代表所有类型都会在运行时加载。
using Device.Controls;
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

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ToOptions。
    /// </summary>
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
    /// <summary>
    /// 初始化 PrinterPageModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
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
