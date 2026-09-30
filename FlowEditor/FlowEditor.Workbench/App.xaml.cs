using System.Linq;
using System.Windows;
using FlowEditor.ViewModels;
using FlowEditor.Workbench.Views;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Regions;

namespace FlowEditor.Workbench;

/// <summary>
/// FlowEditor 工作台入口（Prism 宿主）：只负责壳、模块组合与区域导航，
/// 编辑器视图与服务都由 FlowEditor.Controls 中的 FlowEditorModule 提供。
/// </summary>
public partial class App : PrismApplication
{
    private bool _smoke;

    /// <summary>壳窗口：模块加载前只构造空壳，编辑器视图在 OnInitialized 由区域导航装入。</summary>
    protected override Window CreateShell() => Container.Resolve<MainWindow>();

    /// <summary>注册壳窗口（业务服务全部由 FlowEditorModule 注册，此处只登记宿主自身类型）。</summary>
    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<MainWindow>();
    }

    /// <summary>模块目录：只加载 FlowEditor 模块，内建节点随模块注册。</summary>
    protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
    {
        moduleCatalog.AddModule<FlowEditorModule>();
    }

    /// <summary>解析 --smoke 参数，供无人工干预的启动冒烟使用。</summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        _smoke = e.Args.Contains("--smoke", StringComparer.OrdinalIgnoreCase);
        base.OnStartup(e);
    }

    /// <summary>壳与模块就绪后导航出流程图编辑器；--smoke 时加载完成即关闭。</summary>
    protected override void OnInitialized()
    {
        base.OnInitialized(); // 此时 FlowEditorModule 已 OnInitialized（资源已合并）

        var regionManager = Container.Resolve<IRegionManager>();
        regionManager.RequestNavigate("FlowEditorRegion", "FlowChartContainerView", result =>
        {
            if (result.Result != true)
                return;

            var view = regionManager.Regions["FlowEditorRegion"].ActiveViews
                .OfType<FrameworkElement>()
                .FirstOrDefault();
            if (view?.DataContext is FlowChartContainerViewModel vm)
                vm.ProcessName = "FlowEditorWorkbench";
        });

        if (_smoke && MainWindow is not null)
            MainWindow.Loaded += (_, _) => MainWindow.Close();
    }
}
