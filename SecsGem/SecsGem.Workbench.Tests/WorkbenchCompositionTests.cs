using System.Windows;
using DataManager.Log;
using Prism.Ioc;
using Secs.Manager;
using Secs.Variable;
using Secs.Variable.Data;
using SecsGem.Controls.Views;
using SecsGem.Core;
using SecsGem.Runtime;
using SecsGem.Runtime.Protocol.Registry;

namespace SecsGem.Workbench.Tests;

/// <summary>
/// 宿主装配验收：把 SecsGem.Workbench 真正启动起来，验证 App.xaml 资源合并、
/// Prism 容器（App.RegisterTypes 的目录单例 + SecsGemModule 的运行时注册）
/// 与跨程序集 BAML 资源可解析性。这些装配问题编译期不报错，只有拉起宿主才暴露。
/// </summary>
[Collection("WorkbenchAppBoot")]
public sealed class WorkbenchCompositionTests
{
    private readonly WorkbenchAppFixture _fixture;

    public WorkbenchCompositionTests(WorkbenchAppFixture fixture) => _fixture = fixture;

    [Fact]
    public void AppBoot_ShowsShell_MainWindow()
    {
        var (shell, title) = _fixture.Invoke(() =>
            (Application.Current.MainWindow, Application.Current.MainWindow?.Title ?? string.Empty));

        Assert.IsType<MainWindow>(shell);
        Assert.False(string.IsNullOrWhiteSpace(title), "宿主主窗应设置标题。");
    }

    [Fact]
    public void AppBoot_MergesShellResources()
    {
        var merged = _fixture.Invoke(() => Application.Current.Resources.MergedDictionaries.Count);

        // App.xaml 合并了 MaterialDesign BundledTheme 与 MaterialDesign3.Defaults.xaml 两类资源。
        Assert.True(merged > 0, $"App.xaml 的 MergedDictionaries 只解析出 {merged} 条，资源合并疑似失败。");
    }

    [Fact]
    public void Container_RegistersHostCatalogSingletons_FromApp()
    {
        _fixture.Invoke(() =>
        {
            var container = ContainerLocator.Container;

            // App.RegisterTypes 里的 GEM 目录单例：少一个，主窗构造期 Resolve<VIDManager> 就会失败。
            Assert.NotNull(container.Resolve<ReportID>());
            Assert.NotNull(container.Resolve<CEID>());
            Assert.NotNull(container.Resolve<DVID>());
            Assert.NotNull(container.Resolve<SVID>());
            Assert.NotNull(container.Resolve<ECID>());
            Assert.NotNull(container.Resolve<Alarm>());
            Assert.NotNull(container.Resolve<ActiveReceive>());
        });
    }

    [Fact]
    public void Container_ResolvesLogManager_AsHostSingleton()
    {
        var (first, second) = _fixture.Invoke(() =>
            (ContainerLocator.Container.Resolve<LogManager>(), ContainerLocator.Container.Resolve<LogManager>()));

        Assert.NotNull(first);
        Assert.Same(first, second);
    }

    [Fact]
    public void Container_RegistersSecsGemModuleServices()
    {
        _fixture.Invoke(() =>
        {
            var container = ContainerLocator.Container;

            // SecsGemModule.RegisterTypes：运行时门面与消息注册表，MainWindow / 传输层直接依赖。
            Assert.NotNull(container.Resolve<ISecsGemRuntime>());
            Assert.NotNull(container.Resolve<SecsGemRuntime>());
            Assert.NotNull(container.Resolve<VIDManager>());
            Assert.NotNull(container.Resolve<SecsMessageRegistry>());
        });
    }

    [Fact]
    public void Container_RuntimeFacade_IsSingleton_AcrossContracts()
    {
        var (asRuntime, asContract) = _fixture.Invoke(() =>
            (ContainerLocator.Container.Resolve<SecsGemRuntime>(),
             ContainerLocator.Container.Resolve<ISecsGemRuntime>()));

        // SecsGemModule 用 provider 工厂把两个注册项都指向同一个 SecsControl 单例。
        Assert.Same(asRuntime, asContract);
    }

    [Fact]
    public void ControlsWpf_SecsOverviewView_InstantiatesOnUiThread()
    {
        var view = _fixture.Invoke(() => new SecsOverviewView());

        Assert.IsType<SecsOverviewView>(view);
    }

    [Theory]
    [InlineData("pack://application:,,,/SecsGem.Workbench;component/MainWindow.xaml")]
    [InlineData("pack://application:,,,/SecsGem.Controls;component/Views/SecsOverviewView.xaml")]
    [InlineData("pack://application:,,,/SecsGem.Controls;component/Terminal/MessagePrompt.xaml")]
    public void PackUri_ResolvesBaml_FromForeignEntryAssembly(string packUri)
    {
        _fixture.Invoke(() =>
        {
            // ⛔ 只解析资源流、不实例化：pack URI 可解析性单独钉住，
            //    与页面的实例化（ControlsWpf_SecsOverviewView_InstantiatesOnUiThread）解耦。
            var resolved = Application.GetResourceStream(new Uri(packUri, UriKind.Absolute)) is not null;
            Assert.True(resolved, $"pack URI 未能解析：{packUri}");
        });
    }
}