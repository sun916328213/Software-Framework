using System.Buffers;
using System.IO.Pipelines;
using CommunicationNew.Abstractions;
using CommunicationNew.Protocols.Mitsubishi;
using CommunicationNew.Transports;
using Xunit;

namespace CommunicationNew.Tests.Compatibility;

public sealed class McClientCompatibilityTests
{
    [Fact]
    public void Mc_communication_timeout_follows_the_common_timeout_when_not_overridden()
    {
        var parameter = new McParam { TimeoutMs = 1750 };

        Assert.Equal(1750, parameter.CommunicationTimeout);
    }

    [Fact]
    public void Explicit_mc_communication_timeout_preserves_the_legacy_override()
    {
        var parameter = new McParam
        {
            TimeoutMs = 1750,
            CommunicationTimeout = 4200,
        };

        parameter.TimeoutMs = 900;

        Assert.Equal(4200, parameter.CommunicationTimeout);
    }

    [Fact]
    public void Mx_component_class_not_registered_error_explains_the_required_runtime()
    {
        var error = new MxComponentException(
            unchecked((int)0x80040154),
            "Open");

        Assert.Equal(unchecked((int)0x80040154), error.ErrorCode);
        Assert.Contains("未注册", error.Message);
        Assert.Contains("x64", error.Message);
    }

    [Fact]
    public void Mx_component_error_can_include_the_vendor_message()
    {
        var error = new MxComponentException(
            0x1234,
            "ReadDeviceBlock2",
            "逻辑站设置无效");

        Assert.Equal(0x1234, error.ErrorCode);
        Assert.Contains("逻辑站设置无效", error.Message);
    }

    [Fact]
    public async Task Mx_component_backend_can_be_created_in_direct_mode()
    {
        await using var device = new McClientDevice(new McParam
        {
            Backend = McClientBackend.MxComponent,
        });

        Assert.Equal(ConnectionState.Disconnected, device.State);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1024)]
    public void Mx_component_rejects_an_invalid_logical_station_before_connect(int station)
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new McClientDevice(new McParam
            {
                Backend = McClientBackend.MxComponent,
                MxLogicalStationNumber = station,
            }));

        Assert.Contains(nameof(McParam.MxLogicalStationNumber), error.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Mx_backend_rejects_a_non_positive_communication_timeout(int milliseconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new McClientDevice(new McParam
            {
                Backend = McClientBackend.MxComponent,
                CommunicationTimeout = milliseconds,
            }));
    }

    [Fact]
    public async Task Direct_mx_adapter_shares_concurrent_disposal_and_cannot_be_reused()
    {
        var adapter = new MxComponentMcClientAdapter(0, password: null);

        await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => adapter.DisposeAsync().AsTask()));

        Assert.Equal(ConnectionState.Disconnected, adapter.State);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => adapter.ConnectAsync().AsTask());
    }

    [Fact]
    public async Task Mc_client_can_read_through_a_non_socket_adapter()
    {
        var adapter = new ScriptedMcClientAdapter(
            new McResponse(new byte[] { 0x34, 0x12, 0x78, 0x56 }),
            request =>
            {
                Assert.False(request.IsWrite);
                Assert.Equal(McAddressValue.Parse("D100"), request.Address);
                Assert.Equal((ushort)2, request.Quantity);
            });
        await using var device = new McClientDevice(
            new McParam { RetryCount = 1 },
            adapter);

        Assert.True(await device.ConnectAsync());

        Assert.Equal(
            new byte[] { 0x12, 0x34, 0x56, 0x78 },
            await device.ReadAsync("D100", 2));
        Assert.Equal(ConnectionState.Connected, ((IConnection)device).State);
    }

    [Fact]
    public async Task Mx_component_connection_event_identifies_the_logical_station()
    {
        var adapter = new ScriptedMcClientAdapter(
            new McResponse(ReadOnlyMemory<byte>.Empty),
            _ => { });
        await using var device = new McClientDevice(
            new McParam
            {
                Backend = McClientBackend.MxComponent,
                MxLogicalStationNumber = 7,
            },
            adapter);
        string? message = null;
        device.ConnectionStateChanged += (_, args) => message = args.Message;

        Assert.True(await device.ConnectAsync());

        Assert.Equal("MC协议客户端已连接: MX Component 逻辑站 7 (直接模式)", message);
    }

    [Fact]
    public async Task Mc_client_preserves_word_write_conversion_for_backend_adapters()
    {
        McRequest? executedRequest = null;
        var adapter = new ScriptedMcClientAdapter(
            new McResponse(ReadOnlyMemory<byte>.Empty),
            request => executedRequest = request);
        await using var device = new McClientDevice(
            new McParam { RetryCount = 1 },
            adapter);

        await device.ConnectAsync();

        Assert.True(await device.WriteAsync("D100", new byte[] { 0x12, 0x34 }));
        Assert.NotNull(executedRequest);
        Assert.True(executedRequest.IsWrite);
        Assert.Equal((ushort)1, executedRequest.Quantity);
        Assert.Equal(new byte[] { 0x34, 0x12 }, executedRequest.Payload.ToArray());
    }

    [Theory]
    [InlineData("D100", 1922)]
    [InlineData("M100", 7169)]
    public async Task Mc_client_rejects_an_oversized_write_before_executing_the_adapter(
        string address,
        int byteCount)
    {
        var executed = false;
        var adapter = new ScriptedMcClientAdapter(
            new McResponse(ReadOnlyMemory<byte>.Empty),
            _ => executed = true);
        await using var device = new McClientDevice(new McParam(), adapter);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            device.WriteAsync(address, new byte[byteCount]));

        Assert.False(executed);
    }

    [Theory]
    [InlineData("D100", 1920, 960)]
    [InlineData("M100", 7168, 7168)]
    public async Task Mc_client_accepts_the_mc_maximum_write_quantity(
        string address,
        int byteCount,
        ushort expectedQuantity)
    {
        McRequest? executedRequest = null;
        var adapter = new ScriptedMcClientAdapter(
            new McResponse(ReadOnlyMemory<byte>.Empty),
            request => executedRequest = request);
        await using var device = new McClientDevice(new McParam(), adapter);

        Assert.True(await device.WriteAsync(address, new byte[byteCount]));

        Assert.NotNull(executedRequest);
        Assert.Equal(expectedQuantity, executedRequest.Quantity);
    }

    [Fact]
    public async Task Legacy_mc_read_uses_official_three_e_codec_and_connection_session()
    {
        await using var transport = new ScriptedByteStreamTransport();
        await using var device = new McClientDevice(new McParam
        {
            ServerIP = "127.0.0.1",
            Port = 5000,
            FrameType = "3E",
            TimeoutMs = 4000,
            RetryCount = 1,
        }, transport);

        Assert.IsAssignableFrom<IConnection>(device);
        Assert.True(await device.ConnectAsync());
        var readTask = device.ReadAsync("D100", 2);

        Assert.Equal(new byte[]
        {
            0x50, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00,
            0x0C, 0x00, 0x10, 0x00,
            0x01, 0x04, 0x00, 0x00,
            0x64, 0x00, 0x00, 0xA8, 0x02, 0x00,
        }, await transport.ReadRequestAsync(21));
        await transport.WriteResponseAsync(new byte[]
        {
            0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00,
            0x06, 0x00, 0x00, 0x00, 0x34, 0x12, 0x78, 0x56,
        });

        Assert.Equal(new byte[] { 0x12, 0x34, 0x56, 0x78 }, await readTask);
        Assert.Equal(ConnectionState.Connected, ((IConnection)device).State);
    }

    private sealed class ScriptedByteStreamTransport : IByteStreamTransport
    {
        private readonly Pipe _requests = new();
        private readonly Pipe _responses = new();
        public PipeReader Input => _responses.Reader;
        public PipeWriter Output => _requests.Writer;
        public bool IsOpen { get; private set; }

        public ValueTask OpenAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IsOpen = true;
            return ValueTask.CompletedTask;
        }

        public async ValueTask CloseAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsOpen) return;
            IsOpen = false;
            await _requests.Writer.CompleteAsync();
            await _responses.Writer.CompleteAsync();
        }

        public async ValueTask<byte[]> ReadRequestAsync(int length)
        {
            var read = await _requests.Reader.ReadAsync();
            try
            {
                return read.Buffer.Slice(0, length).ToArray();
            }
            finally
            {
                _requests.Reader.AdvanceTo(read.Buffer.GetPosition(length));
            }
        }

        public ValueTask<FlushResult> WriteResponseAsync(ReadOnlyMemory<byte> response) =>
            _responses.Writer.WriteAsync(response);

        public async ValueTask DisposeAsync()
        {
            await CloseAsync();
            await _requests.Reader.CompleteAsync();
            await _responses.Reader.CompleteAsync();
        }
    }

    private sealed class ScriptedMcClientAdapter(
        McResponse response,
        Action<McRequest> verifyRequest) : IMcClientAdapter
    {
        public ConnectionState State { get; private set; } = ConnectionState.Disconnected;

        public ValueTask ConnectAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            State = ConnectionState.Connected;
            return ValueTask.CompletedTask;
        }

        public ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            State = ConnectionState.Disconnected;
            return ValueTask.CompletedTask;
        }

        public ValueTask<McResponse> ExecuteAsync(
            McRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            verifyRequest(request);
            return ValueTask.FromResult(response);
        }

        public ValueTask DisposeAsync()
        {
            State = ConnectionState.Disconnected;
            return ValueTask.CompletedTask;
        }
    }
}
