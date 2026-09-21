using Device.Core.Devices;

namespace Device.Core.Printers;

/// <summary>
/// 创建网络打印机所需的不可变配置。
/// </summary>
public sealed record PrinterOptions
{
    /// <summary>打印机在应用中的唯一名称。</summary>
    public required string Name { get; init; }

    /// <summary>决定由哪个已安装驱动创建打印机。</summary>
    public required DeviceDriverId DriverId { get; init; }

    /// <summary>打印机 IPv4 或 IPv6 地址。</summary>
    public required string IpAddress { get; init; }

    /// <summary>打印数据使用的 TCP 端口，ZPL 和 SBPL 网络打印通常使用 9100。</summary>
    public int Port { get; init; } = 9_100;

    /// <summary>
    /// 单次读取打印机响应时允许等待的最长时间，单位为毫秒。
    /// </summary>
    /// <remarks>
    /// Zebra SDK 使用该值控制读取超时；SATO 驱动同时将其用于连接和状态响应超时。
    /// </remarks>
    public int MaximumReadTimeoutMilliseconds { get; init; } = 3_000;

    /// <summary>
    /// 已收到部分响应后，继续等待后续数据的时间，单位为毫秒。
    /// </summary>
    public int TimeToWaitForMoreDataMilliseconds { get; init; } = 500;
}
