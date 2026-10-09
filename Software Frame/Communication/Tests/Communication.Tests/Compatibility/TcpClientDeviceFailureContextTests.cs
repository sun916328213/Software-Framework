// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：TcpClientDeviceFailureContextTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.IO.Pipelines、System.Net、System.Net.Sockets、Communication.Core、Communication.Core.Abstractions、Communication.Core.EventArgs、Communication.Core.Protocols.Socket、Communication.Runtime.Protocols.Socket、Communication.Runtime.Transports、Xunit。导入不代表所有类型都会在运行时加载。
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using Communication.Core;
using Communication.Core.Abstractions;
using Communication.Core.EventArgs;
using Communication.Core.Protocols.Socket;
using Communication.Runtime.Protocols.Socket;
using Communication.Runtime.Transports;
using Xunit;

namespace Communication.Tests.Compatibility;

/// <summary>
/// T13：TCP 客户端的失败必须保留原异常 cause 与端点上下文。
///
/// <para>
/// 改造前 <c>IConnection.ConnectAsync</c> 与 <c>SendAndReceiveAsync</c> 只抛
/// <c>new IOException("TCP 客户端连接失败。")</c> / <c>new IOException("TCP 数据发送失败。")</c> ——
/// 既没有 <see cref="Exception.InnerException"/>，也没有端点，调用方无法区分
/// 「主机名解析不了」「连接被拒绝」「对端半开导致写超时」，只能看到一句固定的、与现场无关的文案。
/// 根因是**冻结的** <c>Task&lt;bool&gt; ConnectAsync</c> / <c>SendAsync</c> 只能返回 bool，
/// 原因就在这个边界上被压缩掉了；本文件钉住桥把 cause 与端点还原回来。
/// </para>
///
/// <para>
/// 用可编排的 <see cref="ScriptedByteStreamTransport"/> 制造失败，而不是依赖真实 socket 的
/// 时序与平台错误码；另外单独用一个真实「已关闭端口」用例证明生产路径上确实拿得到
/// <see cref="SocketException"/>。
/// </para>
/// </summary>
[Collection("Diagnostics")]
public sealed class TcpClientDeviceFailureContextTests
{
    private const string Host = "10.11.12.13";
    private const int Port = 502;

    private static string Endpoint => $"{Host}:{Port}";

    /// <summary>测试参数：关掉自动重连，避免后台恢复循环干扰断言。</summary>
    private static SocketParam NewParam() => new()
    {
        ServerIP = Host,
        Port = Port,
        Delimiter = null,
        AutoReconnect = false,
        TimeoutMs = 1000,
    };

    /// <summary>申请一个「刚被释放、当前无人监听」的本地端口，用于制造真实的连接拒绝。</summary>
    private static int ReserveClosedPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    // ===== 连接失败 =====

    /// <summary>
    /// 核心用例：<c>IConnection.ConnectAsync</c> 抛出的异常必须挂着原异常，且消息里带端点。
    /// 改造前这里 <c>InnerException</c> 为 null、消息里没有端点。
    /// </summary>
    [Fact]
    public async Task Connect_failure_carries_original_cause_and_endpoint()
    {
        var cause = new SocketException((int)SocketError.ConnectionRefused);
        var transport = new ScriptedByteStreamTransport { OpenFailure = cause };
        await using var device = new TcpClientDevice(NewParam(), transport);

        var failure = await Assert.ThrowsAsync<IOException>(
            () => ((IConnection)device).ConnectAsync().AsTask());

        Assert.Same(cause, failure.InnerException);
        Assert.Contains(Endpoint, failure.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(SocketException), failure.Message, StringComparison.Ordinal);
        Assert.Contains(cause.Message, failure.Message, StringComparison.Ordinal);
    }

    /// <summary>消息里要说清是**哪个操作**失败，不能只给一句「连接失败」。</summary>
    [Fact]
    public async Task Connect_failure_message_names_the_operation()
    {
        var transport = new ScriptedByteStreamTransport
        {
            OpenFailure = new IOException("底层套接字打开失败"),
        };
        await using var device = new TcpClientDevice(NewParam(), transport);

        var failure = await Assert.ThrowsAsync<IOException>(
            () => ((IConnection)device).ConnectAsync().AsTask());

        Assert.StartsWith("TCP 客户端连接失败", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 回归网：桥换了异常形态，但 <c>ErrorOccurred</c> 事件必须照旧带着**同一个**原异常触发。
    /// （桥的实现从 <c>OnErrorOccurred(ex)</c> 换成了 <c>RecordFailure(ex)</c>，后者必须仍然上报。）
    /// </summary>
    [Fact]
    public async Task Connect_failure_still_raises_the_error_event_with_the_same_cause()
    {
        var cause = new IOException("底层套接字打开失败");
        var transport = new ScriptedByteStreamTransport { OpenFailure = cause };
        await using var device = new TcpClientDevice(NewParam(), transport);

        CommunicationErrorEventArgs? observed = null;
        device.ErrorOccurred += (_, args) => observed = args;

        await Assert.ThrowsAsync<IOException>(
            () => ((IConnection)device).ConnectAsync().AsTask());

        Assert.NotNull(observed);
        Assert.Same(cause, observed!.Exception);
        Assert.Equal(device.DeviceName, observed.DeviceName);
    }

    /// <summary>真实 socket：连一个没人监听的端口，拿到的必须是 <see cref="SocketException"/>。</summary>
    [Fact]
    public async Task Connect_failure_against_a_closed_port_carries_the_socket_error()
    {
        var port = ReserveClosedPort();
        var param = NewParam();
        param.ServerIP = IPAddress.Loopback.ToString();
        param.Port = port;
        await using var device = new TcpClientDevice(param);

        var failure = await Assert.ThrowsAsync<IOException>(
            () => ((IConnection)device).ConnectAsync().AsTask());

        Assert.IsAssignableFrom<SocketException>(failure.InnerException);
        Assert.Contains($"{param.ServerIP}:{port}", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 报告的原因必须是**这一次**尝试的原因。第一次失败 → 恢复并连成功 → 断开 → 用另一个原因再失败，
    /// 此时桥给的 cause 必须是第二个，而不是第一次留下的 SocketException。
    /// </summary>
    [Fact]
    public async Task Connect_failure_after_a_successful_connect_reports_the_new_cause()
    {
        var transport = new ScriptedByteStreamTransport
        {
            OpenFailure = new SocketException((int)SocketError.ConnectionRefused),
        };
        await using var device = new TcpClientDevice(NewParam(), transport);

        var first = await Assert.ThrowsAsync<IOException>(
            () => ((IConnection)device).ConnectAsync().AsTask());
        Assert.IsAssignableFrom<SocketException>(first.InnerException);

        transport.OpenFailure = null;
        await ((IConnection)device).ConnectAsync();
        Assert.Equal(ConnectionState.Connected, ((IConnection)device).State);

        await device.DisconnectAsync();
        var second = new InvalidOperationException("第二次尝试的失败原因");
        transport.OpenFailure = second;

        var failure = await Assert.ThrowsAsync<IOException>(
            () => ((IConnection)device).ConnectAsync().AsTask());

        Assert.Same(second, failure.InnerException);
        Assert.Contains(nameof(InvalidOperationException), failure.Message, StringComparison.Ordinal);
    }

    /// <summary>反向对照：连接成功时桥<b>不能</b>抛异常。</summary>
    [Fact]
    public async Task Successful_connect_does_not_throw_through_the_bridge()
    {
        var transport = new ScriptedByteStreamTransport();
        await using var device = new TcpClientDevice(NewParam(), transport);

        await ((IConnection)device).ConnectAsync();

        Assert.Equal(ConnectionState.Connected, ((IConnection)device).State);
    }

    // ===== 发送失败 =====

    /// <summary>
    /// 核心用例：<c>SendAndReceiveAsync</c> 的发送失败路径同样要带 cause 与端点。
    /// </summary>
    [Fact]
    public async Task Send_failure_carries_original_cause_and_endpoint()
    {
        var cause = new IOException("底层写入被中断");
        var transport = new ScriptedByteStreamTransport { WriteFailure = cause };
        await using var device = new TcpClientDevice(NewParam(), transport);
        await ((IConnection)device).ConnectAsync();

        var failure = await Assert.ThrowsAsync<IOException>(
            () => device.SendAndReceiveAsync(new byte[] { 0x10 }));

        Assert.Same(cause, failure.InnerException);
        Assert.Contains(Endpoint, failure.Message, StringComparison.Ordinal);
        Assert.Contains(cause.Message, failure.Message, StringComparison.Ordinal);
    }

    /// <summary>发送失败的消息要标明是「数据发送」而不是笼统的「连接失败」。</summary>
    [Fact]
    public async Task Send_failure_message_names_the_operation()
    {
        var transport = new ScriptedByteStreamTransport
        {
            WriteFailure = new IOException("底层写入被中断"),
        };
        await using var device = new TcpClientDevice(NewParam(), transport);
        await ((IConnection)device).ConnectAsync();

        var failure = await Assert.ThrowsAsync<IOException>(
            () => device.SendAndReceiveAsync(new byte[] { 0x10 }));

        Assert.StartsWith("TCP 数据发送失败", failure.Message, StringComparison.Ordinal);
    }

    // ===== 冻结的旧接口语义不能被顺手改掉 =====

    /// <summary>
    /// <c>ICommunicationDevice.ConnectAsync</c> 仍是「返回 bool、不抛」的兼容入口
    /// （<c>LayeringTests.Legacy_device_interface_member_set_is_frozen</c> 钉住了成员集）。
    /// T13 只在桥上加信息，不能把失败改成抛出。
    /// </summary>
    [Fact]
    public async Task Legacy_connect_still_reports_false_without_throwing()
    {
        var transport = new ScriptedByteStreamTransport
        {
            OpenFailure = new IOException("底层套接字打开失败"),
        };
        await using var device = new TcpClientDevice(NewParam(), transport);

        Assert.False(await device.ConnectAsync());
    }

    /// <summary>
    /// <c>IRawSender.SendAsync</c> 同样是 bool 语义；发送失败必须返回 false 而不是抛。
    /// </summary>
    [Fact]
    public async Task Raw_send_still_reports_false_without_throwing()
    {
        var transport = new ScriptedByteStreamTransport
        {
            WriteFailure = new IOException("底层写入被中断"),
        };
        await using var device = new TcpClientDevice(NewParam(), transport);
        await ((IConnection)device).ConnectAsync();

        var sender = Assert.IsAssignableFrom<IRawSender>(device);

        Assert.False(await sender.SendAsync(new byte[] { 0x10 }));
    }

    // ===== 失败原因不会张冠李戴 =====

    /// <summary>
    /// 一次成功之后必须清掉上一次保留的失败原因。否则将来某条「返回 false 但没记录原因」的路径
    /// 会把**上一次**的异常当成这一次的 cause 报出去 —— 对诊断而言，错的 cause 比没有 cause 更坏。
    /// </summary>
    [Fact]
    public async Task Successful_connect_clears_the_recorded_failure()
    {
        var transport = new ScriptedByteStreamTransport
        {
            OpenFailure = new IOException("底层套接字打开失败"),
        };
        await using var device = new FailureProbeDevice(NewParam(), transport);

        await Assert.ThrowsAsync<IOException>(
            () => ((IConnection)device).ConnectAsync().AsTask());
        Assert.NotNull(device.PeekLastFailure());

        transport.OpenFailure = null;
        await ((IConnection)device).ConnectAsync();

        Assert.Equal(ConnectionState.Connected, ((IConnection)device).State);
        Assert.Null(device.PeekLastFailure());
    }

    /// <summary>发送成功同样要清掉上一次保留的失败原因。</summary>
    [Fact]
    public async Task Successful_send_clears_the_recorded_failure()
    {
        var transport = new ScriptedByteStreamTransport
        {
            WriteFailure = new IOException("底层写入被中断"),
        };
        await using var device = new FailureProbeDevice(NewParam(), transport);
        await ((IConnection)device).ConnectAsync();
        var sender = Assert.IsAssignableFrom<IRawSender>(device);

        Assert.False(await sender.SendAsync(new byte[] { 0x10 }));
        Assert.NotNull(device.PeekLastFailure());

        transport.WriteFailure = null;
        Assert.True(await sender.SendAsync(new byte[] { 0x10 }));

        Assert.Null(device.PeekLastFailure());
    }

    /// <summary>
    /// 暴露基类 <c>protected LastFailure</c> 的探针。
    /// 该成员是「bool 边界丢原因」与「桥还原原因」之间唯一的中间状态，需要能被直接观测。
    /// </summary>
    private sealed class FailureProbeDevice : TcpClientDevice
    {
        public FailureProbeDevice(SocketParam param, IByteStreamTransport transport)
            : base(param, transport)
        {
        }

        /// <summary>读取基类保留的最近一次失败原异常。</summary>
        public Exception? PeekLastFailure() => LastFailure;
    }

    /// <summary>
    /// 可编排的字节流传输：把「打开失败」「写入失败」变成确定性事件，
    /// 避免依赖真实 socket 的时序与平台错误码。正常路径与
    /// <c>SerialPortDeviceCompatibilityTests.ScriptedByteStreamTransport</c> 同形。
    /// </summary>
    private sealed class ScriptedByteStreamTransport : IByteStreamTransport
    {
        private readonly Pipe _incoming = new();
        private readonly Pipe _outgoing = new();

        /// <summary>下一次 <see cref="OpenAsync"/> 要抛的异常；为空表示打开成功。</summary>
        public Exception? OpenFailure { get; set; }

        /// <summary>写方向要抛的异常；为空表示写入成功。</summary>
        public Exception? WriteFailure { get; set; }

        public PipeReader Input => _incoming.Reader;

        public PipeWriter Output =>
            WriteFailure is null ? _outgoing.Writer : new ThrowingPipeWriter(WriteFailure);

        public bool IsOpen { get; private set; }

        public ValueTask OpenAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (OpenFailure is not null)
            {
                return ValueTask.FromException(OpenFailure);
            }

            IsOpen = true;
            return ValueTask.CompletedTask;
        }

        public async ValueTask CloseAsync(CancellationToken cancellationToken = default)
        {
            if (!IsOpen)
            {
                return;
            }

            IsOpen = false;
            await _incoming.Writer.CompleteAsync();
            await _outgoing.Writer.CompleteAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await CloseAsync();
            await _incoming.Reader.CompleteAsync();
            await _outgoing.Reader.CompleteAsync();
        }

        /// <summary>只用于制造写失败的 <see cref="PipeWriter"/>；所有写入入口都抛同一个异常。</summary>
        private sealed class ThrowingPipeWriter : PipeWriter
        {
            private readonly Exception _failure;

            public ThrowingPipeWriter(Exception failure) => _failure = failure;

            public override void Advance(int bytes) => throw _failure;

            public override void CancelPendingFlush()
            {
            }

            public override void Complete(Exception? exception = null)
            {
            }

            public override Memory<byte> GetMemory(int sizeHint = 0) => throw _failure;

            public override Span<byte> GetSpan(int sizeHint = 0) => throw _failure;

            public override ValueTask<FlushResult> FlushAsync(CancellationToken cancellationToken = default) =>
                ValueTask.FromException<FlushResult>(_failure);
        }
    }
}
