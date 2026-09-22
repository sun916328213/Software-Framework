using CommunityToolkit.Mvvm.ComponentModel;
using Device.Core.Devices;
using Device.Core.Lights;

namespace Device.Controls.Models;

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
