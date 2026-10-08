// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：DeviceHostTestRegistration。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Workbench、Device.Core.Cameras、Device.Core.LabelPrinting、Device.Core.Lights、Device.Core.Rfid、Device.Core.Scanners。导入不代表所有类型都会在运行时加载。
using Device.Workbench;
using Device.Core.Cameras;
using Device.Core.LabelPrinting;
using Device.Core.Lights;
using Device.Core.Rfid;
using Device.Core.Scanners;
using Device.Runtime.Cameras;
using Device.Runtime.LabelPrinting;
using Device.Runtime.Lights;
using Device.Runtime.Rfid;
using Device.Runtime.Scanners;
using Prism.Ioc;

namespace Device.Workbench.Tests;

/// <summary>
/// DeviceHostTestRegistration 的类型定义。Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
internal static class DeviceHostTestRegistration
{
    /// <summary>
    /// 测试相关入口 RegisterFactories；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 RegisterInstance；具体条件和先后次序见方法体。
    /// </summary>
    internal static void RegisterFactories(IContainerRegistry registry)
    {
        registry.RegisterInstance<ICameraFactory>(new CameraFactory());
        registry.RegisterInstance<ILightControllerFactory>(new LightControllerFactory());
        registry.RegisterInstance<IRfidDeviceFactory>(new RfidDeviceFactory());
        registry.RegisterInstance<IScannerFactory>(new ScannerFactory());
        registry.RegisterInstance<ILabelPrintEngineFactory>(new LabelPrintEngineFactory());
    }
}
