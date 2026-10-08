// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：WorkbenchCompositionTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Workbench、System.Windows、Communication.Controls.Services、Communication.Controls.ViewModels、Communication.Controls.Views、Communication.Workbench.ViewModels。导入不代表所有类型都会在运行时加载。
using Communication.Workbench;
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

    /// <summary>
    /// 测试相关入口 AppBoot_ShowsMainWindow_WithComposedViewModel；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、NotNull、IsType、NotEmpty；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 AppBoot_MergesShellResources；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、NotNull、True；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 PackUri_ResolvesBaml_FromForeignEntryAssembly；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、GetResourceStream、NotNull；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 CommunicationView_InstantiatesAndBindsComposedViewModel；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、Resolve、Same、False、Contains；具体条件和先后次序见方法体。
    /// </summary>
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