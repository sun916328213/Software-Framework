using Device.Core.Devices;

namespace Device.Core.LabelPrinting;

/// <summary>
/// 所有已实现标签打印软件共用的创建接口。
/// </summary>
/// <remarks>
/// 调用方从 <see cref="AvailableDrivers"/> 选择驱动，并保存描述中的稳定标识，
/// 不需要引用软件枚举、专用工厂或手写驱动标识。
/// </remarks>
public interface ILabelPrintEngineFactory
{
    /// <summary>当前工厂可供配置界面选择的标签打印软件驱动。</summary>
    IReadOnlyList<DeviceDriverDescriptor> AvailableDrivers { get; }

    /// <summary>创建尚未启动厂商进程的标签打印引擎。</summary>
    ILabelPrintEngine Create(LabelPrintEngineOptions options);
}
