// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：FrameGrabberFactoryTests、FakeFrameGrabber。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Core.Devices、Device.Core.FrameGrabbers、Device.Runtime.FrameGrabbers、Xunit。导入不代表所有类型都会在运行时加载。
using Device.Core.Devices;
using Device.Core.FrameGrabbers;
using Device.Runtime.FrameGrabbers;
using Xunit;

namespace Device.Tests.Runtime;

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

    /// <summary>
    /// 测试相关入口 ValidOptions；准备条件、协作调用与结果检查以方法正文为准。
    /// </summary>
    private static FrameGrabberOptions ValidOptions(DeviceDriverId driverId) => new()
    {
        Name = "Grabber01",
        DriverId = driverId,
        ConfigurationFilePath = "Camera.dcf",
    };

    /// <summary>
    /// FakeFrameGrabber 的类型定义。Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：FrameGrabberBase。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class FakeFrameGrabber(FrameGrabberOptions options) : FrameGrabberBase(options)
    {
        public int ConnectCount { get; private set; }

        public int DisconnectCount { get; private set; }

        protected override string ErrorCodePrefix => "frame-grabber.fake";

        /// <summary>
        /// 测试相关入口 ConnectCore；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        protected override void ConnectCore() => ConnectCount++;

        /// <summary>
        /// 测试相关入口 DisconnectCore；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        protected override void DisconnectCore() => DisconnectCount++;

        /// <summary>
        /// 测试相关入口 EmitFrame；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 PublishFrame；具体条件和先后次序见方法体。
        /// </summary>
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
