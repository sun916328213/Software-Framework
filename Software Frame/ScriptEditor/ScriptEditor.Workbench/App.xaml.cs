// 【中文阅读说明】
// ScriptEditor 模块：脚本持久化模型、运行时编译执行和编辑器协作；脚本 ID、引用及断点属于需要保留的状态。 本文件属于验证宿主，模块组合与人工验证流程应与生产机型业务区分。
// 本文件定义：App。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFCustomControls.Workbench、System.Windows、Extension.UI.Theme、Prism.DryIoc、Prism.Ioc、Prism.Modularity。导入不代表所有类型都会在运行时加载。
using WPFCustomControls.Workbench;
using System.Windows;
using Extension.UI.Theme;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Regions;
using ScriptEditor.Controls;
using ScriptEditor.Controls.Services;
using ScriptEditor.Controls.Views;
using ScriptEditor.Workbench.Views;

namespace ScriptEditor.Workbench;

/// <summary>Loads the script module through Prism and opens its page after initialization.</summary>
/// <remarks>
/// App 的类型定义。ScriptEditor 模块：脚本持久化模型、运行时编译执行和编辑器协作；脚本 ID、引用及断点属于需要保留的状态。 本文件属于验证宿主，模块组合与人工验证流程应与生产机型业务区分。
/// 继承或实现的契约：PrismApplication。具体约束按这些契约及各成员实现解释。
/// </remarks>
public partial class App : PrismApplication
{
    private WorkbenchLifetime? _lifetime;

    private bool _smoke;
    /// <summary>
    /// 创建当前实现提供的对象或结果；对应入口为 CreateShell。
    /// 正文中的关键协作调用包括 Resolve；具体条件和先后次序见方法体。
    /// </summary>
    protected override Window CreateShell() => Container.Resolve<MainWindow>();
    /// <summary>
    /// 注册类型、服务或入口映射；对应入口为 RegisterTypes。
    /// 正文中的关键协作调用包括 RegisterSingleton、Register；具体条件和先后次序见方法体。
    /// </summary>
    protected override void RegisterTypes(IContainerRegistry registry)
    {
        registry.RegisterSingleton<ThemeService>();
        registry.Register<ViewModels.MainWindowViewModel>();
        registry.RegisterSingleton<MainWindow>();
    }
    /// <summary>
    /// 配置当前对象或组合入口；对应入口为 ConfigureModuleCatalog。
    /// 正文中的关键协作调用包括 AddModule；具体条件和先后次序见方法体。
    /// </summary>
    protected override void ConfigureModuleCatalog(IModuleCatalog catalog) => catalog.AddModule<ScriptEditorModule>();
    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnStartup。
    /// 正文中的关键协作调用包括 EnsureAliasBrushes、Contains、OnStartup；具体条件和先后次序见方法体。
    /// </summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        ThemeService.EnsureAliasBrushes();
        _smoke = e.Args.Contains("--smoke", StringComparer.OrdinalIgnoreCase);
        base.OnStartup(e);
    }
    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnInitialized。
    /// 正文中的关键协作调用包括 Initialize、Resolve、SetEditorService、OnInitialized、StopAll；具体条件和先后次序见方法体。
    /// </summary>
    protected override void OnInitialized()
    {
        Container.Resolve<ThemeService>().Initialize();
        ScriptEditorView.SetEditorService(MainWindow, Container.Resolve<ScriptEditorService>());
        base.OnInitialized();
        _lifetime = new WorkbenchLifetime(MainWindow,
            () => { (MainWindow.DataContext as IDisposable)?.Dispose(); return ValueTask.CompletedTask; },
            () => { Container.Resolve<ScriptEditor.Controls.Services.ScriptManagerService>().StopAll(); Container.Resolve<ScriptEditorService>().DisposeExecution(); return ValueTask.CompletedTask; },
            () => WorkbenchLifetime.CloseRegionAsync(Container.Resolve<IRegionManager>(), ViewModels.MainWindowViewModel.MainRegion));
        (MainWindow?.DataContext as ViewModels.MainWindowViewModel)?.SelectFirst();
        if (_smoke) MainWindow.Dispatcher.BeginInvoke(new Action(MainWindow.Close));
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
