// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：LightControllerContractTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Core.Devices、Device.Core.Lights、Xunit。导入不代表所有类型都会在运行时加载。
using Device.Core.Devices;
using Device.Core.Lights;
using Xunit;

namespace Device.Tests.Core;

/// <summary>
/// 验证光源控制器的公共配置约束。
/// </summary>
public sealed class LightControllerContractTests
{
    /// <summary>
    /// 无效配置应在访问厂商驱动前一次返回全部问题。
    /// </summary>
    [Fact]
    public void Validate_InvalidConfiguration_ReturnsAllErrors()
    {
        var options = new LightControllerOptions
        {
            Name = "",
            DriverId = default,
            PortName = "USB1",
            ChannelCount = 0,
            MaximumIntensity = 256,
        };

        var exception = Assert.Throws<LightControllerConfigurationException>(() =>
            LightControllerOptionsValidator.Validate(options));

        Assert.Equal(5, exception.Errors.Count);
    }

    /// <summary>
    /// 串口格式应忽略大小写，并接受正整数端口号。
    /// </summary>
    [Fact]
    public void Validate_LowerCaseComPort_IsAccepted()
    {
        var options = new LightControllerOptions
        {
            Name = "Light01",
            DriverId = new DeviceDriverId("light.fake.serial"),
            PortName = "com3",
        };

        LightControllerOptionsValidator.Validate(options);
    }
}
