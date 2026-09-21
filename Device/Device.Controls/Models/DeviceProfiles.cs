using CommunityToolkit.Mvvm.ComponentModel;
using Device.Core.Devices;
using Device.Core.FrameGrabbers;
using Device.Core.LabelPrinting;
using Device.Core.Lights;
using Device.Core.Printers;
using Device.Core.Rfid;
using Device.Core.Scanners;

namespace Device.Controls.Models;

/// <summary>采集卡档案（对应 <see cref="FrameGrabberOptions"/>）。</summary>
public partial class FrameGrabberProfile : ObservableObject, IDeviceProfile
{
    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string driverId = string.Empty;

    /// <summary>厂商相机配置文件路径（Matrox DCF / Euresys CAM）。</summary>
    [ObservableProperty]
    private string configurationFilePath = string.Empty;

    [ObservableProperty]
    private int boardIndex;

    [ObservableProperty]
    private string connector = "A";

    [ObservableProperty]
    private string pixelFormat = "Mono8";

    [ObservableProperty]
    private int bufferCount = 2;

    public FrameGrabberOptions ToOptions() => new()
    {
        Name = Name,
        DriverId = new DeviceDriverId(DriverId),
        ConfigurationFilePath = ConfigurationFilePath,
        BoardIndex = BoardIndex,
        Connector = Connector,
        PixelFormat = PixelFormat,
        BufferCount = BufferCount,
    };
}

/// <summary>光源控制器档案（对应 <see cref="LightControllerOptions"/>）。</summary>
public partial class LightProfile : ObservableObject, IDeviceProfile
{
    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string driverId = string.Empty;

    [ObservableProperty]
    private string portName = "COM1";

    [ObservableProperty]
    private int channelCount = 4;

    [ObservableProperty]
    private int maximumIntensity = 255;

    public LightControllerOptions ToOptions() => new()
    {
        Name = Name,
        DriverId = new DeviceDriverId(DriverId),
        PortName = PortName,
        ChannelCount = ChannelCount,
        MaximumIntensity = MaximumIntensity,
    };
}

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
