// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
// 本文件定义：Script。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：MachineApp.Infra.Core、MachineApp.Infra.Device。导入不代表所有类型都会在运行时加载。
using MachineApp.Infra.Core;
using MachineApp.Infra.Device;

/// <summary>
/// Script 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
/// </summary>
public class Script
{
    private ProductionDeviceSession _devices;
    /// <summary>
    /// 执行初始化步骤；对应入口为 Initialize。
    /// 正文中的关键协作调用包括 Resolve；具体条件和先后次序见方法体。
    /// </summary>
    public void Initialize() => _devices = ContainerManager.Resolve<ProductionDeviceSession>();
    /// <summary>
    /// 运行当前处理流程；对应入口为 Run。
    /// 正文中的关键协作调用包括 GetResult、GetAwaiter、ConfigureAwait、InitializeAsync、WriteLine；具体条件和先后次序见方法体。
    /// </summary>
    public void Run()
    {
        _devices.InitializeAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        Console.WriteLine("新 Device 生产设备已按全部档案连接，光源已关闭。");
    }
}
