// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：ILightController。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Core.Devices。导入不代表所有类型都会在运行时加载。
using Device.Core.Devices;

namespace Device.Core.Lights;

/// <summary>
/// 应用层使用的光源控制器接口，不暴露厂商函数和句柄。
/// </summary>
public interface ILightController : IDevice
{
    /// <summary>创建控制器时使用的配置快照。</summary>
    LightControllerOptions Options { get; }

    /// <summary>设置一个通道的亮度。</summary>
    /// <param name="channel">从 1 开始的通道号。</param>
    /// <param name="intensity">包含在配置亮度范围内的值。</param>
    /// <param name="cancellationToken">用于在调用厂商函数前取消操作。</param>
    /// <returns>包含设置是否成功以及失败原因的结果。</returns>
    ValueTask<DeviceResult> SetIntensityAsync(
        int channel,
        int intensity,
        CancellationToken cancellationToken = default);

    /// <summary>将所有已配置通道设置为相同亮度。</summary>
    /// <param name="intensity">包含在配置亮度范围内的值。</param>
    /// <param name="cancellationToken">用于在每次通道设置前取消操作。</param>
    /// <returns>包含批量设置是否成功以及失败原因的结果。</returns>
    ValueTask<DeviceResult> SetAllIntensityAsync(
        int intensity,
        CancellationToken cancellationToken = default);
}
