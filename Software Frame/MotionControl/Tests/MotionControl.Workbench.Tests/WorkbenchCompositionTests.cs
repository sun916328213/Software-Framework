// 【中文阅读说明】
// MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：WorkbenchCompositionTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：MotionControl.Controls.Services、MotionControl.Controls.ViewModels、MotionControl.Workbench、System.Windows、MotionControl.Controls.Views、MotionControl.Core.Definitions。导入不代表所有类型都会在运行时加载。
using MotionControl.Controls.Services;
using MotionControl.Controls.ViewModels;
using MotionControl.Workbench;
using System.Windows;
using MotionControl.Controls.Views;
using MotionControl.Core.Definitions;
using MotionControl.Workbench.ViewModels;
using MotionControl.Workbench.Views;
using Prism.Ioc;

namespace MotionControl.Workbench.Tests;

/// <summary>
/// 宿主装配冒烟测试：主窗可构造、复用控件可实例化并绑定仿真卡。
/// 任一 XAML 资源、控件依赖属性或装配接线问题都会在此失败；真实硬件不参与。
/// </summary>
[Collection("WorkbenchAppBoot")]
public sealed class WorkbenchCompositionTests(WorkbenchAppFixture host)
{
    private readonly WorkbenchAppFixture _host = host;

    /// <summary>
    /// 测试相关入口 AppBoot_MainWindowIsShown；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、NotNull、Equal、TryFindResource；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void AppBoot_MainWindowIsShown()
    {
        _host.Invoke(() =>
        {
            Assert.NotNull(Application.Current);
            Assert.NotNull(Application.Current.MainWindow);
            Assert.Equal(WindowStartupLocation.CenterScreen, Application.Current.MainWindow.WindowStartupLocation);
            Assert.NotNull(Application.Current.TryFindResource("MaterialDesignOutlinedTextBox"));
        });
    }

    /// <summary>
    /// 测试相关入口 HomeRegion_ConstructsWithViewModel；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、Resolve、IsType、Single、Contains；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void HomeRegion_ConstructsWithViewModel()
    {
        _host.Invoke(() =>
        {
            var region = Prism.Ioc.ContainerLocator.Container.Resolve<Prism.Regions.IRegionManager>().Regions["MotionRegion"];
            var page = Assert.IsType<MotionView>(Assert.Single(region.ActiveViews));
            var vm = Assert.IsType<MotionViewModel>(page.DataContext);
            Assert.Contains(EMotionCardType.Simulation, vm.Vendors);
            Assert.Equal(EMotionCardType.Simulation, vm.SelectedVendor);
            Assert.False(vm.IsConnected);
        });
    }

    /// <summary>
    /// 测试相关入口 Controls_InstantiateAndBindSimulationCard；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、Connect、True、NotNull、Same；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Controls_InstantiateAndBindSimulationCard()
    {
        _host.Invoke(() =>
        {
            using var workspace = new MotionWorkspace();
            using var vm = new MotionViewModel(workspace)
            {
                SelectedVendor = EMotionCardType.Simulation,
                AxisCount = 1,
            };
            vm.Connect();
            Assert.True(vm.IsConnected);
            Assert.NotNull(vm.SelectedCard);
            var card = vm.SelectedCard!;

            var status = new AxisStatusControl { MotionCard = card, AxisIndex = 0 };
            var jog = new AxisJogControl { MotionCard = card, AxisIndex = 0 };
            var operation = new AxisOperationControl { MotionCard = card, AxisIndex = 0 };
            var overview = new MotionOverviewControl { MotionCard = card };

            Assert.Same(card, status.MotionCard);
            Assert.Same(card, jog.MotionCard);
            Assert.Same(card, operation.MotionCard);
            Assert.Same(card, overview.MotionCard);
            Assert.NotNull(status.ViewModel);
            Assert.True(status.ViewModel!.IsAvailable);

            vm.Disconnect();
        });
    }
}
