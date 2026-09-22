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

    /// <summary>
    /// 相机扫描类型，默认是面阵相机。
    /// </summary>
    public CameraScanType ScanType { get; init; } = CameraScanType.AreaScan;

    /// <summary>
    /// 可选的厂商参数文件路径。连接相机后先加载该文件，再应用本配置中的公共参数。
    /// </summary>
    public string? ParameterFilePath { get; init; }

    /// <summary>曝光时间，单位为毫秒。</summary>
    public int ExposureMilliseconds { get; init; } = 20;

    /// <summary>相机增益。</summary>
    public double Gain { get; init; }

    /// <summary>采集超时时间，单位为毫秒。</summary>
    public int TimeoutMilliseconds { get; init; } = 1_000;

    /// <summary>帧开始触发配置；面阵和线扫相机均使用该配置。</summary>
    public CameraTriggerSettings FrameTrigger { get; init; } = new();

    /// <summary>
    /// 行开始触发配置，仅在线扫相机中应用；默认关闭。
    /// </summary>
    public CameraTriggerSettings LineTrigger { get; init; } = new()
    {
        Selector = CameraTriggerSelectors.LineStart,
        Mode = CameraTriggerMode.Off,
        Source = CameraTriggerSources.Line1,
    };
}
