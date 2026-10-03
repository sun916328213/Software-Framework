// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
// 本文件定义：IProductionDevices。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Core.Lights、Device.Core.Rfid、Device.Core.Scanners。导入不代表所有类型都会在运行时加载。
using Device.Core.Lights;
using Device.Core.Rfid;
using Device.Core.Scanners;

namespace MachineApp.Business.Ports;

/// <summary>
/// IProductionDevices 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
/// 继承或实现的契约：IAsyncDisposable。具体约束按这些契约及各成员实现解释。
/// </summary>
public interface IProductionDevices : IAsyncDisposable
{
    IReadOnlyList<ILightController> Lights { get; }
    IReadOnlyList<IRfidDevice> Rfid { get; }
    IReadOnlyList<IScanner> Scanners { get; }
}
