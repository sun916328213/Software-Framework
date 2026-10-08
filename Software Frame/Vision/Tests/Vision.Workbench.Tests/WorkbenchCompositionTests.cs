// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：WorkbenchCompositionTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Workbench、System.Windows、System.Linq、Extension.UI.Theme、MaterialDesignThemes.Wpf。导入不代表所有类型都会在运行时加载。
using Vision.Workbench;
using System.Windows;
using System.Linq;
using Extension.UI.Theme;
using MaterialDesignThemes.Wpf;

namespace Vision.Workbench.Tests;

/// <summary>
/// 宿主装配冒烟测试：App.xaml 的资源合并（含 <c>Vision.Controls</c> 的 ResourceDic）能跨程序集解析，
/// 主窗 / ToolBrowser 的 BAML 在「入口程序集不是 Vision.Workbench」时依然找得到。
/// <para>
/// 为什么必须在这条测试线上：Vision.Workbench 的 App.xaml 原先写的是相对 <c>StartupUri="MainWindow.xaml"</c>，
/// 相对 URI 会绑到**入口程序集**（生产是 Vision.Workbench.exe，vstest 下是 testhost.exe）
/// ⇒ 宿主换成测试进程后立刻报「找不到资源 views/mainwindow.xaml」。
/// 这类装配问题在编译期完全不报错，只有真正把宿主拉起来才暴露。
/// </para>
/// </summary>
[Collection("WorkbenchAppBoot")]
public sealed class WorkbenchCompositionTests(WorkbenchAppFixture host)
{
    private readonly WorkbenchAppFixture _host = host;

    /// <summary>
    /// App 启动按 StartupUri 真正拉起了 MainWindow —— 2026-09-29 删掉旧 HALCON 页签后，
    /// 主窗构造不再需要 HALCON Runtime，宿主装配这一环终于可以端到端验。
    /// </summary>
    [Fact]
    public void AppBoot_ShowsMainWindow_WithoutHalconRuntime()
    {
        _host.Invoke(() =>
        {
            var window = Application.Current!.MainWindow;
            Assert.NotNull(window);
            Assert.Equal(typeof(Vision.Workbench.MainWindow), window!.GetType());
        });
    }

    /// <summary>
    /// 测试相关入口 ThemeService_UsesHostThemeBeforeNestedControlsTheme；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、Single、OfType、Same、FindBundledTheme；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ThemeService_UsesHostThemeBeforeNestedControlsTheme()
    {
        _host.Invoke(() =>
        {
            var hostTheme = Assert.Single(Application.Current!.Resources.MergedDictionaries.OfType<BundledTheme>());
            Assert.Same(hostTheme, ThemeService.FindBundledTheme());
        });
    }

    /// <summary>
    /// 测试相关入口 AppBoot_MergesWorkbenchAndControlsResources；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、NotNull、True；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void AppBoot_MergesWorkbenchAndControlsResources()
    {
        _host.Invoke(() =>
        {
            Assert.NotNull(Application.Current);

            // App.xaml 合并了三条：Vision.Controls 的 ResourceDic、MaterialDesign BundledTheme、MaterialDesign3 Defaults。
            var merged = Application.Current!.Resources.MergedDictionaries.Count;
            Assert.True(merged >= 3, $"App.xaml 的 MergedDictionaries 只解析出 {merged} 条，资源合并疑似失败。");
        });
    }

    /// <summary>
    /// 测试相关入口 PackUri_ResolvesBaml_FromForeignEntryAssembly；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、GetResourceStream、NotNull；具体条件和先后次序见方法体。
    /// </summary>
    [Theory]
    [InlineData("pack://application:,,,/Vision.Workbench;component/MainWindow.xaml")]
    [InlineData("pack://application:,,,/Vision.Workbench;component/ToolBrowserView.xaml")]
    [InlineData("pack://application:,,,/Vision.Controls;component/Ctr_Halcon/View/ResourceDic .xaml")]
    public void PackUri_ResolvesBaml_FromForeignEntryAssembly(string uri)
    {
        _host.Invoke(() =>
        {
            // ⛔ 只解析资源流、不实例化：pack URI 的可解析性单独钉住，
            //    与 MainWindow 的实例化（AppBoot_ShowsMainWindow_WithoutHalconRuntime）解耦。
            var resource = Application.GetResourceStream(new Uri(uri, UriKind.Absolute));
            Assert.NotNull(resource);
        });
    }
}
