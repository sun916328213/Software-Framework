using Device.Core.Devices;

namespace Device.Core.Printers;

/// <summary>
/// 配置引用了当前运行环境中未安装的打印机驱动。
/// </summary>
public sealed class PrinterDriverNotFoundException : Exception
{
    /// <summary>创建未安装驱动异常。</summary>
    /// <param name="driverId">配置请求的驱动标识。</param>
    /// <param name="installedDrivers">用于诊断的已安装驱动列表。</param>
    public PrinterDriverNotFoundException(DeviceDriverId driverId, string installedDrivers)
        : base($"Printer driver '{driverId}' is not installed. Installed drivers: {installedDrivers}.")
    {
        DriverId = driverId;
    }

    /// <summary>未找到的驱动标识。</summary>
    public DeviceDriverId DriverId { get; }
}
