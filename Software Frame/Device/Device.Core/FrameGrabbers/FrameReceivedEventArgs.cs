// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：FrameReceivedEventArgs。成员说明可结合下方具体实现阅读。
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
