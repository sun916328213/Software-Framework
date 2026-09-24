using System.Reflection;
using System.Windows;
using CommunicationNew;
using DataManager;
using Device.Core.Cameras;
using Device.Drivers.Cameras;
using FlowEditor;
using FlowEditor.Interfaces;
using FlowNodes.Device;
using FlowNodes.Workbench.Services;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Modularity;
using ScriptEditor;
using WPFCustomControls;

namespace FlowNodes.Workbench;

/// <summary>
/// FlowNodes 测试工作台应用入口。
/// 注册各依赖模块（通讯/设备/脚本/编辑器），宿主完整的 FlowChartContainerView，
/// 用于拖拽、连线、运行验证 FlowNodes 下所有节点。
/// </summary>
public partial class App : PrismApplication
{
    protected override Window CreateShell()
    {
        return Container.Resolve<Views.MainWindow>();
    }

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        // “相机采集”节点只依赖 ICameraProvider 与 Device.Core 的 ICamera；
        // 品牌工厂与按名称查找都由宿主在这里接好，节点不会接触厂商 SDK 类型。
        containerRegistry.RegisterSingleton<ICameraFactory, CameraFactory>();
        containerRegistry.RegisterSingleton<ICameraProvider, WorkbenchCameraProvider>();
    }

    protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
    {
        // 节点构造所需的单例由这些模块与 RegisterTypes 共同提供，
        // 缺失会导致含 DI 构造参数的节点无法物化。
        moduleCatalog.AddModule<DataManagerModule>();
        moduleCatalog.AddModule<CommunicationNewModule>();
        moduleCatalog.AddModule<WPFCustomControlsModule>();
        moduleCatalog.AddModule<FlowEditorModule>();
        moduleCatalog.AddModule<ScriptEditorModule>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // WorkbenchCameraProvider 是缓存相机的所有者，容器不会隐式释放 IAsyncDisposable，
        // 因此在退出时显式收尾，避免厂商 SDK 句柄随进程退出而被强杀。
        try
        {
            if (Container.Resolve<ICameraProvider>() is IAsyncDisposable disposable)
            {
                // 退出入口没有可 await 的上下文；Provider 内部全部 ConfigureAwait(false)，
                // 不会回调 UI 线程，因此这里的同步等待不会死锁。
#pragma warning disable VSTHRD002
                disposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
            }
        }
        catch
        {
            // 退出阶段的释放失败不应阻止进程退出。
        }

        base.OnExit(e);
    }

    protected override void OnInitialized()
    {
        // base 会跑 ModuleManager.Run()：CreateShell 时模块尚未加载，
        // 必须在这里（模块就绪后）再导航创建编辑器视图，否则 AutoWire 解析 VM 失败。
        base.OnInitialized();

        // 强制加载 FlowNodes 程序集，确保其节点能被工具箱扫描到（与 Packing 一致）。
        try { Assembly.Load("FlowNodes"); } catch { }
        Container.Resolve<IFlowToolDiscoveryService>().Discover();

        // 未注册 IFlowLogSink：节点日志仅走 ConsoleOutputRouter 进底部输出面板，
        // 不落盘。如需按流程落盘，可在此接入 DataManager 的 LogManager。

        var regionManager = Container.Resolve<Prism.Regions.IRegionManager>();
        regionManager.RequestNavigate("FlowRegion", "FlowChartContainerView", result =>
        {
            if (result.Result != true) return;
            var view = result.Context.NavigationService.Region.ActiveViews
                .OfType<FrameworkElement>().FirstOrDefault();
            if (view?.DataContext is FlowEditor.ViewModels.FlowChartContainerViewModel vm)
                vm.ProcessName = "FlowNodesWorkbench";
        });
    }
}
