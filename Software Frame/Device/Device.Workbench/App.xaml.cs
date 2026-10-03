// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。 本文件属于验证宿主，模块组合与人工验证流程应与生产机型业务区分。
// 本文件定义：App。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Workbench.Infrastructure、WPFCustomControls.Workbench、Prism.Regions、Device.Controls、Device.Core.Cameras、Device.Core.FrameGrabbers。导入不代表所有类型都会在运行时加载。
using Device.Workbench.Infrastructure;
using WPFCustomControls.Workbench;
using Prism.Regions;
using Device.Controls;
using Device.Core.Cameras;
using Device.Core.FrameGrabbers;
using Device.Core.LabelPrinting;
using Device.Core.Lights;
using Device.Core.Printers;
using Device.Core.Rfid;
using Device.Core.Scanners;
using Device.Controls.Services;
using Device.Controls.ViewModels;
using Device.Controls.Views;
using Device.Workbench.Views;
using Extension.UI.Theme;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Modularity;
using System.Windows;
using WPFCustomControls;

namespace Device.Workbench
{
    /// <summary>
    /// Prism 应用入口：宿主只留壳，页面 UI 全在 Device.Controls。
    /// </summary>
    public partial class App : PrismApplication
    {
    private WorkbenchLifetime? _lifetime;

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnStartup。
        /// 正文中的关键协作调用包括 EnsureAliasBrushes、OnStartup；具体条件和先后次序见方法体。
        /// </summary>
        protected override void OnStartup(StartupEventArgs e)
        {
            ThemeService.EnsureAliasBrushes();
            base.OnStartup(e);
        }

        /// <summary>
        /// 创建当前实现提供的对象或结果；对应入口为 CreateShell。
        /// 正文中的关键协作调用包括 Resolve；具体条件和先后次序见方法体。
        /// </summary>
        protected override Window CreateShell()
        {
            return Container.Resolve<MainWindow>();
        }

        /// <summary>
        /// 注册类型、服务或入口映射；对应入口为 RegisterTypes。
        /// 正文中的关键协作调用包括 RegisterSingleton、RegisterInstance；具体条件和先后次序见方法体。
        /// </summary>
        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterSingleton<ThemeService>();

            containerRegistry.RegisterInstance(new DeviceModuleOptions { ReuseNavigationViews = false });

            // 这里只注册统一工厂，不创建设备实例；具体实例由各 Workspace 按用户所选驱动延迟创建。
            // 各类页面和 Workspace 由 Device.Controls 模块装配。

        }

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnInitialized。
        /// 正文中的关键协作调用包括 Initialize、Resolve、OnInitialized、StopLive、CancelPendingOperations；具体条件和先后次序见方法体。
        /// </summary>
        protected override void OnInitialized()
        {
            // 启动时把持久化的主题应用到 App.xaml 里的 BundledTheme 实例。
            Container.Resolve<ThemeService>().Initialize();
            base.OnInitialized();
        _lifetime = new WorkbenchLifetime(MainWindow,
            () => { (MainWindow.DataContext as IDisposable)?.Dispose(); return ValueTask.CompletedTask; },
            () => { Container.Resolve<CameraWorkspace>().StopLive(); return ValueTask.CompletedTask; },
            () => { Container.Resolve<CameraWorkspace>().CancelPendingOperations(); return ValueTask.CompletedTask; },
            () => { Container.Resolve<FrameGrabberWorkspace>().CancelPendingOperations(); return ValueTask.CompletedTask; },
            () => { Container.Resolve<LightWorkspace>().CancelPendingOperations(); return ValueTask.CompletedTask; },
            () => { Container.Resolve<ScannerWorkspace>().CancelPendingOperations(); return ValueTask.CompletedTask; },
            () => { Container.Resolve<RfidWorkspace>().CancelPendingOperations(); return ValueTask.CompletedTask; },
            () => { Container.Resolve<PrinterWorkspace>().CancelPendingOperations(); return ValueTask.CompletedTask; },
            () => { Container.Resolve<LabelPrintWorkspace>().CancelPendingOperations(); return ValueTask.CompletedTask; },
            () => WorkbenchLifetime.CloseRegionAsync(Container.Resolve<IRegionManager>(), RegionNames.MainRegion),
            () => Container.Resolve<Device.Controls.Services.CameraWorkspace>().DisposeAsync(),
            () => Container.Resolve<Device.Controls.Services.FrameGrabberWorkspace>().DisposeAsync(),
            () => Container.Resolve<Device.Controls.Services.LightWorkspace>().DisposeAsync(),
            () => Container.Resolve<Device.Controls.Services.ScannerWorkspace>().DisposeAsync(),
            () => Container.Resolve<Device.Controls.Services.RfidWorkspace>().DisposeAsync(),
            () => Container.Resolve<Device.Controls.Services.PrinterWorkspace>().DisposeAsync(),
            () => Container.Resolve<Device.Controls.Services.LabelPrintWorkspace>().DisposeAsync());
            (MainWindow?.DataContext as Device.Workbench.ViewModels.MainWindowViewModel)?.SelectFirst();
        }

        /// <summary>
        /// 配置当前对象或组合入口；对应入口为 ConfigureModuleCatalog。
        /// 正文中的关键协作调用包括 AddModule；具体条件和先后次序见方法体。
        /// </summary>
        protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
        {
            moduleCatalog.AddModule<DeviceControlsModule>();
            moduleCatalog.AddModule<WPFCustomControlsModule>();
        }    protected override void OnExit(ExitEventArgs e)
    {
        _ = _lifetime?.BeginCleanup();
        base.OnExit(e);
    }

    }
}
