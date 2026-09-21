using Device.Core.Devices;

namespace Device.Core.FrameGrabbers;

/// <summary>
/// 连续工业相机采集卡的公共接口，不暴露厂商句柄或缓冲区类型。
/// </summary>
public interface IFrameGrabber : IDevice
{
    /// <summary>创建采集卡时使用的配置快照。</summary>
    FrameGrabberOptions Options { get; }

    /// <summary>厂商缓冲区已经复制到托管内存后触发的帧事件。</summary>
    event EventHandler<FrameReceivedEventArgs>? FrameReceived;
}
