using System.Windows;
using MotionControl.Controls.Views;
using MotionControl.Core.Definitions;
using MotionControl.Workbench.ViewModels;
using MotionControl.Workbench.Views;

namespace MotionControl.Workbench.Tests;

/// <summary>
/// 宿主装配冒烟测试：主窗可构造、复用控件可实例化并绑定仿真卡。
/// 任一 XAML 资源、控件依赖属性或装配接线问题都会在此失败；真实硬件不参与。
/// </summary>
[Collection("WorkbenchAppBoot")]
public sealed class WorkbenchCompositionTests(WorkbenchAppFixture host)
{
    private readonly WorkbenchAppFixture _host = host;

    [Fact]
    public void AppBoot_MainWindowIsShown()
    {
        _host.Invoke(() =>
        {
            Assert.NotNull(Application.Current);
            Assert.NotNull(Application.Current.MainWindow);
        });
    }

    [Fact]
    public void MainWindow_ConstructsWithViewModel()
    {
        _host.Invoke(() =>
        {
            var window = new MainWindow();

            var vm = Assert.IsType<MainViewModel>(window.DataContext);
            Assert.Contains(EMotionCardType.Simulation, vm.Vendors);
            Assert.Equal(EMotionCardType.Simulation, vm.SelectedVendor);
            Assert.False(vm.IsConnected);
        });
    }

    [Fact]
    public void Controls_InstantiateAndBindSimulationCard()
    {
        _host.Invoke(() =>
        {
            using var vm = new MainViewModel
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
