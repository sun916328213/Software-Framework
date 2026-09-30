using System.Net;
using System.Net.Sockets;
using Device.Core.Devices;
using Device.Core.Printers;
using Device.Runtime.Printers;
using Device.Runtime.Printers.Sato;
using Xunit;

namespace Device.Tests.Runtime;

/// <summary>
/// 验证统一打印机工厂和无需真实硬件即可验证的行为。
/// </summary>
public sealed class PrinterFactoryTests
{
    private const string SatoDriverId = "printer.sato.sbpl.tcp";
    private const string ZebraDriverId = "printer.zebra.zpl.tcp";

    /// <summary>工厂应公开当前真正实现的 SATO 和 Zebra 驱动。</summary>
    [Fact]
    public void AvailableDrivers_ContainsImplementedDriversOnly()
    {
        var factory = new PrinterFactory();

        Assert.Equal(
            [SatoDriverId, ZebraDriverId],
            factory.AvailableDrivers.Select(x => x.Id.Value));
        Assert.All(factory.AvailableDrivers, descriptor =>
            Assert.Equal(DeviceCategory.Printer, descriptor.Category));
    }

    /// <summary>创建任一品牌打印机都不应自动连接网络或访问真实硬件。</summary>
    /// <param name="driverId">需要创建的稳定驱动标识。</param>
    [Theory]
    [InlineData(SatoDriverId)]
    [InlineData(ZebraDriverId)]
    public async Task Create_KnownDriver_ReturnsDisconnectedPrinter(string driverId)
    {
        var options = ValidOptions(driverId);

        await using var printer = new PrinterFactory().Create(options);

        Assert.Equal(options, printer.Options);
        Assert.Equal(DeviceState.Disconnected, printer.State);
    }

    /// <summary>释放后的打印机应稳定拒绝所有新操作，并允许重复释放。</summary>
    /// <param name="driverId">需要验证的打印机驱动标识。</param>
    [Theory]
    [InlineData(SatoDriverId)]
    [InlineData(ZebraDriverId)]
    public async Task Dispose_ThenOperate_ThrowsObjectDisposedException(string driverId)
    {
        var printer = new PrinterFactory().Create(ValidOptions(driverId));

        await printer.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await printer.ConnectAsync());
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await printer.DisconnectAsync());
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await printer.GetStatusAsync());
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await printer.PrintRawAsync(ReadOnlyMemory<byte>.Empty));

        await printer.DisposeAsync();
    }

    /// <summary>未连接时查询状态应返回对应品牌的稳定错误码。</summary>
    /// <param name="driverId">需要创建的稳定驱动标识。</param>
    /// <param name="errorCode">预期错误码。</param>
    [Theory]
    [InlineData(SatoDriverId, "printer.sato.not_connected")]
    [InlineData(ZebraDriverId, "printer.zebra.not_connected")]
    public async Task GetStatus_WhenDisconnected_ReturnsNotConnected(string driverId, string errorCode)
    {
        await using var printer = new PrinterFactory().Create(ValidOptions(driverId));

        var result = await printer.GetStatusAsync();

        Assert.False(result.Success);
        Assert.Equal(errorCode, result.ErrorCode);
    }

    /// <summary>未连接时发送非空数据应返回稳定错误码。</summary>
    [Fact]
    public async Task PrintRaw_WhenDisconnected_ReturnsNotConnected()
    {
        await using var printer = new PrinterFactory().Create(ValidOptions());

        var result = await printer.PrintRawAsync("^XA^XZ"u8.ToArray());

        Assert.False(result.Success);
        Assert.Equal("printer.zebra.not_connected", result.ErrorCode);
    }

    /// <summary>空打印任务应在访问连接前直接拒绝。</summary>
    [Fact]
    public async Task PrintRaw_WhenDataIsEmpty_ReturnsValidationFailure()
    {
        await using var printer = new PrinterFactory().Create(ValidOptions());

        var result = await printer.PrintRawAsync(ReadOnlyMemory<byte>.Empty);

        Assert.False(result.Success);
        Assert.Equal("printer.zebra.print_data_empty", result.ErrorCode);
    }

    /// <summary>未知驱动异常应列出当前真正安装的驱动。</summary>
    [Fact]
    public void Create_UnknownDriver_ReportsInstalledDrivers()
    {
        var factory = new PrinterFactory();
        var options = ValidOptions() with
        {
            DriverId = new DeviceDriverId("printer.unknown.tcp"),
        };

        var exception = Assert.Throws<PrinterDriverNotFoundException>(() => factory.Create(options));

        Assert.Contains(ZebraDriverId, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>状态摘要应组合全部同时存在的状态，而不是丢失次要故障。</summary>
    [Fact]
    public void PrinterStatus_Summary_ContainsEveryActiveState()
    {
        var status = new PrinterStatus(
            ReadyToPrint: false,
            Paused: true,
            HeadOpen: true,
            PaperOut: false,
            RibbonOut: true,
            ReceiveBufferFull: false,
            MediaNearEnd: true,
            AdditionalFaultCode: PrinterFaultCodes.CutterError,
            HeadTooHot: true,
            LabelsRemainingInBatch: 12);

        Assert.True(status.HasFault);
        Assert.Equal(12, status.LabelsRemainingInBatch);
        Assert.Equal(
            "Paused/HeadOpen/RibbonOut/MediaNearEnd/cutter_error/HeadTooHot",
            status.Summary);
    }

    /// <summary>SATO Status4 状态码应映射为公共打印机状态。</summary>
    [Theory]
    [InlineData('A', true, false, false, false, false, false)]
    [InlineData('0', false, true, false, false, false, false)]
    [InlineData('b', false, false, true, false, false, false)]
    [InlineData('c', false, false, false, true, false, false)]
    [InlineData('d', false, false, false, false, true, false)]
    [InlineData('a', false, false, false, false, false, true)]
    public void SatoStatusResponse_MapsStatusCode(
        char statusCode,
        bool ready,
        bool paused,
        bool headOpen,
        bool paperOut,
        bool ribbonOut,
        bool receiveBufferFull)
    {
        var status = SatoNetworkPrinter.ParseStatus(CreateSatoStatusResponse(statusCode));

        Assert.Equal(ready, status.ReadyToPrint);
        Assert.Equal(paused, status.Paused);
        Assert.Equal(headOpen, status.HeadOpen);
        Assert.Equal(paperOut, status.PaperOut);
        Assert.Equal(ribbonOut, status.RibbonOut);
        Assert.Equal(receiveBufferFull, status.ReceiveBufferFull);
    }

    /// <summary>SATO 耗材预警、缓冲区预警和其他硬件故障不应丢失。</summary>
    [Theory]
    [InlineData('B', true, false, null)]
    [InlineData('C', false, true, null)]
    [InlineData('D', true, true, null)]
    [InlineData('e', false, false, PrinterFaultCodes.MediaError)]
    [InlineData('j', false, false, PrinterFaultCodes.CutterError)]
    [InlineData('z', false, false, PrinterFaultCodes.UnknownError)]
    public void SatoStatusResponse_MapsWarningsAndAdditionalFaults(
        char statusCode,
        bool mediaNearEnd,
        bool receiveBufferNearFull,
        string? additionalFaultCode)
    {
        var status = SatoNetworkPrinter.ParseStatus(CreateSatoStatusResponse(statusCode));

        Assert.Equal(mediaNearEnd, status.MediaNearEnd);
        Assert.Equal(receiveBufferNearFull, status.ReceiveBufferNearFull);
        Assert.Equal(additionalFaultCode, status.AdditionalFaultCode);
        Assert.Equal(additionalFaultCode is not null, status.HasFault);
    }

    /// <summary>缺少完整帧边界的 SATO 状态响应必须被明确拒绝。</summary>
    [Theory]
    [InlineData(new byte[] { 0x31, 0x30, 0x03 })]
    [InlineData(new byte[] { 0x02, 0x31, 0x30 })]
    [InlineData(new byte[] { 0x02, 0x31, 0x30, 0x03 })]
    public void SatoStatusResponse_WhenMalformed_ThrowsFormatException(byte[] response)
    {
        Assert.Throws<FormatException>(() => SatoNetworkPrinter.ParseStatus(response));
    }

    /// <summary>Status4 剩余标签数量损坏时必须拒绝响应，不能误报为零。</summary>
    [Fact]
    public void SatoStatusResponse_WhenRemainingCountIsNotNumeric_ThrowsFormatException()
    {
        var response = CreateSatoStatusResponse('A');
        response[9] = (byte)'X';

        Assert.Throws<FormatException>(() => SatoNetworkPrinter.ParseStatus(response));
    }

    /// <summary>兼容模式额外增加的长度前缀不应影响 Status4 负载解析。</summary>
    [Fact]
    public void SatoStatusResponse_WithLegacyPrefix_ParsesStatusPayload()
    {
        var response = new byte[] { 0x00, 0x00, 0x00, 0x20 }
            .Concat(CreateSatoStatusResponse('d', 123))
            .ToArray();

        var status = SatoNetworkPrinter.ParseStatus(response);

        Assert.True(status.RibbonOut);
        Assert.Equal(123, status.LabelsRemainingInBatch);
    }

    /// <summary>
    /// SATO 适配器应通过同一 TCP 连接发送 ENQ、解析状态并原样发送 SBPL 数据。
    /// </summary>
    [Fact]
    public async Task SatoTcp_StatusAndPrint_CompletesProtocolRoundTrip()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var options = ValidOptions(SatoDriverId) with
            {
                IpAddress = IPAddress.Loopback.ToString(),
                Port = port,
                MaximumReadTimeoutMilliseconds = 2_000,
            };
            await using var printer = new PrinterFactory().Create(options);

            var acceptTask = listener.AcceptTcpClientAsync();
            Assert.True((await printer.ConnectAsync()).Success);
            using var server = await acceptTask.WaitAsync(TimeSpan.FromSeconds(2));

            var printData = "\u001bA\u001bQ1\u001bZ"u8.ToArray();
            var exchangeTask = ExchangeSatoProtocolAsync(server.GetStream(), printData);

            var status = await printer.GetStatusAsync();
            var print = await printer.PrintRawAsync(printData);
            await exchangeTask.WaitAsync(TimeSpan.FromSeconds(2));

            Assert.True(status.Success);
            Assert.True(status.Value!.PaperOut);
            Assert.True(print.Success);
            Assert.True((await printer.DisconnectAsync()).Success);
        }
        finally
        {
            listener.Stop();
        }
    }

    /// <summary>对端在状态响应前断开时，查询应失败并把连接标记为故障。</summary>
    [Fact]
    public async Task SatoTcp_StatusPeerDisconnects_MarksPrinterFaulted()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var options = ValidOptions(SatoDriverId) with
            {
                IpAddress = IPAddress.Loopback.ToString(),
                Port = port,
                MaximumReadTimeoutMilliseconds = 2_000,
            };
            await using var printer = new PrinterFactory().Create(options);

            var acceptTask = listener.AcceptTcpClientAsync();
            Assert.True((await printer.ConnectAsync()).Success);
            using var server = await acceptTask.WaitAsync(TimeSpan.FromSeconds(2));
            server.Dispose();

            var status = await printer.GetStatusAsync();

            Assert.False(status.Success);
            Assert.Equal("printer.sato.status_failed", status.ErrorCode);
            Assert.Equal(DeviceState.Faulted, printer.State);
        }
        finally
        {
            listener.Stop();
        }
    }

    /// <summary>打印机收到 ENQ 但不响应时，应返回稳定超时错误并禁止继续复用连接。</summary>
    [Fact]
    public async Task SatoTcp_StatusDoesNotRespond_MarksPrinterFaulted()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var options = ValidOptions(SatoDriverId) with
            {
                IpAddress = IPAddress.Loopback.ToString(),
                Port = port,
                MaximumReadTimeoutMilliseconds = 200,
                TimeToWaitForMoreDataMilliseconds = 100,
            };
            await using var printer = new PrinterFactory().Create(options);

            var acceptTask = listener.AcceptTcpClientAsync();
            Assert.True((await printer.ConnectAsync()).Success);
            using var server = await acceptTask.WaitAsync(TimeSpan.FromSeconds(2));
            var requestReceived = ReadSatoStatusRequestAsync(server.GetStream());

            var status = await printer.GetStatusAsync();
            await requestReceived.WaitAsync(TimeSpan.FromSeconds(2));

            Assert.False(status.Success);
            Assert.Equal("printer.sato.status_timeout", status.ErrorCode);
            Assert.Equal(DeviceState.Faulted, printer.State);
        }
        finally
        {
            listener.Stop();
        }
    }

    /// <summary>模拟打印机完成一次 ENQ 响应和一次 SBPL 数据接收。</summary>
    private static async Task ExchangeSatoProtocolAsync(NetworkStream stream, byte[] expectedPrintData)
    {
        await ReadSatoStatusRequestAsync(stream);

        await stream.WriteAsync(CreateSatoStatusResponse('c'));
        await stream.FlushAsync();

        var printData = new byte[expectedPrintData.Length];
        await stream.ReadExactlyAsync(printData);
        Assert.Equal(expectedPrintData, printData);
    }

    /// <summary>读取并验证单字节 SATO Status4 ENQ 请求。</summary>
    private static async Task ReadSatoStatusRequestAsync(NetworkStream stream)
    {
        var statusRequest = new byte[1];
        await stream.ReadExactlyAsync(statusRequest);
        Assert.Equal(new byte[] { 0x05 }, statusRequest);
    }

    /// <summary>构造带标准长度前缀的官方 Status4 ENQ 返回帧。</summary>
    private static byte[] CreateSatoStatusResponse(char statusCode, int labelsRemaining = 0) =>
    [
        0x00, 0x00, 0x00, 0x1C,
        0x05,
        0x02,
        .. "00"u8.ToArray(),
        (byte)statusCode,
        .. System.Text.Encoding.ASCII.GetBytes(labelsRemaining.ToString("D6")),
        .. "                "u8.ToArray(),
        0x03,
    ];

    private static PrinterOptions ValidOptions(string driverId = ZebraDriverId) => new()
    {
        Name = "Printer01",
        DriverId = new DeviceDriverId(driverId),
        IpAddress = "192.168.1.100",
    };
}
