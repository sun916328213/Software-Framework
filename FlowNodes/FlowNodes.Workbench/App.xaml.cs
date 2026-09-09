using System.Reflection;
using System.Windows;
using Communication;
using CommunicationNew;
using DataManager;
using Device;
using FlowEditor;
using FlowEditor.Interfaces;
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
    }

    protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
    {
        // 节点构造依赖（MCHelper / CamManager 等）由这些模块提供单例，
        // 缺失会导致含 DI 构造参数的节点无法物化。
        moduleCatalog.AddModule<DataManagerModule>();
        moduleCatalog.AddModule<CommunicationModule>();
        moduleCatalog.AddModule<CommunicationNewModule>();
        moduleCatalog.AddModule<DeviceModule>();
        moduleCatalog.AddModule<WPFCustomControlsModule>();
        moduleCatalog.AddModule<FlowEditorModule>();
        moduleCatalog.AddModule<ScriptEditorModule>();
    }

    protected override void OnInitialized()
    {
        // base 会跑 ModuleManager.Run()：CreateShell 时模块尚未加载，
        // 必须在这里（模块就绪后）再导航创建编辑器视图，否则 AutoWire 解析 VM 失败。
        base.OnInitialized();

        // 强制加载 FlowNodes 程序集，确保其节点能被工具箱扫描到（与 Packing 一致）。
        try { Assembly.Load("FlowNodes"); } catch { }
        Container.Resolve<IFlowToolDiscoveryService>().Discover();

        // NodeLog.Resolver 保持未注册：节点日志仅走 ConsoleOutputRouter 进底部输出面板，
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
