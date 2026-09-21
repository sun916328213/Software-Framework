using Device.Core.Devices;

namespace Device.Core.Scanners;

/// <summary>
/// 创建串口扫码器所需的不可变配置。
/// </summary>
public sealed record ScannerOptions
{
    /// <summary>扫码器在应用中的唯一名称。</summary>
    public required string Name { get; init; }

    /// <summary>决定由哪个已安装驱动创建扫码器。</summary>
    public required DeviceDriverId DriverId { get; init; }

    /// <summary>扫码器使用的串口名称，例如 <c>COM3</c>。</summary>
    public required string PortName { get; init; }

    /// <summary>串口波特率。</summary>
    public int BaudRate { get; init; } = 9_600;

    /// <summary>串口数据位。</summary>
    public int DataBits { get; init; } = 8;

    /// <summary>串口停止位，目前支持 1 或 2。</summary>
    public int StopBits { get; init; } = 1;

    /// <summary>串口校验方式：none、odd、even、mark 或 space。</summary>
    public string Parity { get; init; } = "none";

    /// <summary>一条扫码数据的结束符，默认为回车换行。</summary>
    public string Delimiter { get; init; } = "\r\n";

    /// <summary>允许缓存的单帧最大字节数，用于防止异常数据无限增长。</summary>
    public int MaximumFrameBytes { get; init; } = 4_096;

    /// <summary>串口读写超时时间，单位为毫秒。</summary>
    public int TimeoutMilliseconds { get; init; } = 3_000;
}
