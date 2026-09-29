using System.Windows;
using Communication.Controls.Services;
using Communication.Controls.ViewModels;
using Communication.Controls.Views;
using Communication.Workbench.ViewModels;
using Prism.Ioc;
using Prism.Services.Dialogs;

namespace Communication.Workbench.Tests;

/// <summary>
/// 宿主装配冒烟测试：App 启动拉起主窗、App.xaml 资源合并跨程序集可解析、
/// 页面 ViewModel 与 UserControl 能在真实 DI 容器下组装并绑定。
/// 这些装配问题编译期不报错，只有把宿主真正拉起来才暴露；真实设备不参与。
/// </summary>
[Collection("WorkbenchAppBoot")]
public sealed class WorkbenchCompositionTests(WorkbenchAppFixture host)
{
    private readonly WorkbenchAppFixture _host = host;

    [Fact]
    public void AppBoot_ShowsMainWindow_WithComposedViewModel()
    {
        _host.Invoke(() =>
        {
            Assert.NotNull(Application.Current);

            var window = Application.Current!.MainWindow;
            Assert.NotNull(window);
            Assert.IsType<Communication.Workbench.Views.MainWindow>(window);

            // DataContext 由 Prism 约定装配（Views.MainWindow → ViewModels.MainWindowViewModel），
            // 代码里没有显式 new —— 这条断言守住该约定一旦被破坏。
            var vm = Assert.IsType<MainWindowViewModel>(window!.DataContext);
            Assert.NotEmpty(vm.NavItems);
        });
    }

    [Fact]
    public void AppBoot_MergesShellResources()
    {
        _host.Invoke(() =>
        {
            Assert.NotNull(Application.Current);

            // App.xaml 合并了四条：MaterialDesign BundledTheme、MD3 Defaults、
            // WPFCustomControls Resources、壳层 Styles/Theme.xaml。
            var merged = Application.Current!.Resources.MergedDictionaries.Count;
            Assert.True(merged >= 4, $"App.xaml 的 MergedDictionaries 只解析出 {merged} 条，资源合并疑似失败。");
        });
    }

    [Theory]
    [InlineData("pack://application:,,,/Communication.Workbench;component/Views/MainWindow.xaml")]
    [InlineData("pack://application:,,,/Communication.Workbench;component/Styles/Theme.xaml")]
    [InlineData("pack://application:,,,/Communication.Controls;component/Views/CommunicationView.xaml")]
    [InlineData("pack://application:,,,/Communication.Controls;component/Views/DeviceConfigDialog.xaml")]
    [InlineData("pack://application:,,,/Communication.Controls;component/Themes/PageStyles.xaml")]
    public void PackUri_ResolvesBaml_FromForeignEntryAssembly(string uri)
    {
        _host.Invoke(() =>
        {
            // ⛔ 只解析资源流、不实例化：pack URI 可解析性单独钉住，
            //    与页面的实例化（CommunicationView_InstantiatesAndBindsComposedViewModel）解耦。
            var resource = Application.GetResourceStream(new Uri(uri, UriKind.Absolute));
            Assert.NotNull(resource);
        });
    }

    [Fact]
    public void CommunicationView_InstantiatesAndBindsComposedViewModel()
    {
        _host.Invoke(() =>
        {
            // 页面 VM 经宿主 DI 容器解析：会话服务 / 日志服务 / 对话框服务三个注册项
            // 少一个，这里就构造失败 —— 这正是「DI 装配」的验收点。
            var container = ContainerLocator.Container;
            var vm = new CommunicationViewModel(
                container.Resolve<CommunicationSessionService>(),
                container.Resolve<ILogService>(),
                container.Resolve<IDialogService>());

            var view = new CommunicationView { DataContext = vm };

            Assert.Same(vm, view.DataContext);
            Assert.False(vm.HasDevices);
            // 构造即写一条会话就绪日志，证明日志服务 → 集合绑定这一环是通的。
            Assert.Contains(vm.Logs, entry => entry.Source == "Session");
        });
    }
}