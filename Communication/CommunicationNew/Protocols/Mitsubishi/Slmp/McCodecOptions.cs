namespace CommunicationNew.Protocols.Mitsubishi;

/// <summary>MC 3E/4E 二进制帧路由与监视定时器配置。</summary>
public sealed class McCodecOptions
{
    /// <summary>帧格式。</summary>
    public McFrameFormat FrameFormat { get; init; } = McFrameFormat.ThreeE;

    /// <summary>网络编号。</summary>
    public byte NetworkNumber { get; init; }

    /// <summary>目标站编号。</summary>
    public byte StationNumber { get; init; } = 0xFF;

    /// <summary>目标模块 I/O 编号。</summary>
    public ushort ModuleIoNumber { get; init; } = 0x03FF;

    /// <summary>目标模块站号。</summary>
    public byte ModuleStationNumber { get; init; }

    /// <summary>监视定时器，单位为 250 毫秒。</summary>
    public ushort MonitoringTimer { get; init; } = 0x0010;
}
