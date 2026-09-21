using Device.Core.Devices;

namespace Device.Core.Cameras;

/// <summary>
/// 创建相机所需的不可变配置。
/// </summary>
public sealed record CameraOptions
{
    /// <summary>相机在应用中的唯一名称。</summary>
    public required string Name { get; init; }

    /// <summary>决定由哪个已安装驱动创建相机。</summary>
    public required DeviceDriverId DriverId { get; init; }

    /// <summary>GigE 相机的 IP 地址。</summary>
    public required string IpAddress { get; init; }

    /// <summary>曝光时间，单位为毫秒。</summary>
    public int ExposureMilliseconds { get; init; } = 20;

    /// <summary>相机增益。</summary>
    public double Gain { get; init; }

    /// <summary>采集超时时间，单位为毫秒。</summary>
    public int TimeoutMilliseconds { get; init; } = 1_000;
}
