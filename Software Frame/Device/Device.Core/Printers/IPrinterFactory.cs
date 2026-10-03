// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：IPrinterFactory。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Core.Devices、Device.Core.Printers。导入不代表所有类型都会在运行时加载。
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
