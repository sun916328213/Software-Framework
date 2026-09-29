using MotionControl.Core.Definitions;
using MotionControl.Workbench.ViewModels;

namespace MotionControl.Workbench.Tests;

/// <summary>
/// 宿主 ViewModel 行为测试：只用 Simulation 厂商（无硬件），验证连接/断开状态机。
/// 纯状态断言，不需要 STA；仿真之外的厂商连接失败属于环境依赖，不在此覆盖。
/// </summary>
public sealed class WorkbenchSimulationTests
{
    [Fact]
    public void Connect_WithSimulation_PopulatesAxes()
    {
        using var vm = new MainViewModel
        {
            SelectedVendor = EMotionCardType.Simulation,
            AxisCount = 2,
        };

        vm.Connect();

        try
        {
            Assert.True(vm.IsConnected);
            Assert.NotNull(vm.SelectedCard);
            Assert.Equal([0, 1], vm.AxisIndices);
            Assert.Equal(0, vm.SelectedAxisIndex);
        }
        finally
        {
            vm.Disconnect();
        }

        Assert.False(vm.IsConnected);
        Assert.Null(vm.SelectedCard);
        Assert.Equal("已断开", vm.StatusMessage);
    }

    [Fact]
    public void Connect_WithInvalidAxisCount_FailsWithoutThrowing()
    {
        using var vm = new MainViewModel
        {
            SelectedVendor = EMotionCardType.Simulation,
            AxisCount = 0,
        };

        vm.Connect();

        Assert.False(vm.IsConnected);
        Assert.Null(vm.SelectedCard);
        Assert.Contains("轴数", vm.StatusMessage);
    }
}
