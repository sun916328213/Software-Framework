using Device.Core.Devices;
using Device.Core.FrameGrabbers;
using Device.Drivers.FrameGrabbers;
using Xunit;

namespace Device.Drivers.Tests;

/// <summary>
/// 验证统一采集卡工厂和无需真实硬件即可检查的约束。
/// </summary>
public sealed class FrameGrabberFactoryTests
{
    private const string EuresysDriverId = "frame-grabber.euresys.multicam";
    private const string MatroxDriverId = "frame-grabber.matrox.radient-evcl";

    /// <summary>同一个工厂应公开两个已实现的采集卡驱动。</summary>
    [Fact]
    public void AvailableDrivers_ContainsAllImplementedDrivers()
    {
        var factory = new FrameGrabberFactory();

        Assert.Equal(
            [EuresysDriverId, MatroxDriverId],
            factory.AvailableDrivers.Select(x => x.Id.Value));
        Assert.All(factory.AvailableDrivers, descriptor =>
            Assert.Equal(DeviceCategory.FrameGrabber, descriptor.Category));
    }

    /// <summary>创建任一品牌时都不应立即加载厂商驱动或访问硬件。</summary>
    /// <param name="driverId">强类型属性对应的稳定持久化值。</param>
    [Theory]
    [InlineData("frame-grabber.euresys.multicam")]
    [InlineData("frame-grabber.matrox.radient-evcl")]
    public async Task Create_KnownDriver_ReturnsDisconnectedFrameGrabber(string driverId)
    {
        var options = ValidOptions(new DeviceDriverId(driverId));

        await using var frameGrabber = new FrameGrabberFactory().Create(options);

        Assert.Equal(options, frameGrabber.Options);
        Assert.Equal(DeviceState.Disconnected, frameGrabber.State);
    }

    /// <summary>Matrox 适配器应明确拒绝旧实现从未正确处理的非 Mono8 数据。</summary>
    [Fact]
    public void Create_MatroxNonMono8_ThrowsConfigurationException()
    {
        var options = ValidOptions(new DeviceDriverId(MatroxDriverId)) with
        {
            PixelFormat = "Mono16",
        };

        var exception = Assert.Throws<FrameGrabberConfigurationException>(() =>
            new FrameGrabberFactory().Create(options));

        Assert.Single(exception.Errors);
    }

    /// <summary>手工修改配置造成的未知 ID 应被拒绝并列出全部可选值。</summary>
    [Fact]
    public void Create_UnknownDriver_ReportsAllInstalledDrivers()
    {
        var factory = new FrameGrabberFactory();
        var options = ValidOptions(new DeviceDriverId("frame-grabber.unknown"));

        var exception = Assert.Throws<FrameGrabberDriverNotFoundException>(() => factory.Create(options));

        Assert.Contains(EuresysDriverId, exception.Message, StringComparison.Ordinal);
        Assert.Contains(MatroxDriverId, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>共享基类应管理幂等连接和断开，不重复分配厂商资源。</summary>
    [Fact]
    public async Task Lifecycle_RepeatedCalls_InvokeDriverOnce()
    {
        await using var frameGrabber = new FakeFrameGrabber(
            ValidOptions(new DeviceDriverId(EuresysDriverId)));

        Assert.True((await frameGrabber.ConnectAsync()).Success);
        Assert.True((await frameGrabber.ConnectAsync()).Success);
        Assert.True((await frameGrabber.DisconnectAsync()).Success);
        Assert.True((await frameGrabber.DisconnectAsync()).Success);

        Assert.Equal(1, frameGrabber.ConnectCount);
        Assert.Equal(1, frameGrabber.DisconnectCount);
    }

    /// <summary>一个订阅者异常不能阻止后续订阅者收到采集帧。</summary>
    [Fact]
    public async Task FrameReceived_WhenSubscriberThrows_ContinuesPublishing()
    {
        await using var frameGrabber = new FakeFrameGrabber(
            ValidOptions(new DeviceDriverId(EuresysDriverId)));
        var receivedCount = 0;
        frameGrabber.FrameReceived += (_, _) => throw new InvalidOperationException("subscriber failure");
        frameGrabber.FrameReceived += (_, _) => receivedCount++;

        frameGrabber.EmitFrame();

        Assert.Equal(1, receivedCount);
    }

    private static FrameGrabberOptions ValidOptions(DeviceDriverId driverId) => new()
    {
        Name = "Grabber01",
        DriverId = driverId,
        ConfigurationFilePath = "Camera.dcf",
    };

    private sealed class FakeFrameGrabber(FrameGrabberOptions options) : FrameGrabberBase(options)
    {
        public int ConnectCount { get; private set; }

        public int DisconnectCount { get; private set; }

        protected override string ErrorCodePrefix => "frame-grabber.fake";

        protected override void ConnectCore() => ConnectCount++;

        protected override void DisconnectCore() => DisconnectCount++;

        public void EmitFrame() => PublishFrame(new FrameGrabberFrame(
            2,
            2,
            2,
            "Mono8",
            new byte[4],
            1,
            DateTimeOffset.UtcNow));
    }
}
