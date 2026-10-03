// 【中文阅读说明】
// MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：WorkbenchSimulationTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：MotionControl.Controls.Services、MotionControl.Controls.ViewModels、MotionControl.Core.Definitions。导入不代表所有类型都会在运行时加载。
using MotionControl.Controls.Services;
using MotionControl.Controls.ViewModels;
using MotionControl.Core.Definitions;

namespace MotionControl.Workbench.Tests;

/// <summary>
/// 宿主 ViewModel 行为测试：只用 Simulation 厂商（无硬件），验证连接/断开状态机。
/// ViewModel 使用 WPF 弱事件，创建和释放均在宿主 STA Dispatcher 上完成。
/// </summary>
[Collection("WorkbenchAppBoot")]
public sealed class WorkbenchSimulationTests
{
    private readonly WorkbenchAppFixture _host;

    /// <summary>
    /// 初始化 WorkbenchSimulationTests 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    public WorkbenchSimulationTests(WorkbenchAppFixture host) => _host = host;

    /// <summary>
    /// 验证 Connect_WithSimulation_PopulatesAxes 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Invoke、Equal、GetApartmentState、Connect、True；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Connect_WithSimulation_PopulatesAxes() => _host.Invoke(() =>
    {
        Assert.Equal(ApartmentState.STA, Thread.CurrentThread.GetApartmentState());
        using var workspace = new MotionWorkspace();
        using var vm = new MotionViewModel(workspace)
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
    });

    /// <summary>
    /// 验证 Connect_WithInvalidAxisCount_FailsWithoutThrowing 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Invoke、Equal、GetApartmentState、Connect、False；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Connect_WithInvalidAxisCount_FailsWithoutThrowing() => _host.Invoke(() =>
    {
        Assert.Equal(ApartmentState.STA, Thread.CurrentThread.GetApartmentState());
        using var workspace = new MotionWorkspace();
        using var vm = new MotionViewModel(workspace)
        {
            SelectedVendor = EMotionCardType.Simulation,
            AxisCount = 0,
        };

        vm.Connect();

        Assert.False(vm.IsConnected);
        Assert.Null(vm.SelectedCard);
        Assert.Contains("轴数", vm.StatusMessage);
    });
}
