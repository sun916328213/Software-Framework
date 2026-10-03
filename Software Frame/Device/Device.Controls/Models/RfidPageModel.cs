// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：RfidProfile、RfidPageModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Controls、CommunityToolkit.Mvvm.ComponentModel、Device.Core.Devices、Device.Core.Rfid。导入不代表所有类型都会在运行时加载。
using Device.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using Device.Core.Devices;
using Device.Core.Rfid;

namespace Device.Controls.Models;

/// <summary>RFID 读写器档案（对应 <see cref="RfidOptions"/>）。</summary>
public partial class RfidProfile : ObservableObject, IDeviceProfile
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

    [ObservableProperty]
    private string parity = "none";

    [ObservableProperty]
    private byte slaveId = 1;

    [ObservableProperty]
    private int pageSizeBytes = 8;

    [ObservableProperty]
    private int timeoutMilliseconds = 3_000;

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ToOptions。
    /// </summary>
    public RfidOptions ToOptions() => new()
    {
        Name = Name,
        DriverId = new DeviceDriverId(DriverId),
        PortName = PortName,
        BaudRate = BaudRate,
        DataBits = DataBits,
        StopBits = StopBits,
        Parity = Parity,
        SlaveId = SlaveId,
        PageSizeBytes = PageSizeBytes,
        TimeoutMilliseconds = TimeoutMilliseconds,
    };
}

/// <summary>RFID 页状态：读写参数与结果文本。</summary>
public partial class RfidPageModel : DevicePageModelBase<RfidProfile>
{
    /// <summary>
    /// 初始化 RfidPageModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
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
