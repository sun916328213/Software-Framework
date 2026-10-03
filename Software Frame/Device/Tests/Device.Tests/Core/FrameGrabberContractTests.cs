// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：FrameGrabberContractTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Core.Devices、Device.Core.FrameGrabbers、Xunit。导入不代表所有类型都会在运行时加载。
using Device.Core.Devices;
using Device.Core.FrameGrabbers;
using Xunit;

namespace Device.Tests.Core;

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
