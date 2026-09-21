using CommunityToolkit.Mvvm.ComponentModel;
using Device.Core.Cameras;
using Device.Core.Devices;

namespace Device.Controls.Models;

/// <summary>
/// 相机档案（ConfigManagerView 的可编辑行；创建相机时转换成 <see cref="CameraOptions"/>）。
/// 落盘位置由 <see cref="CameraPageModel"/> 统一指定（Config/Cameras.json）。
/// </summary>
public partial class CameraProfile : ObservableObject
{
    /// <summary>档案在应用中的唯一名称。</summary>
    [ObservableProperty]
    private string name = string.Empty;

    /// <summary>驱动标识字符串（配置界面从工厂 AvailableDrivers 选择，不手填）。</summary>
    [ObservableProperty]
    private string driverId = string.Empty;

    /// <summary>相机 IP 地址。</summary>
    [ObservableProperty]
    private string ipAddress = "127.0.0.1";

    /// <summary>曝光时间，单位为毫秒。</summary>
    [ObservableProperty]
    private int exposureMilliseconds = 20;

    /// <summary>相机增益。</summary>
    [ObservableProperty]
    private double gain;

    /// <summary>采集超时时间，单位为毫秒。</summary>
    [ObservableProperty]
    private int timeoutMilliseconds = 1_000;

    /// <summary>转换为运行时创建相机用的不可变配置。</summary>
    public CameraOptions ToOptions() => new()
    {
        Name = Name,
        DriverId = new DeviceDriverId(DriverId),
        IpAddress = IpAddress,
        ExposureMilliseconds = ExposureMilliseconds,
        Gain = Gain,
        TimeoutMilliseconds = TimeoutMilliseconds,
    };
}
