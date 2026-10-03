// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ScannerFactoryTests、FakeByteStreamTransport。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.IO.Pipelines、System.Threading.Channels、Communication.Runtime.Transports、Device.Core.Devices、Device.Core.Scanners、Device.Runtime.Scanners。导入不代表所有类型都会在运行时加载。
using System.IO.Pipelines;
using System.Threading.Channels;
using Communication.Runtime.Transports;
using Device.Core.Devices;
using Device.Core.Scanners;
using Device.Runtime.Scanners;
using Device.Runtime.Scanners.Newland;
using Xunit;

namespace Device.Tests.Runtime;

/// <summary>
/// 验证统一扫码器工厂和新大陆串口分帧实现。
/// </summary>
public sealed class ScannerFactoryTests
{
    private const string DriverId = "scanner.newland.nvh220.serial";

    /// <summary>工厂应公开新大陆 NVH220 驱动。</summary>
    [Fact]
    public void AvailableDrivers_ContainsNewlandNvh220()
    {
        var factory = new ScannerFactory();

        var descriptor = Assert.Single(factory.AvailableDrivers);
        Assert.Equal(DriverId, descriptor.Id.Value);
        Assert.Equal(DeviceCategory.Scanner, descriptor.Category);
        Assert.Equal("Newland", descriptor.Brand);
    }

    /// <summary>已知驱动应创建尚未连接的扫码器实例。</summary>
    [Fact]
    public async Task Create_KnownDriver_RoutesThroughUnifiedFactory()
    {
        var options = ValidOptions();

        await using var scanner = new ScannerFactory().Create(options);

        Assert.Equal(options, scanner.Options);
        Assert.Equal(DeviceState.Disconnected, scanner.State);
    }

    /// <summary>未知驱动异常应包含当前可用驱动。</summary>
    [Fact]
    public void Create_UnknownDriver_ReportsInstalledDriver()
    {
        var factory = new ScannerFactory();
        var options = ValidOptions() with
        {
            DriverId = new DeviceDriverId("scanner.unknown.serial"),
        };

        var exception = Assert.Throws<ScannerDriverNotFoundException>(() => factory.Create(options));

        Assert.Contains(DriverId, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 结束符跨读取块时应正确拼接，并能从同一读取块发布多条扫码结果。
    /// </summary>
    [Fact]
    public async Task ReceiveLoop_FramesAcrossChunks_AndIsolatesSubscriberFailures()
    {
        var transport = new FakeByteStreamTransport();
        var scanner = new NewlandNvh220Scanner(ValidOptions(), transport);
        await using (scanner)
        {
            var received = Channel.CreateUnbounded<string>();
            scanner.ScanReceived += (_, _) => throw new InvalidOperationException("模拟业务订阅者异常");
            scanner.ScanReceived += (_, args) => received.Writer.TryWrite(args.Value);

            Assert.True((await scanner.ConnectAsync()).Success);
            await transport.SendToScannerAsync("ABC\r");
            await transport.SendToScannerAsync("\nXYZ\r\n");

            var first = await received.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
            var second = await received.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));

            Assert.Equal("ABC", first);
            Assert.Equal("XYZ", second);
            Assert.True((await scanner.DisconnectAsync()).Success);
        }
    }

    /// <summary>
    /// 即使超长帧和结束符位于同一个读取块中，也必须在发布事件前拒绝该帧。
    /// </summary>
    [Fact]
    public async Task ReceiveLoop_CompletedOversizedFrame_FaultsWithoutPublishing()
    {
        var transport = new FakeByteStreamTransport();
        var scanner = new NewlandNvh220Scanner(
            ValidOptions() with { MaximumFrameBytes = 4 },
            transport);
        await using (scanner)
        {
            var published = false;
            scanner.ScanReceived += (_, _) => published = true;

            Assert.True((await scanner.ConnectAsync()).Success);
            await transport.SendToScannerAsync("ABCDE\r\n");
            await WaitUntilAsync(() => scanner.State == DeviceState.Faulted);

            Assert.False(published);
        }
    }

    /// <summary>等待后台接收循环进入预期状态，超时则让测试明确失败。</summary>
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (!condition())
            await Task.Delay(10, timeout.Token);
    }

    /// <summary>
    /// 验证 ValidOptions 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// </summary>
    private static ScannerOptions ValidOptions() => new()
    {
        Name = "Scanner01",
        DriverId = new DeviceDriverId(DriverId),
        PortName = "COM3",
    };

    /// <summary>
    /// FakeByteStreamTransport 的类型定义。Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：IByteStreamTransport。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class FakeByteStreamTransport : IByteStreamTransport
    {
        private readonly Pipe _input = new();
        private readonly Pipe _output = new();

        public PipeReader Input => _input.Reader;
        public PipeWriter Output => _output.Writer;
        public bool IsOpen { get; private set; }

        /// <summary>
        /// 验证 OpenAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 ThrowIfCancellationRequested；具体条件和先后次序见方法体。
        /// </summary>
        public ValueTask OpenAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IsOpen = true;
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// 验证 CloseAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 ThrowIfCancellationRequested、CancelPendingRead；具体条件和先后次序见方法体。
        /// </summary>
        public ValueTask CloseAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IsOpen = false;
            _input.Reader.CancelPendingRead();
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// 验证 SendToScannerAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 WriteAsync、GetBytes；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        public async ValueTask SendToScannerAsync(string value)
        {
            await _input.Writer.WriteAsync(System.Text.Encoding.UTF8.GetBytes(value));
        }

        /// <summary>
        /// 验证 DisposeAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 CompleteAsync；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            await _input.Writer.CompleteAsync();
            await _input.Reader.CompleteAsync();
            await _output.Writer.CompleteAsync();
            await _output.Reader.CompleteAsync();
        }
    }
}
