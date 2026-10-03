// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：LightControllerFactoryTests、ThrowingLightController。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Core.Devices、Device.Core.Lights、Device.Runtime.Lights、Xunit。导入不代表所有类型都会在运行时加载。
using Device.Core.Devices;
using Device.Core.Lights;
using Device.Runtime.Lights;
using Xunit;

namespace Device.Tests.Runtime;

/// <summary>
/// 验证统一光源控制器工厂的驱动目录和品牌路由。
/// </summary>
public sealed class LightControllerFactoryTests
{
    private static readonly string[] ExpectedDriverIds =
    [
        "light.cst.serial",
        "light.opt.serial",
    ];

    /// <summary>
    /// 工厂应向配置界面公开 CST 和 OPT 两种驱动。
    /// </summary>
    [Fact]
    public void AvailableDrivers_ContainsAllSupportedBrands()
    {
        var factory = new LightControllerFactory();

        Assert.Equal(ExpectedDriverIds, factory.AvailableDrivers.Select(x => x.Id.Value));
        Assert.All(factory.AvailableDrivers, descriptor =>
            Assert.Equal(DeviceCategory.LightController, descriptor.Category));
    }

    /// <summary>
    /// 两个品牌都应由同一个工厂创建，创建过程不应连接硬件。
    /// </summary>
    /// <param name="driverId">待验证的稳定驱动标识。</param>
    [Theory]
    [InlineData("light.cst.serial")]
    [InlineData("light.opt.serial")]
    public async Task Create_KnownDriver_RoutesThroughUnifiedFactory(string driverId)
    {
        var options = ValidOptions(driverId);
        var factory = new LightControllerFactory();

        await using var controller = factory.Create(options);

        Assert.Equal(options, controller.Options);
        Assert.Equal(DeviceState.Disconnected, controller.State);
    }

    /// <summary>
    /// 未连接时设置亮度应返回稳定失败结果，不能调用原生 DLL。
    /// </summary>
    [Fact]
    public async Task SetIntensity_WhenDisconnected_ReturnsNotConnected()
    {
        var controller = new LightControllerFactory().Create(ValidOptions("light.cst.serial"));
        await using (controller)
        {
            var result = await controller.SetIntensityAsync(1, 100);

            Assert.False(result.Success);
            Assert.Equal("light.cst.not_connected", result.ErrorCode);
        }
    }

    /// <summary>
    /// 未安装的驱动应报告全部可用光源驱动。
    /// </summary>
    [Fact]
    public void Create_UnknownDriver_ReportsAllInstalledDrivers()
    {
        var factory = new LightControllerFactory();

        var exception = Assert.Throws<LightControllerDriverNotFoundException>(() =>
            factory.Create(ValidOptions("light.unknown.serial")));

        foreach (var driverId in ExpectedDriverIds)
            Assert.Contains(driverId, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 批量设置过程中厂商函数抛出异常时，应返回稳定失败结果并进入故障状态。
    /// </summary>
    [Fact]
    public async Task SetAllIntensity_WhenVendorCallThrows_ReturnsFailure()
    {
        var controller = new ThrowingLightController(ValidOptions("light.test.serial"));
        await using (controller)
        {
            Assert.True((await controller.ConnectAsync()).Success);

            var result = await controller.SetAllIntensityAsync(100);

            Assert.False(result.Success);
            Assert.Equal("light.test.set_intensity_failed", result.ErrorCode);
            Assert.Contains("通道 2", result.Message, StringComparison.Ordinal);
            Assert.Equal(DeviceState.Faulted, controller.State);
        }
    }

    /// <summary>
    /// 验证 ValidOptions 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// </summary>
    private static LightControllerOptions ValidOptions(string driverId) => new()
    {
        Name = "Light01",
        DriverId = new DeviceDriverId(driverId),
        PortName = "COM3",
        ChannelCount = 4,
        MaximumIntensity = 255,
    };

    /// <summary>模拟第二个通道调用厂商 DLL 时抛出异常的光源控制器。</summary>
    private sealed class ThrowingLightController(LightControllerOptions options)
        : SerialLightControllerBase(options)
    {
        protected override string ErrorCodePrefix => "light.test";

        protected override int SuccessCode => 0;

        /// <summary>
        /// 验证 ConnectCore 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        protected override int ConnectCore() => 0;

        /// <summary>
        /// 验证 DisconnectCore 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        protected override int DisconnectCore() => 0;

        /// <summary>
        /// 验证 SetIntensityCore 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        protected override int SetIntensityCore(int channel, int intensity) =>
            channel == 2
                ? throw new InvalidOperationException("模拟厂商调用失败")
                : 0;
    }
}
