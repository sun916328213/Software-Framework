// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：IFrameGrabberFactory。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Core.Devices。导入不代表所有类型都会在运行时加载。
using Device.Core.Devices;

namespace Device.Core.FrameGrabbers;

/// <summary>
/// 所有已实现采集卡品牌共用的创建接口。
/// </summary>
/// <remarks>
/// 调用方从 <see cref="AvailableDrivers"/> 选择驱动，并保存描述中的稳定标识，
/// 不需要引用品牌枚举、品牌专用工厂或手写驱动标识。
/// </remarks>
public interface IFrameGrabberFactory
{
    /// <summary>当前工厂可供配置界面选择的采集卡驱动。</summary>
    IReadOnlyList<DeviceDriverDescriptor> AvailableDrivers { get; }

    /// <summary>根据配置中的强类型驱动标识创建尚未连接的采集卡。</summary>
    /// <param name="options">采集卡配置。</param>
    /// <returns>尚未加载厂商驱动的采集卡实例。</returns>
    /// <exception cref="FrameGrabberConfigurationException">配置不符合公共约束。</exception>
    /// <exception cref="FrameGrabberDriverNotFoundException">驱动未由该工厂实现。</exception>
    IFrameGrabber Create(FrameGrabberOptions options);
}
