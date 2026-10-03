// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：WorkbenchCompositionTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Workbench、System.Windows、Device.Controls.Views、Device.Core.Cameras、Device.Core.FrameGrabbers、Device.Core.LabelPrinting。导入不代表所有类型都会在运行时加载。
using Device.Workbench;
using System.Windows;
using Device.Controls.Views;
using Device.Core.Cameras;
using Device.Core.FrameGrabbers;
using Device.Core.LabelPrinting;
using Device.Core.Lights;
using Device.Core.Printers;
using Device.Core.Rfid;
using Device.Core.Scanners;
using Device.Workbench.Infrastructure;
using Device.Workbench.ViewModels;
using Device.Workbench.Views;
using Extension.UI.Theme;
using Prism.Ioc;
using Prism.Regions;

namespace Device.Workbench.Tests;

/// <summary>
/// 宿主装配验收：把 Device.Workbench 真正启动起来，验证 App.xaml 资源合并、
/// Prism 容器（工厂来自 Device.Runtime、页面来自 Device.Controls）与区域导航。
/// 这些装配问题编译期不报错，只有拉起宿主才暴露。
/// </summary>
[Collection("WorkbenchAppBoot")]
public sealed class WorkbenchCompositionTests
{
    private readonly WorkbenchAppFixture _fixture;

    /// <summary>
    /// 初始化 WorkbenchCompositionTests 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    public WorkbenchCompositionTests(WorkbenchAppFixture fixture) => _fixture = fixture;

    /// <summary>
    /// 验证 AppBoot_ShowsShell_MainWindow 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Invoke、IsType、False、IsNullOrWhiteSpace；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void AppBoot_ShowsShell_MainWindow()
    {
        var (shell, title) = _fixture.Invoke(() =>
            (Application.Current.MainWindow, Application.Current.MainWindow?.Title ?? string.Empty));

        Assert.IsType<MainWindow>(shell);
        Assert.False(string.IsNullOrWhiteSpace(title), "宿主主窗应设置标题。");
    }

    /// <summary>
    /// 验证 Shell_AutoWiresMainWindowViewModel_WithDefaultNavItems 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Invoke、IsType、NotEmpty、Contains；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Shell_AutoWiresMainWindowViewModel_WithDefaultNavItems()
    {
        var vm = _fixture.Invoke(() => ((MainWindow)Application.Current.MainWindow!).DataContext);

        var viewModel = Assert.IsType<MainWindowViewModel>(vm);
        Assert.NotEmpty(viewModel.NavItems);
        // 默认页必须与 RegisterForNavigation 的视图名一致，否则侧栏点进去就是空区域。
        Assert.Contains(viewModel.NavItems, item => item.ViewName == "CameraView");
        Assert.Contains(viewModel.NavItems, item => item.ViewName == "LabelPrintView");
    }

    /// <summary>
    /// 验证 AppBoot_MergesHostAndSharedResources 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Invoke、ToArray、Cast、Where、Select；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void AppBoot_MergesHostAndSharedResources()
    {
        var sources = _fixture.Invoke(() => Application.Current.Resources.MergedDictionaries
            .Select(dictionary => dictionary.Source?.OriginalString)
            .Where(uri => uri is not null)
            .Cast<string>()
            .ToArray());

        // App.xaml 是入口程序集自身的资源字典，相对 URI 会绑到 Device.Workbench 上。
        Assert.Contains(sources, uri => uri.EndsWith("Styles/Theme.xaml", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(sources, uri => uri.EndsWith("/WPFCustomControls;component/Resources/Resources.xaml", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(sources, uri => uri.Contains("MaterialDesign3.Defaults.xaml", StringComparison.OrdinalIgnoreCase));
        // BundledTheme 是对象而非字典，Source 为空——至少要有一个非 Source 项，证明主题实例已挂上。
        var hasThemeInstance = _fixture.Invoke(() => Application.Current.Resources.MergedDictionaries
            .Any(dictionary => dictionary.Source is null));
        Assert.True(hasThemeInstance, "App.xaml 应挂上 BundledTheme 实例（Source 为空的合并项）。");
    }

    /// <summary>
    /// 验证 PackUri_ResolvesBaml_FromForeignEntryAssembly 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Invoke、GetResourceStream、True；具体条件和先后次序见方法体。
    /// </summary>
    [Theory]
    [InlineData("pack://application:,,,/Device.Workbench;component/Views/MainWindow.xaml")]
    [InlineData("pack://application:,,,/Device.Controls;component/Themes/PageStyles.xaml")]
    [InlineData("pack://application:,,,/Device.Controls;component/Views/CameraView.xaml")]
    [InlineData("pack://application:,,,/WPFCustomControls;component/Resources/Resources.xaml")]
    public void PackUri_ResolvesBaml_FromForeignEntryAssembly(string packUri)
    {
        var resolved = _fixture.Invoke(() =>
            Application.GetResourceStream(new Uri(packUri, UriKind.Absolute)) is not null);

        Assert.True(resolved, $"pack URI 未能解析：{packUri}");
    }

    /// <summary>
    /// 验证 Container_RegistersRuntimeFactories_FromHostOnly 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 NotNull、Resolve；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Container_RegistersRuntimeFactories_FromHostOnly()
    {
        var container = ContainerLocator.Container;

        // 只有宿主引用 Device.Runtime，工厂实例必须由宿主注入；Controls 只面向 Core 契约。
        Assert.NotNull(container.Resolve<ICameraFactory>());
        Assert.NotNull(container.Resolve<IFrameGrabberFactory>());
        Assert.NotNull(container.Resolve<ILightControllerFactory>());
        Assert.NotNull(container.Resolve<IScannerFactory>());
        Assert.NotNull(container.Resolve<IPrinterFactory>());
        Assert.NotNull(container.Resolve<IRfidDeviceFactory>());
        Assert.NotNull(container.Resolve<ILabelPrintEngineFactory>());
    }

    /// <summary>
    /// 验证 Container_ThemeService_IsSingleton 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Invoke、Resolve、Same；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Container_ThemeService_IsSingleton()
    {
        var (first, second) = _fixture.Invoke(() =>
            (ContainerLocator.Container.Resolve<ThemeService>(), ContainerLocator.Container.Resolve<ThemeService>()));

        Assert.Same(first, second);
    }

    /// <summary>
    /// 验证 MainRegion_NavigatesToCameraView_ThroughPrism 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Invoke、RequestNavigate、Resolve、WaitUntil、Any；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void MainRegion_NavigatesToCameraView_ThroughPrism()
    {
        _fixture.Invoke(() => ContainerLocator.Container.Resolve<IRegionManager>()
            .RequestNavigate(RegionNames.MainRegion, "CameraView"));

        var navigated = _fixture.WaitUntil(
            () => ContainerLocator.Container.Resolve<IRegionManager>()
                .Regions[RegionNames.MainRegion].Views.OfType<CameraView>().Any(),
            TimeSpan.FromSeconds(10));

        Assert.True(navigated, "MainRegion 应能通过 Prism 导航解析出 CameraView（页面来自 Device.Controls）。");
    }
}