// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：WorkbenchCompositionTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Workbench、SecsGem.Controls、System.Windows、DataManager.Core.Log、DataManager.Runtime.Log、Prism.Ioc。导入不代表所有类型都会在运行时加载。
using SecsGem.Workbench;
using SecsGem.Workbench.Views;
using SecsGem.Controls;
using System.Windows;
using DataManager.Core.Log;
using DataManager.Runtime.Log;
using Prism.Ioc;
using Prism.Modularity;
using DataManager.Controls;
using SecsGem.Runtime.Manager;
using SecsGem.Runtime.Variable;
using SecsGem.Runtime.Variable.Data;
using SecsGem.Controls.Views;
using SecsGem.Core;
using SecsGem.Runtime;
using SecsGem.Runtime.Protocol.Registry;

namespace SecsGem.Workbench.Tests;

/// <summary>
/// 宿主装配验收：把 SecsGem.Workbench 真正启动起来，验证 App.xaml 资源合并、
/// Prism 模块目录（DataManager 共享服务 + SecsGem 协议目录与运行时注册）
/// 与跨程序集 BAML 资源可解析性。这些装配问题编译期不报错，只有拉起宿主才暴露。
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
    /// 测试相关入口 AppBoot_ShowsShell_MainWindow；准备条件、协作调用与结果检查以方法正文为准。
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
    /// 测试相关入口 AppBoot_InitializesCatalogModulesBeforeResolvingShellServices；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、Resolve、Single、Equal、Same；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void AppBoot_InitializesCatalogModulesBeforeResolvingShellServices()
    {
        _fixture.Invoke(() =>
        {
            var catalog = ContainerLocator.Container.Resolve<IModuleCatalog>();
            var data = Assert.Single(catalog.Modules, info =>
                info.ModuleType == typeof(DataManagerModule).AssemblyQualifiedName);
            var secs = Assert.Single(catalog.Modules, info =>
                info.ModuleType == typeof(SecsGemModule).AssemblyQualifiedName);
            Assert.Equal(ModuleState.Initialized, data.State);
            Assert.Equal(ModuleState.Initialized, secs.State);
            Assert.Same(ContainerLocator.Container.Resolve<LogManager>(),
                ContainerLocator.Container.Resolve<ILogManager>());
        });
    }

    /// <summary>
    /// 测试相关入口 AppBoot_MergesShellResources；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、True；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void AppBoot_MergesShellResources()
    {
        var merged = _fixture.Invoke(() => Application.Current.Resources.MergedDictionaries.Count);

        // App.xaml 合并了 MaterialDesign BundledTheme 与 MaterialDesign3.Defaults.xaml 两类资源。
        Assert.True(merged > 0, $"App.xaml 的 MergedDictionaries 只解析出 {merged} 条，资源合并疑似失败。");
    }

    /// <summary>
    /// 测试相关入口 Container_RegistersCatalogSingletons_FromModule；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、NotNull、Resolve；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Container_RegistersCatalogSingletons_FromModule()
    {
        _fixture.Invoke(() =>
        {
            var container = ContainerLocator.Container;

            // SecsGemModule 里的 GEM 目录单例：少一个，主窗构造期 Resolve<VIDManager> 就会失败。
            Assert.NotNull(container.Resolve<ReportID>());
            Assert.NotNull(container.Resolve<CEID>());
            Assert.NotNull(container.Resolve<DVID>());
            Assert.NotNull(container.Resolve<SVID>());
            Assert.NotNull(container.Resolve<ECID>());
            Assert.NotNull(container.Resolve<Alarm>());
            Assert.NotNull(container.Resolve<ActiveReceive>());
        });
    }

    /// <summary>
    /// 测试相关入口 Container_ResolvesLogManager_AsHostSingleton；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、Resolve、NotNull、Same；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Container_ResolvesLogManager_AsHostSingleton()
    {
        var (first, second) = _fixture.Invoke(() =>
            (ContainerLocator.Container.Resolve<LogManager>(), ContainerLocator.Container.Resolve<LogManager>()));

        Assert.NotNull(first);
        Assert.Same(first, second);
    }

    /// <summary>
    /// 测试相关入口 Container_RegistersSecsGemModuleServices；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、NotNull、Resolve；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 Container_RuntimeFacade_IsSingleton_AcrossContracts；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、Resolve、Same；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Container_RuntimeFacade_IsSingleton_AcrossContracts()
    {
        var (asRuntime, asContract) = _fixture.Invoke(() =>
            (ContainerLocator.Container.Resolve<SecsGemRuntime>(),
             ContainerLocator.Container.Resolve<ISecsGemRuntime>()));

        // SecsGemModule 用 provider 工厂把两个注册项都指向同一个 SecsGemRuntime 单例。
        Assert.Same(asRuntime, asContract);
    }

    /// <summary>
    /// 测试相关入口 ControlsWpf_SecsOverviewView_InstantiatesOnUiThread；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、IsType；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ControlsWpf_SecsOverviewView_InstantiatesOnUiThread()
    {
        var view = _fixture.Invoke(() => new SecsOverviewView());

        Assert.IsType<SecsOverviewView>(view);
    }

    /// <summary>
    /// 测试相关入口 PackUri_ResolvesBaml_FromForeignEntryAssembly；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、GetResourceStream、True；具体条件和先后次序见方法体。
    /// </summary>
    [Theory]
    [InlineData("pack://application:,,,/SecsGem.Workbench;component/Views/MainWindow.xaml")]
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