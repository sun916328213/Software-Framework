using Device.Core.Devices;

namespace Device.Core.Cameras;

/// <summary>
/// 所有相机统一使用的创建接口。
/// </summary>
/// <remarks>
/// 沿用原 Device 中“一个设备类别对应一个统一入口”的思路：品牌选择和具体适配器路由
/// 集中在工厂内部，调用方不需要了解品牌专用工厂。工厂只保存驱动目录和延迟创建逻辑，
/// 设备实例仍由调用方在实际需要时创建。
/// </remarks>
public interface ICameraFactory
{
    /// <summary>当前工厂已经实现、可供配置界面选择的相机驱动。</summary>
    IReadOnlyList<DeviceDriverDescriptor> AvailableDrivers { get; }

    /// <summary>根据配置中的驱动标识，只创建命中的一个相机实例。</summary>
    /// <param name="options">相机配置。</param>
    /// <returns>尚未连接的相机实例。</returns>
    /// <exception cref="CameraConfigurationException">配置不符合相机约束。</exception>
    /// <exception cref="CameraDriverNotFoundException">配置的驱动未由该工厂实现。</exception>
    ICamera Create(CameraOptions options);

    /// <summary>
    /// 用全部已注册驱动枚举在线相机；单个驱动枚举失败不影响其余驱动的结果。
    /// </summary>
    /// <param name="cancellationToken">取消枚举操作。</param>
    /// <returns>
    /// 成功时 Value 为发现的全部设备；部分驱动失败时 Success 仍为 true，
    /// 失败说明拼接在 Message 中；全部驱动失败时 Success 为 false。
    /// </returns>
    ValueTask<DeviceResult<IReadOnlyList<CameraDeviceDescriptor>>> EnumerateDevicesAsync(
        CancellationToken cancellationToken = default);
}
