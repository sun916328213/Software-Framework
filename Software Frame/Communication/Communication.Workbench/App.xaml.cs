// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于验证宿主，模块组合与人工验证流程应与生产机型业务区分。
// 本文件定义：App。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Workbench.Infrastructure、WPFCustomControls.Workbench、Prism.Regions、Communication.Controls、Communication.Controls.Services、Communication.Controls.ViewModels。导入不代表所有类型都会在运行时加载。
using Communication.Workbench.Infrastructure;
using WPFCustomControls.Workbench;
using Prism.Regions;
using Communication.Controls;
using Communication.Controls.Services;
using Communication.Controls.ViewModels;
using Communication.Controls.Views;
using Communication.Workbench.Views;
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
    private WorkbenchLifetime? _lifetime;

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
    /// 正文中的关键协作调用包括 RegisterSingleton；具体条件和先后次序见方法体。
    /// </summary>
    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<ThemeService>();
    }

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnStartup。
    /// 正文中的关键协作调用包括 EnsureAliasBrushes、OnStartup；具体条件和先后次序见方法体。
    /// </summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        // 语义状态画刷（Brush.Success / Warning / Danger / Info）必须是未冻结实例，
        // BAML 编译的 Freezable 无法运行时换色，故在 shell 创建前由代码注入。
        ThemeService.EnsureAliasBrushes();
        base.OnStartup(e);
    }

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnInitialized。
    /// 正文中的关键协作调用包括 Initialize、Resolve、OnInitialized、CloseRegionAsync、DisposeAsync；具体条件和先后次序见方法体。
    /// </summary>
    protected override void OnInitialized()
    {
        Container.Resolve<ThemeService>().Initialize();
        base.OnInitialized();
        _lifetime = new WorkbenchLifetime(MainWindow,
            () => { (MainWindow.DataContext as IDisposable)?.Dispose(); return ValueTask.CompletedTask; },
            () => WorkbenchLifetime.CloseRegionAsync(Container.Resolve<IRegionManager>(), RegionNames.MainRegion),
            () => Container.Resolve<CommunicationSessionService>().DisposeAsync());
        (MainWindow?.DataContext as Communication.Workbench.ViewModels.MainWindowViewModel)?.SelectFirst();
    }

    /// <summary>
    /// 配置当前对象或组合入口；对应入口为 ConfigureModuleCatalog。
    /// 正文中的关键协作调用包括 AddModule；具体条件和先后次序见方法体。
    /// </summary>
    protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
    {
        moduleCatalog.AddModule<CommunicationModule>();
        moduleCatalog.AddModule<WPFCustomControlsModule>();
    }
    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnExit。
    /// 正文中的关键协作调用包括 OnExit；具体条件和先后次序见方法体。
    /// </summary>
    protected override void OnExit(ExitEventArgs e)
    {
        _ = _lifetime?.BeginCleanup();
        base.OnExit(e);
    }
}
