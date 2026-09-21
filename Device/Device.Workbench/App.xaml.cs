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
using Device.Drivers.Cameras;
using Device.Drivers.FrameGrabbers;
using Device.Drivers.LabelPrinting;
using Device.Drivers.Lights;
using Device.Drivers.Printers;
using Device.Drivers.Rfid;
using Device.Drivers.Scanners;
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
        protected override Window CreateShell()
        {
            return Container.Resolve<MainWindow>();
        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterSingleton<ThemeService>();

            // 这里只注册统一工厂，不创建设备实例；具体实例由各 Workspace 按用户所选驱动延迟创建。
            // 只有宿主引用 Device.Drivers，Controls 始终面向 Device.Core 契约工作。
            containerRegistry.RegisterInstance<ICameraFactory>(new CameraFactory());
            containerRegistry.RegisterInstance<IFrameGrabberFactory>(new FrameGrabberFactory());
            containerRegistry.RegisterInstance<ILightControllerFactory>(new LightControllerFactory());
            containerRegistry.RegisterInstance<IScannerFactory>(new ScannerFactory());
            containerRegistry.RegisterInstance<IPrinterFactory>(new PrinterFactory());
            containerRegistry.RegisterInstance<IRfidDeviceFactory>(new RfidDeviceFactory());
            containerRegistry.RegisterInstance<ILabelPrintEngineFactory>(new LabelPrintEngineFactory());

            containerRegistry.RegisterSingleton<CameraWorkspace>();
            containerRegistry.RegisterSingleton<FrameGrabberWorkspace>();
            containerRegistry.RegisterSingleton<LightWorkspace>();
            containerRegistry.RegisterSingleton<ScannerWorkspace>();
            containerRegistry.RegisterSingleton<PrinterWorkspace>();
            containerRegistry.RegisterSingleton<RfidWorkspace>();
            containerRegistry.RegisterSingleton<LabelPrintWorkspace>();

            containerRegistry.RegisterForNavigation<CameraView, CameraViewModel>();
            containerRegistry.RegisterForNavigation<FrameGrabberView, FrameGrabberViewModel>();
            containerRegistry.RegisterForNavigation<LightView, LightViewModel>();
            containerRegistry.RegisterForNavigation<ScannerView, ScannerViewModel>();
            containerRegistry.RegisterForNavigation<PrinterView, PrinterViewModel>();
            containerRegistry.RegisterForNavigation<RfidView, RfidViewModel>();
            containerRegistry.RegisterForNavigation<LabelPrintView, LabelPrintViewModel>();
        }

        protected override void OnInitialized()
        {
            // 启动时把持久化的主题应用到 App.xaml 里的 BundledTheme 实例。
            Container.Resolve<ThemeService>().Initialize();
            base.OnInitialized();
        }

        protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
        {
            moduleCatalog.AddModule<WPFCustomControlsModule>();
        }
    }
}
