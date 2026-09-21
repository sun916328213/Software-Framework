using Device.Core.Devices;

namespace Device.Core.LabelPrinting;

/// <summary>
/// 配置引用了当前运行环境中未安装的标签打印软件驱动。
/// </summary>
public sealed class LabelPrintEngineDriverNotFoundException : Exception
{
    /// <summary>创建未安装驱动异常。</summary>
    public LabelPrintEngineDriverNotFoundException(DeviceDriverId driverId, string installedDrivers)
        : base($"Label print engine driver '{driverId}' is not installed. Installed drivers: {installedDrivers}.")
    {
        DriverId = driverId;
    }

    /// <summary>未找到的驱动标识。</summary>
    public DeviceDriverId DriverId { get; }
}
