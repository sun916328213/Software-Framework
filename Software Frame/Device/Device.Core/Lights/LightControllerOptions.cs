// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：LightControllerOptions。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Core.Devices。导入不代表所有类型都会在运行时加载。
using Device.Core.Devices;

namespace Device.Core.Lights;

/// <summary>
/// 创建串口光源控制器所需的不可变配置。
/// </summary>
public sealed record LightControllerOptions
{
    /// <summary>光源控制器在应用中的唯一名称。</summary>
    public required string Name { get; init; }

    /// <summary>决定由哪个已安装驱动创建控制器。</summary>
    public required DeviceDriverId DriverId { get; init; }

    /// <summary>控制器使用的串口名称，例如 <c>COM3</c>。</summary>
    public required string PortName { get; init; }

    /// <summary>可配置的光源通道数量。</summary>
    public int ChannelCount { get; init; } = 4;

    /// <summary>控制器允许的最大亮度值。</summary>
    public int MaximumIntensity { get; init; } = 255;
}
