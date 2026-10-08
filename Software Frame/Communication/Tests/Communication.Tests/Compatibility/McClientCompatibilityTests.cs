// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：McClientCompatibilityTests、ScriptedByteStreamTransport、ScriptedMcClientAdapter。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Buffers、System.IO.Pipelines、Communication.Core.Abstractions、Communication.Core.Protocols.Mitsubishi、Communication.Runtime.Protocols.Mitsubishi、Communication.Runtime.Transports。导入不代表所有类型都会在运行时加载。
using System.Buffers;
using System.IO.Pipelines;
using Communication.Core.Abstractions;
using Communication.Core.Protocols.Mitsubishi;
using Communication.Runtime.Protocols.Mitsubishi;
using Communication.Runtime.Transports;
using Xunit;

namespace Communication.Tests.Compatibility;

/// <summary>
/// McClientCompatibilityTests 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class McClientCompatibilityTests
{
    /// <summary>
    /// 测试相关入口 Mc_communication_timeout_follows_the_common_timeout_when_not_overridden；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Mc_communication_timeout_follows_the_common_timeout_when_not_overridden()
    {
        var parameter = new McParam { TimeoutMs = 1750 };

        Assert.Equal(1750, parameter.CommunicationTimeout);
    }

    /// <summary>
    /// 测试相关入口 Explicit_mc_communication_timeout_preserves_the_legacy_override；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Equal；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 Mx_component_class_not_registered_error_explains_the_required_runtime；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Equal、Contains；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 Mx_component_error_can_include_the_vendor_message；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Equal、Contains；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 Mx_component_backend_can_be_created_in_direct_mode；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public async Task Mx_component_backend_can_be_created_in_direct_mode()
    {
        await using var device = new McClientDevice(new McParam
        {
            Backend = McClientBackend.MxComponent,
        });

        Assert.Equal(ConnectionState.Disconnected, device.State);
    }

    /// <summary>
    /// 测试相关入口 Mx_component_rejects_an_invalid_logical_station_before_connect；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Throws、Contains、nameof；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 Mx_backend_rejects_a_non_positive_communication_timeout；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Throws；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 Direct_mx_adapter_shares_concurrent_disposal_and_cannot_be_reused；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 WhenAll、Select、Range、AsTask、DisposeAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Direct_mx_adapter_shares_concurrent_disposal_and_cannot_be_reused()
    {
        var adapter = new MxComponentMcClientAdapter(0, password: null);

        await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => adapter.DisposeAsync().AsTask()));

        Assert.Equal(ConnectionState.Disconnected, adapter.State);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => adapter.ConnectAsync().AsTask());
    }

    /// <summary>
    /// 测试相关入口 Mc_client_can_read_through_a_non_socket_adapter；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 False、Equal、Parse、True、ConnectAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 Mx_component_connection_event_identifies_the_logical_station；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 True、ConnectAsync、Equal；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 Mc_client_preserves_word_write_conversion_for_backend_adapters；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 ConnectAsync、True、WriteAsync、NotNull、Equal；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 Mc_client_rejects_an_oversized_write_before_executing_the_adapter；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 ThrowsAsync、WriteAsync、False；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 Mc_client_accepts_the_mc_maximum_write_quantity；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 True、WriteAsync、NotNull、Equal；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 Legacy_mc_read_uses_official_three_e_codec_and_connection_session；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 IsAssignableFrom、True、ConnectAsync、ReadAsync、Equal；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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

    /// <summary>
    /// ScriptedByteStreamTransport 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：IByteStreamTransport。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class ScriptedByteStreamTransport : IByteStreamTransport
    {
        private readonly Pipe _requests = new();
        private readonly Pipe _responses = new();
        public PipeReader Input => _responses.Reader;
        public PipeWriter Output => _requests.Writer;
        public bool IsOpen { get; private set; }

        /// <summary>
        /// 测试相关入口 OpenAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 ThrowIfCancellationRequested；具体条件和先后次序见方法体。
        /// </summary>
        public ValueTask OpenAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IsOpen = true;
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// 测试相关入口 CloseAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 ThrowIfCancellationRequested、CompleteAsync；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        public async ValueTask CloseAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsOpen) return;
            IsOpen = false;
            await _requests.Writer.CompleteAsync();
            await _responses.Writer.CompleteAsync();
        }

        /// <summary>
        /// 测试相关入口 ReadRequestAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 ReadAsync、ToArray、Slice、AdvanceTo、GetPosition；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
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

        /// <summary>
        /// 测试相关入口 WriteResponseAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 WriteAsync；具体条件和先后次序见方法体。
        /// </summary>
        public ValueTask<FlushResult> WriteResponseAsync(ReadOnlyMemory<byte> response) =>
            _responses.Writer.WriteAsync(response);

        /// <summary>
        /// 测试相关入口 DisposeAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 CloseAsync、CompleteAsync；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            await CloseAsync();
            await _requests.Reader.CompleteAsync();
            await _responses.Reader.CompleteAsync();
        }
    }

    /// <summary>
    /// ScriptedMcClientAdapter 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：IMcClientAdapter。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class ScriptedMcClientAdapter(
        McResponse response,
        Action<McRequest> verifyRequest) : IMcClientAdapter
    {
        public ConnectionState State { get; private set; } = ConnectionState.Disconnected;

        /// <summary>
        /// 测试相关入口 ConnectAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 ThrowIfCancellationRequested；具体条件和先后次序见方法体。
        /// </summary>
        public ValueTask ConnectAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            State = ConnectionState.Connected;
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// 测试相关入口 DisconnectAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 ThrowIfCancellationRequested；具体条件和先后次序见方法体。
        /// </summary>
        public ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            State = ConnectionState.Disconnected;
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// 测试相关入口 ExecuteAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 ThrowIfCancellationRequested、verifyRequest、FromResult；具体条件和先后次序见方法体。
        /// </summary>
        public ValueTask<McResponse> ExecuteAsync(
            McRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            verifyRequest(request);
            return ValueTask.FromResult(response);
        }

        /// <summary>
        /// 测试相关入口 DisposeAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public ValueTask DisposeAsync()
        {
            State = ConnectionState.Disconnected;
            return ValueTask.CompletedTask;
        }
    }
}
