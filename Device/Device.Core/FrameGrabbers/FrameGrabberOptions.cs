using Device.Core.Devices;

namespace Device.Core.FrameGrabbers;

/// <summary>
/// 创建连续采集卡所需的不可变配置。
/// </summary>
public sealed record FrameGrabberOptions
{
    /// <summary>采集卡在应用中的唯一名称。</summary>
    public required string Name { get; init; }

    /// <summary>决定由哪个已安装驱动创建采集卡。</summary>
    public required DeviceDriverId DriverId { get; init; }

    /// <summary>厂商相机配置文件路径，例如 Matrox DCF 或 Euresys CAM 文件。</summary>
    public required string ConfigurationFilePath { get; init; }

    /// <summary>主机中的采集卡索引，从 0 开始。</summary>
    public int BoardIndex { get; init; }

    /// <summary>Euresys 板卡使用的连接器名称；其他驱动可以忽略该值。</summary>
    public string Connector { get; init; } = "A";

    /// <summary>对外发布的像素格式名称，例如 Mono8 或 Mono16。</summary>
    public string PixelFormat { get; init; } = "Mono8";

    /// <summary>Matrox 连续采集时使用的缓冲区数量。</summary>
    public int BufferCount { get; init; } = 2;
}
