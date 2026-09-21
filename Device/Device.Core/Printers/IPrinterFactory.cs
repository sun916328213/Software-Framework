using Device.Core.Devices;
using Device.Core.Printers;

namespace Device.Core.Printers;

/// <summary>
/// 所有已实现打印机品牌共用的创建接口。
/// </summary>
/// <remarks>
/// 调用方从 <see cref="AvailableDrivers"/> 选择驱动，并把描述中的稳定标识保存到配置；
/// 不需要引用品牌枚举、品牌专用工厂或手写驱动标识。
/// </remarks>
public interface IPrinterFactory
{
    /// <summary>当前工厂已经实现、可供配置界面选择的打印机驱动。</summary>
    IReadOnlyList<DeviceDriverDescriptor> AvailableDrivers { get; }

    /// <summary>根据配置中的驱动标识创建打印机。</summary>
    /// <param name="options">打印机配置。</param>
    /// <returns>尚未连接的打印机实例。</returns>
    /// <exception cref="PrinterConfigurationException">配置不符合打印机约束。</exception>
    /// <exception cref="PrinterDriverNotFoundException">配置的驱动未由该工厂实现。</exception>
    IPrinter Create(PrinterOptions options);
}
