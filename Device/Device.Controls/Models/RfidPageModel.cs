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
