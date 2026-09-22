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
