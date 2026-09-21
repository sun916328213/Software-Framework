namespace Device.Core.FrameGrabbers;

/// <summary>
/// 连续采集完成事件参数。
/// </summary>
public sealed class FrameReceivedEventArgs : EventArgs
{
    /// <summary>使用已经复制到托管内存的图像帧创建事件参数。</summary>
    /// <param name="frame">不依赖厂商缓冲区生命周期的图像帧。</param>
    public FrameReceivedEventArgs(FrameGrabberFrame frame)
    {
        Frame = frame;
    }

    /// <summary>本次完成采集的图像帧。</summary>
    public FrameGrabberFrame Frame { get; }
}
