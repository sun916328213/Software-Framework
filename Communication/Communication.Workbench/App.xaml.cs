using Communication.Controls.Services;
using Communication.Controls.ViewModels;
using Communication.Controls.Views;
using Communication.Workbench.Views;
using CommunicationNew;
using Extension.Integration.Prism;
using Extension.UI.Theme;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Modularity;
using System.Windows;
using WPFCustomControls;

namespace Communication.Workbench;

/// <summary>通讯测试工作台入口：只负责壳（侧栏 + 区域导航）与模块组合，页面在 Communication.Controls。</summary>
public partial class App : PrismApplication
{
    protected override Window CreateShell() => Container.Resolve<MainWindow>();

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<ThemeService>();
        containerRegistry.RegisterSingleton<ILogService, LogService>();
        containerRegistry.RegisterSingleton<CommunicationSessionService>();

        // 对外只有 CommunicationNew 一套接口，因此整个工作台就一个页面（不拆多 View）。
        containerRegistry.RegisterNavigationView<CommunicationView, CommunicationViewModel>();
        containerRegistry.RegisterDialog<DeviceConfigDialog, DeviceConfigDialogViewModel>();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        // 语义状态画刷（Brush.Success / Warning / Danger / Info）必须是未冻结实例，
        // BAML 编译的 Freezable 无法运行时换色，故在 shell 创建前由代码注入。
        ThemeService.EnsureAliasBrushes();
        base.OnStartup(e);
    }

    protected override void OnInitialized()
    {
        Container.Resolve<ThemeService>().Initialize();
        base.OnInitialized();
    }

    protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
    {
        moduleCatalog.AddModule<CommunicationNewModule>();
        moduleCatalog.AddModule<WPFCustomControlsModule>();
    }
}
