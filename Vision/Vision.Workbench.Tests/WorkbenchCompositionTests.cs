using System.Windows;

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