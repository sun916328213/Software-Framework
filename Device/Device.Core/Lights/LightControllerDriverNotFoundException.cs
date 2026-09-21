using Device.Core.Devices;

namespace Device.Core.Lights;

/// <summary>
/// 配置引用了当前运行环境中未安装的光源控制器驱动。
/// </summary>
public sealed class LightControllerDriverNotFoundException : Exception
{
    /// <summary>创建未安装驱动异常。</summary>
    /// <param name="driverId">配置请求的驱动标识。</param>
    /// <param name="installedDrivers">用于诊断的已安装驱动列表。</param>
    public LightControllerDriverNotFoundException(DeviceDriverId driverId, string installedDrivers)
        : base($"Light controller driver '{driverId}' is not installed. Installed drivers: {installedDrivers}.")
    {
        DriverId = driverId;
    }

    /// <summary>未找到的驱动标识。</summary>
    public DeviceDriverId DriverId { get; }
}
