// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
// 本文件定义：StationServices。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：MachineApp.Business.Ports、MachineApp.Infra.Device、MachineApp.Infra.Tool、WPFControls.ProgramControl.Services。导入不代表所有类型都会在运行时加载。
using MachineApp.Business.Ports;
using MachineApp.Infra.Device;
using MachineApp.Infra.Tool;
using WPFControls.ProgramControl.Services;

namespace MachineApp.Business.Station;

/// <summary>Typed station collaborators. Factories preserve deferred lookup and initialization order.</summary>
/// <remarks>
/// StationServices 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
/// </remarks>
public sealed class StationServices
{
    internal Func<IProductionLog> LogInfo { get; }
    internal Func<IProductionSecs> Secs { get; }
    internal Func<IProductionVision> Vision { get; }
    internal Func<IProductionDevices> Devices { get; }
    internal Func<IProductionPrinter> Printer { get; }
    internal Func<IProductionPlc> Plc { get; }

    /// <summary>
    /// 初始化 StationServices 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    internal StationServices(Func<IProductionLog> logInfo, Func<IProductionSecs> secs,
        Func<IProductionVision> vision, Func<IProductionDevices> devices,
        Func<IProductionPrinter> printer, Func<IProductionPlc> plc)
    {
        LogInfo = logInfo;
        Secs = secs;
        Vision = vision;
        Devices = devices;
        Printer = printer;
        Plc = plc;
    }
}
