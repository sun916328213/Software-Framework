using Device.Core.Devices;
using Device.Core.FrameGrabbers;
using Xunit;

namespace Device.Core.Tests;

/// <summary>
/// 验证采集卡公共配置约束。
/// </summary>
public sealed class FrameGrabberContractTests
{
    /// <summary>无效配置应一次返回全部问题。</summary>
    [Fact]
    public void Validate_InvalidConfiguration_ReturnsAllErrors()
    {
        var options = new FrameGrabberOptions
        {
            Name = "",
            DriverId = default,
            ConfigurationFilePath = "",
            BoardIndex = -1,
            Connector = "",
            PixelFormat = "",
            BufferCount = 0,
        };

        var exception = Assert.Throws<FrameGrabberConfigurationException>(() =>
            FrameGrabberOptionsValidator.Validate(options));

        Assert.Equal(7, exception.Errors.Count);
    }

    /// <summary>相对配置文件路径允许在部署目录中解析。</summary>
    [Fact]
    public void Validate_RelativeConfigurationPath_IsAccepted()
    {
        FrameGrabberOptionsValidator.Validate(new FrameGrabberOptions
        {
            Name = "Grabber01",
            DriverId = new DeviceDriverId("frame-grabber.test"),
            ConfigurationFilePath = "Camera.cam",
        });
    }
}
