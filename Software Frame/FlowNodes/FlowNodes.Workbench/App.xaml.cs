// 【中文阅读说明】
// FlowNodes 模块：WPF 流程节点与执行、编辑协作的集成；新增或迁移节点需同时核对节点目录、执行契约和持久化类型身份。 本文件属于验证宿主，模块组合与人工验证流程应与生产机型业务区分。
// 本文件定义：App。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFCustomControls.Workbench、Prism.Regions、Extension.UI.Theme、FlowNodes、System.Reflection、System.Windows。导入不代表所有类型都会在运行时加载。
using WPFCustomControls.Workbench;
using Prism.Regions;
using Extension.UI.Theme;
using FlowNodes;

using System.Reflection;
using System.Windows;

using Device.Core.Cameras;
using Device.Controls;
using DataManager.Controls;
using Communication.Controls;
using ScriptEditor.Controls;
using FlowEditor.Controls;
using FlowEditor.Controls.Interfaces;
using FlowNodes.Device;
using FlowNodes.Workbench.Services;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Modularity;

using WPFCustomControls;

namespace FlowNodes.Workbench;

/// <summary>
/// FlowNodes 测试工作台应用入口。
/// 注册各依赖模块（通讯/设备/脚本/编辑器），宿主完整的 FlowChartContainerView，
/// 用于拖拽、连线、运行验证 FlowNodes 下所有节点。
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
        return Container.Resolve<Views.MainWindow>();
    }

    /// <summary>
    /// 注册类型、服务或入口映射；对应入口为 RegisterTypes。
    /// 正文中的关键协作调用包括 RegisterSingleton、Register、RegisterInstance、CreateNodeCatalog、IsRegistered；具体条件和先后次序见方法体。
    /// </summary>
    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<ThemeService>();
        containerRegistry.Register<ViewModels.MainWindowViewModel>();
        containerRegistry.RegisterInstance<FlowEditor.Controls.Discovery.IFlowNodeCatalog>(
            FlowNodes.Integration.FlowNodesComposition.CreateNodeCatalog());
        if (!containerRegistry.IsRegistered<FlowEditor.Controls.Interfaces.INodeEditorHandler>())
            containerRegistry.RegisterSingleton<FlowEditor.Controls.Interfaces.INodeEditorHandler,
                FlowNodes.Presentation.NodeEditing.FlowNodesEditorAdapter>();
        // “相机采集”节点只依赖 ICameraProvider 与 Device.Core 的 ICamera；
        // 品牌默认工厂由 Device.Controls 统一装配；宿主保留按名称查找和生命周期。
        containerRegistry.RegisterSingleton<ICameraProvider, WorkbenchCameraProvider>();
    }

    /// <summary>
    /// 配置当前对象或组合入口；对应入口为 ConfigureModuleCatalog。
    /// 正文中的关键协作调用包括 AddModule；具体条件和先后次序见方法体。
    /// </summary>
    protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
    {
        // 节点构造所需的单例由这些模块与 RegisterTypes 共同提供，
        // 缺失会导致含 DI 构造参数的节点无法物化。
        moduleCatalog.AddModule<DeviceControlsModule>();
        moduleCatalog.AddModule<DataManagerModule>();
        moduleCatalog.AddModule<CommunicationModule>();
        moduleCatalog.AddModule<WPFCustomControlsModule>();
        moduleCatalog.AddModule<FlowEditorModule>();
        moduleCatalog.AddModule<ScriptEditorModule>();
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

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnInitialized。
    /// 正文中的关键协作调用包括 Initialize、Resolve、OnInitialized、Discover、CloseRegionAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    protected override void OnInitialized()
    {
        // Prism 已在 InitializeModules 阶段加载模块；这里再显示壳并创建导航视图。
        Container.Resolve<ThemeService>().Initialize();
        base.OnInitialized();

        // 与生产宿主使用相同来源，模块注册与工具箱共享目录快照。
        Container.Resolve<IFlowToolDiscoveryService>().Discover();

        // 未注册 IFlowLogSink：节点日志仅走 ConsoleOutputRouter 进底部输出面板，
        // 不落盘。如需按流程落盘，可在此接入 DataManager 的 LogManager。

        _lifetime = new WorkbenchLifetime(MainWindow,
            () => { (MainWindow.DataContext as IDisposable)?.Dispose(); return ValueTask.CompletedTask; },
            () => WorkbenchLifetime.CloseRegionAsync(Container.Resolve<IRegionManager>(), ViewModels.MainWindowViewModel.MainRegion),
            async () => { if (Container.Resolve<ICameraProvider>() is IAsyncDisposable disposable) await disposable.DisposeAsync(); },
            () => Container.Resolve<global::Communication.Controls.Services.CommunicationSessionService>().DisposeAsync(),
            () => { Container.Resolve<ScriptEditor.Controls.Services.ScriptManagerService>().StopAll(); Container.Resolve<ScriptEditor.Controls.Services.ScriptEditorService>().DisposeExecution(); return ValueTask.CompletedTask; });
        (MainWindow?.DataContext as ViewModels.MainWindowViewModel)?.SelectFirst();
    }
}
