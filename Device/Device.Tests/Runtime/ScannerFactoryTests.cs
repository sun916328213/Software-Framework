using System.IO.Pipelines;
using System.Threading.Channels;
using CommunicationNew.Transports;
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

    private static ScannerOptions ValidOptions() => new()
    {
        Name = "Scanner01",
        DriverId = new DeviceDriverId(DriverId),
        PortName = "COM3",
    };

    private sealed class FakeByteStreamTransport : IByteStreamTransport
    {
        private readonly Pipe _input = new();
        private readonly Pipe _output = new();

        public PipeReader Input => _input.Reader;
        public PipeWriter Output => _output.Writer;
        public bool IsOpen { get; private set; }

        public ValueTask OpenAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IsOpen = true;
            return ValueTask.CompletedTask;
        }

        public ValueTask CloseAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IsOpen = false;
            _input.Reader.CancelPendingRead();
            return ValueTask.CompletedTask;
        }

        public async ValueTask SendToScannerAsync(string value)
        {
            await _input.Writer.WriteAsync(System.Text.Encoding.UTF8.GetBytes(value));
        }

        public async ValueTask DisposeAsync()
        {
            await _input.Writer.CompleteAsync();
            await _input.Reader.CompleteAsync();
            await _output.Writer.CompleteAsync();
            await _output.Reader.CompleteAsync();
        }
    }
}
