using Device.Camera;
using Device.Light;
using Device.Printer;
using Device.PrintSoftware;
using Device.RFID;
using Device.Scan;
using Extension.Core.Reflection;
using Prism.Ioc;
using Prism.Modularity;
using System.Reflection;


namespace Device
{
    /// <summary>
    /// DeviceModule模块类，负责注册DeviceModule中的服务
    /// </summary>
    public class DeviceModule : IModule
    {
        /// <summary>
        /// 注册模块中的服务
        /// </summary>
        /// <param name="containerRegistry">容器注册器</param>
        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // 注册日志服务
            containerRegistry.RegisterSingleton<CamManager>();
            containerRegistry.RegisterSingleton<LightControlManager>();

            containerRegistry.RegisterSingleton<IPrintSoftware, BartenderHelper>();
            containerRegistry.RegisterSingleton<PrinterManager>();
            containerRegistry.RegisterSingleton<IPrintSoftware, BartenderHelper>();

            containerRegistry.RegisterSingleton<ScanManager>();

            containerRegistry.RegisterSingleton<RFIDManager>();
        }

        /// <summary>
        /// 模块初始化完成后的回调
        /// </summary>
        /// <param name="containerProvider">容器提供者</param>
        public void OnInitialized(IContainerProvider containerProvider)
        {
            // 在这里执行模块初始化完成后的操作
            // 例如获取服务实例等
 
        }
    }
}