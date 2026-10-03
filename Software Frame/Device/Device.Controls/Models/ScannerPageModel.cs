// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：ScannerProfile、ScanRecord、ScannerPageModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Controls、System.Collections.ObjectModel、CommunityToolkit.Mvvm.ComponentModel、Device.Core.Devices、Device.Core.Scanners。导入不代表所有类型都会在运行时加载。
using Device.Controls;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Device.Core.Devices;
using Device.Core.Scanners;

namespace Device.Controls.Models;

/// <summary>扫码器档案（对应 <see cref="ScannerOptions"/>）。</summary>
public partial class ScannerProfile : ObservableObject, IDeviceProfile
{
    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string driverId = string.Empty;

    [ObservableProperty]
    private string portName = "COM1";

    [ObservableProperty]
    private int baudRate = 9_600;

    [ObservableProperty]
    private int dataBits = 8;

    [ObservableProperty]
    private int stopBits = 1;

    /// <summary>none、odd、even、mark 或 space。</summary>
    [ObservableProperty]
    private string parity = "none";

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ToOptions。
    /// </summary>
    public ScannerOptions ToOptions() => new()
    {
        Name = Name,
        DriverId = new DeviceDriverId(DriverId),
        PortName = PortName,
        BaudRate = BaudRate,
        DataBits = DataBits,
        StopBits = StopBits,
        Parity = Parity,
    };
}

/// <summary>一条扫码记录。</summary>
public sealed record ScanRecord(string ReceivedAt, string Value);

/// <summary>扫码器页状态：被动收件箱。</summary>
public partial class ScannerPageModel : DevicePageModelBase<ScannerProfile>
{
    /// <summary>
    /// 初始化 ScannerPageModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    public ScannerPageModel()
        : base("Scanners.json")
    {
    }

    /// <summary>收到的扫码记录（最新在前，上限 200 条）。</summary>
    public ObservableCollection<ScanRecord> Scans { get; } = [];

    /// <summary>
    /// 执行添加操作；对应入口为 AddScan。
    /// 正文中的关键协作调用包括 Insert、RemoveAt；具体条件和先后次序见方法体。
    /// </summary>
    public void AddScan(ScanRecord record)
    {
        Scans.Insert(0, record);
        while (Scans.Count > 200)
            Scans.RemoveAt(Scans.Count - 1);
    }
}
