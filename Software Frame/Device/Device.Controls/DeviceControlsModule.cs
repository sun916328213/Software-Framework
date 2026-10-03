// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。 本文件属于组合注册入口，具体依赖可以在这里组装；注册页面不等于选择导航或启动设备。
// 本文件定义：DeviceModuleOptions、DeviceControlsModule。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Core.Cameras、Device.Core.Lights、Device.Core.Rfid、Device.Core.Scanners、Device.Core.LabelPrinting、Device.Core.FrameGrabbers。导入不代表所有类型都会在运行时加载。
using Device.Core.Cameras;
using Device.Core.Lights;
using Device.Core.Rfid;
using Device.Core.Scanners;
using Device.Core.LabelPrinting;
using Device.Core.FrameGrabbers;
using Device.Core.Printers;
using Device.Runtime.Cameras;
using Device.Runtime.Lights;
using Device.Runtime.Rfid;
using Device.Runtime.Scanners;
using Device.Runtime.LabelPrinting;
using Device.Runtime.FrameGrabbers;
using Device.Runtime.Printers;
using Device.Controls.Services;
using Device.Controls.ViewModels;
using Device.Controls.Views;
using Extension.Integration.Prism;
using Prism.Ioc;

namespace Device.Controls;

/// <summary>Controls container registration lifetime. Region page retention is a separate Prism policy.</summary>
/// <remarks>
/// DeviceModuleOptions 的类型定义。Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。 本文件属于组合注册入口，具体依赖可以在这里组装；注册页面不等于选择导航或启动设备。
/// </remarks>
public sealed class DeviceModuleOptions { public bool ReuseNavigationViews { get; init; } = true; }

/// <summary>
/// DeviceControlsModule 的类型定义。Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。 本文件属于组合注册入口，具体依赖可以在这里组装；注册页面不等于选择导航或启动设备。
/// 继承或实现的契约：Prism.Modularity.IModule。具体约束按这些契约及各成员实现解释。
/// </summary>
public sealed class DeviceControlsModule : Prism.Modularity.IModule
{
    private readonly bool _reuseViews;
    /// <summary>
    /// 初始化 DeviceControlsModule 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    public DeviceControlsModule(DeviceModuleOptions? options = null) => _reuseViews = options?.ReuseNavigationViews ?? true;
    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnInitialized。
    /// </summary>
    public void OnInitialized(IContainerProvider provider) { }

    /// <summary>
    /// Registers default services and UI types. Workspaces survive navigation.
    /// Production hosts share container navigation views for prewarming. Transient registration
    /// constructs a view on resolution; it does not require the region to discard cached pages.
    /// </summary>
    /// <remarks>
    /// 注册类型、服务或入口映射；对应入口为 RegisterServices。
    /// 正文中的关键协作调用包括 ThrowIfNull、Default、IsRegistered、RegisterSingleton；具体条件和先后次序见方法体。
    /// </remarks>
    private static void RegisterServices(IContainerRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        Default<ICameraFactory, CameraFactory>(registry);
        Default<ILightControllerFactory, LightControllerFactory>(registry);
        Default<IRfidDeviceFactory, RfidDeviceFactory>(registry);
        Default<IScannerFactory, ScannerFactory>(registry);
        Default<ILabelPrintEngineFactory, LabelPrintEngineFactory>(registry);
        Default<IFrameGrabberFactory, FrameGrabberFactory>(registry);
        Default<IPrinterFactory, PrinterFactory>(registry);
        if (!registry.IsRegistered<ILabelJobService>()) registry.RegisterSingleton<ILabelJobService, LabelJobService>();
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Default。
    /// 正文中的关键协作调用包括 IsRegistered、RegisterInstance；具体条件和先后次序见方法体。
    /// </summary>
    private static void Default<TContract, TImplementation>(IContainerRegistry registry)
        where TImplementation : TContract, new()
    {
        if (!registry.IsRegistered<TContract>()) registry.RegisterInstance<TContract>(new TImplementation());
    }

    /// <summary>
    /// 注册类型、服务或入口映射；对应入口为 RegisterTypes。
    /// 正文中的关键协作调用包括 ThrowIfNull、RegisterServices、IsRegistered、RegisterSingleton、RegisterPage；具体条件和先后次序见方法体。
    /// </summary>
    public void RegisterTypes(IContainerRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        RegisterServices(registry);
        if (!registry.IsRegistered<CameraWorkspace>()) registry.RegisterSingleton<CameraWorkspace>();
        if (!registry.IsRegistered<LightWorkspace>()) registry.RegisterSingleton<LightWorkspace>();
        if (!registry.IsRegistered<RfidWorkspace>()) registry.RegisterSingleton<RfidWorkspace>();
        if (!registry.IsRegistered<ScannerWorkspace>()) registry.RegisterSingleton<ScannerWorkspace>();
        if (!registry.IsRegistered<LabelPrintWorkspace>()) registry.RegisterSingleton<LabelPrintWorkspace>();
        if (!registry.IsRegistered<FrameGrabberWorkspace>()) registry.RegisterSingleton<FrameGrabberWorkspace>();
        if (!registry.IsRegistered<PrinterWorkspace>()) registry.RegisterSingleton<PrinterWorkspace>();
        RegisterPage<CameraView, CameraViewModel>(registry);
        RegisterPage<LightView, LightViewModel>(registry);
        RegisterPage<RfidView, RfidViewModel>(registry);
        RegisterPage<ScannerView, ScannerViewModel>(registry);
        RegisterPage<LabelPrintView, LabelPrintViewModel>(registry);
        RegisterPage<FrameGrabberView, FrameGrabberViewModel>(registry);
        RegisterPage<PrinterView, PrinterViewModel>(registry);
    }

    /// <summary>
    /// 注册类型、服务或入口映射；对应入口为 RegisterPage。
    /// 正文中的关键协作调用包括 IsRegistered、RegisterSingleton、Register、RegisterForNavigation；具体条件和先后次序见方法体。
    /// </summary>
    private void RegisterPage<TView, TViewModel>(IContainerRegistry registry)
    {
        if (registry.IsRegistered(typeof(object), typeof(TView).Name)) return;
        if (_reuseViews)
        {
            registry.RegisterSingleton(typeof(object), typeof(TView), typeof(TView).Name);
            Prism.Mvvm.ViewModelLocationProvider.Register<TView, TViewModel>();
        }
        else registry.RegisterForNavigation<TView, TViewModel>();
    }
}
