// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：ILabelPrintEngineFactory。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Core.Devices。导入不代表所有类型都会在运行时加载。
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
