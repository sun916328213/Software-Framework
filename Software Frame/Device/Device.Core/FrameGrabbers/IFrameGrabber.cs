// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：IFrameGrabber。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Core.Devices。导入不代表所有类型都会在运行时加载。
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
