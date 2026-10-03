// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于验证宿主，模块组合与人工验证流程应与生产机型业务区分。
// 本文件定义：App。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFCustomControls.Workbench、Extension.UI.Theme、FlowEditor.Controls、System.Linq、System.Windows、FlowEditor.Controls.ViewModels。导入不代表所有类型都会在运行时加载。
using WPFCustomControls.Workbench;
using Extension.UI.Theme;
using FlowEditor.Controls;
using System.Linq;
using System.Windows;
using FlowEditor.Controls.ViewModels;
using FlowEditor.Workbench.Views;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Regions;
using WPFCustomControls;

namespace FlowEditor.Workbench;

/// <summary>
/// FlowEditor 工作台入口（Prism 宿主）：只负责壳、模块组合与区域导航，
/// 编辑器视图与服务都由 FlowEditor.Controls 中的 FlowEditorModule 提供。
/// </summary>
public partial class App : PrismApplication
{
    private WorkbenchLifetime? _lifetime;

    private bool _smoke;

    /// <summary>壳窗口：模块加载前只构造空壳，编辑器视图在 OnInitialized 由区域导航装入。</summary>
    protected override Window CreateShell() => Container.Resolve<MainWindow>();

    /// <summary>注册壳窗口（业务服务全部由 FlowEditorModule 注册，此处只登记宿主自身类型）。</summary>
    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<ThemeService>();
        containerRegistry.Register<ViewModels.MainWindowViewModel>();
        containerRegistry.RegisterSingleton<MainWindow>();
    }

    /// <summary>先装配属性面板依赖，再加载流程编辑器与内建节点。</summary>
    protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
    {
        moduleCatalog.AddModule<WPFCustomControlsModule>();
        moduleCatalog.AddModule<FlowEditorModule>();
    }

    /// <summary>解析 --smoke 参数，供无人工干预的启动冒烟使用。</summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        ThemeService.EnsureAliasBrushes();
        _smoke = e.Args.Contains("--smoke", StringComparer.OrdinalIgnoreCase);
        base.OnStartup(e);
    }

    /// <summary>壳与模块就绪后导航出流程图编辑器；--smoke 时加载完成即关闭。</summary>
    protected override void OnInitialized()
    {
        Container.Resolve<ThemeService>().Initialize();
        base.OnInitialized(); // 此时 FlowEditorModule 已 OnInitialized（资源已合并）

        _lifetime = new WorkbenchLifetime(MainWindow,
            () => { (MainWindow.DataContext as IDisposable)?.Dispose(); return ValueTask.CompletedTask; },
            () => WorkbenchLifetime.CloseRegionAsync(Container.Resolve<IRegionManager>(), ViewModels.MainWindowViewModel.MainRegion));
        (MainWindow?.DataContext as ViewModels.MainWindowViewModel)?.SelectFirst();

        if (_smoke && MainWindow is not null)
            MainWindow.Dispatcher.BeginInvoke(new Action(MainWindow.Close));
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
