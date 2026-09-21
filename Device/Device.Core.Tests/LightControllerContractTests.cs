using Device.Core.Devices;
using Device.Core.Lights;
using Xunit;

namespace Device.Core.Tests;

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
