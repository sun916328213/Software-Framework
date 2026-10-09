// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：DeviceReceiveContractTests、ScriptedByteStreamTransport。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.IO.Pipelines、Communication.Core.Abstractions、Communication.Core.Protocols.Serial、Communication.Core.Protocols.Socket、Communication.Runtime.Protocols.Serial、Communication.Runtime.Protocols.Socket、Communication.Runtime.Transports。导入不代表所有类型都会在运行时加载。
using System.IO.Pipelines;
using Communication.Core.Abstractions;
using Communication.Core.Protocols.Serial;
using Communication.Core.Protocols.Socket;
using Communication.Runtime.Protocols.Serial;
using Communication.Runtime.Protocols.Socket;
using Communication.Runtime.Transports;
using Xunit;

namespace Communication.Tests.Compatibility;

/// <summary>
/// T15：锁定「连续字节流设备」的接收合同。TCP 客户端与串口必须给出同一套结论。
/// </summary>
/// <remarks>
/// 三条被钉住的约定：
/// <list type="number">
/// <item>可靠收件箱拿到的永远是<b>未分帧的原始块</b>；配置了分包结束符时，旧 <c>DataReceived</c>
/// 事件只承载<b>完整帧</b>。改造前是「原始块进收件箱 + 原始块进事件 + 帧进事件」，
/// 订阅者会同时收到原始块和它的各个帧 —— 同一批数据投递两遍。</item>
/// <item>接收循环结束时，状态与原因都必须<b>如实</b>：本地故障（收件箱满、分帧缓冲超限）不能
/// 被说成「远端连接已断开」，串口的字节流结束也不能静默什么都不报。</item>
/// <item>请求-回复关联是可靠内部路径，必须先于用户事件与收件箱投递完成，否则收件箱拒绝一条
/// 报文就会让等待中的请求一直挂到超时。</item>
/// </list>
/// </remarks>
public sealed class DeviceReceiveContractTests
{
    private static readonly byte[] CrLf = [0x0D, 0x0A];

    private static SocketParam TcpParam(string? delimiter, int receiveQueueCapacity = 256) => new()
    {
        ServerIP = "127.0.0.1",
        Port = 1,
        Delimiter = delimiter,
        TimeoutMs = 1000,
        // 本文件只关心接收路径；自动重连会额外拉起一条恢复循环，掩盖状态断言。
        AutoReconnect = false,
        ReceiveQueueCapacity = receiveQueueCapacity,
    };

    private static SerialParam SerialParamWith(string? delimiter, int receiveQueueCapacity = 256) => new()
    {
        PortName = "COM_TEST",
        Delimiter = delimiter,
        TimeoutMs = 1000,
        AutoReconnect = false,
        ReceiveQueueCapacity = receiveQueueCapacity,
    };

    // ── 1. 收件箱与事件的投递分离 ───────────────────────────────────────

    [Fact]
    public async Task Delimiter_configured_tcp_device_delivers_raw_block_to_inbox_and_only_frames_to_event()
    {
        await using var transport = new ScriptedByteStreamTransport();
        await using var device = new TcpClientDevice(TcpParam("\r\n"), transport);
        var events = new List<byte[]>();
        var gate = new object();
        var firstEvent = new TaskCompletionSource<byte[]>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        device.DataReceived += (_, args) =>
        {
            lock (gate)
            {
                events.Add(args.Data);
            }

            firstEvent.TrySetResult(args.Data);
        };

        Assert.True(await device.ConnectAsync());
        var raw = new byte[] { 0x41, 0x42, 0x0D, 0x0A, 0x43 };
        await transport.WriteIncomingAsync(raw);

        Assert.Equal(raw, (await device.ReceiveAsync()).Data.ToArray());
        Assert.Equal(
            new byte[] { 0x41, 0x42 },
            await firstEvent.Task.WaitAsync(TimeSpan.FromSeconds(1)));

        // 给重复投递留出机会：改造前这里还会再冒出一个「原始块」事件。
        await Task.Delay(200);
        lock (gate)
        {
            Assert.Single(events);
        }
    }

    [Fact]
    public async Task Delimiter_configured_serial_device_delivers_raw_block_to_inbox_and_only_frames_to_event()
    {
        await using var transport = new ScriptedByteStreamTransport();
        await using var device = new SerialPortDevice(SerialParamWith("\r\n"), transport);
        var events = new List<byte[]>();
        var gate = new object();
        var firstEvent = new TaskCompletionSource<byte[]>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        device.DataReceived += (_, args) =>
        {
            lock (gate)
            {
                events.Add(args.Data);
            }

            firstEvent.TrySetResult(args.Data);
        };

        Assert.True(await device.ConnectAsync());
        var raw = new byte[] { 0x41, 0x42, 0x0D, 0x0A, 0x43 };
        await transport.WriteIncomingAsync(raw);

        Assert.Equal(raw, (await device.ReceiveAsync()).Data.ToArray());
        Assert.Equal(
            new byte[] { 0x41, 0x42 },
            await firstEvent.Task.WaitAsync(TimeSpan.FromSeconds(1)));

        await Task.Delay(200);
        lock (gate)
        {
            Assert.Single(events);
        }
    }

    [Fact]
    public async Task Tcp_device_without_delimiter_delivers_the_raw_block_to_both_paths()
    {
        await using var transport = new ScriptedByteStreamTransport();
        await using var device = new TcpClientDevice(TcpParam(null), transport);
        var events = new List<byte[]>();
        var gate = new object();
        var firstEvent = new TaskCompletionSource<byte[]>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        device.DataReceived += (_, args) =>
        {
            lock (gate)
            {
                events.Add(args.Data);
            }

            firstEvent.TrySetResult(args.Data);
        };

        Assert.True(await device.ConnectAsync());
        var raw = new byte[] { 0x41, 0x42 };
        await transport.WriteIncomingAsync(raw);

        Assert.Equal(raw, (await device.ReceiveAsync()).Data.ToArray());
        Assert.Equal(raw, await firstEvent.Task.WaitAsync(TimeSpan.FromSeconds(1)));

        await Task.Delay(200);
        lock (gate)
        {
            Assert.Single(events);
        }
    }

    // ── 2. 分帧必须是字节级的 ──────────────────────────────────────────

    [Fact]
    public async Task Tcp_device_preserves_multibyte_bytes_split_across_transport_chunks()
    {
        await using var transport = new ScriptedByteStreamTransport();
        await using var device = new TcpClientDevice(TcpParam("\r\n"), transport);
        var frame = new TaskCompletionSource<byte[]>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        device.DataReceived += (_, args) => frame.TrySetResult(args.Data);

        Assert.True(await device.ConnectAsync());

        // 「中」= E4 B8 AD，故意把分块边界落在字符中间。
        await transport.WriteIncomingAsync(new byte[] { 0xE4, 0xB8 });
        Assert.Equal(new byte[] { 0xE4, 0xB8 }, (await device.ReceiveAsync()).Data.ToArray());
        await transport.WriteIncomingAsync(new byte[] { 0xAD, 0x0D, 0x0A });

        Assert.Equal(
            new byte[] { 0xE4, 0xB8, 0xAD },
            await frame.Task.WaitAsync(TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task Serial_device_resolves_a_multibyte_delimiter_by_bytes()
    {
        await using var transport = new ScriptedByteStreamTransport();
        await using var device = new SerialPortDevice(SerialParamWith("√"), transport);
        var frame = new TaskCompletionSource<byte[]>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        device.DataReceived += (_, args) => frame.TrySetResult(args.Data);

        Assert.True(await device.ConnectAsync());

        // 「√」的 UTF-8 是 E2 88 9A。若结束符被按 ASCII 之类的编码转成字节，这里永远切不出帧。
        await transport.WriteIncomingAsync(new byte[] { 0x41, 0xE2, 0x88, 0x9A });

        Assert.Equal(new byte[] { 0x41 }, await frame.Task.WaitAsync(TimeSpan.FromSeconds(1)));
    }

    // ── 3. 接收循环结束的原因必须如实 ──────────────────────────────────

    [Fact]
    public async Task Tcp_device_reports_remote_close_as_remote_close()
    {
        await using var transport = new ScriptedByteStreamTransport();
        await using var device = new TcpClientDevice(TcpParam(null), transport);
        var closed = new TaskCompletionSource<string?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        device.ConnectionStateChanged += (_, args) =>
        {
            if (!args.IsConnected)
            {
                closed.TrySetResult(args.Message);
            }
        };

        Assert.True(await device.ConnectAsync());
        await transport.CompleteIncomingAsync();

        Assert.Equal("远端连接已断开", await closed.Task.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Equal(ConnectionState.Faulted, ((IConnection)device).State);
    }

    [Fact]
    public async Task Serial_device_reports_stream_end_as_a_fault()
    {
        await using var transport = new ScriptedByteStreamTransport();
        await using var device = new SerialPortDevice(SerialParamWith(null), transport);
        var closed = new TaskCompletionSource<string?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        device.ConnectionStateChanged += (_, args) =>
        {
            if (!args.IsConnected)
            {
                closed.TrySetResult(args.Message);
            }
        };

        Assert.True(await device.ConnectAsync());
        await transport.CompleteIncomingAsync();

        // 改造前串口接收循环在这里直接 break，既不结束接收代际也不改状态：
        // 设备继续对外报告 Connected，而接收循环已经死了。
        Assert.Equal("串口字节流已结束", await closed.Task.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Equal(ConnectionState.Faulted, ((IConnection)device).State);
    }

    [Fact]
    public async Task Tcp_device_reports_local_inbox_overflow_as_a_local_failure()
    {
        await using var transport = new ScriptedByteStreamTransport();
        await using var device = new TcpClientDevice(TcpParam(null, receiveQueueCapacity: 1), transport);
        var firstAccepted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var closed = new TaskCompletionSource<string?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        device.DataReceived += (_, _) => firstAccepted.TrySetResult();
        device.ConnectionStateChanged += (_, args) =>
        {
            if (!args.IsConnected)
            {
                closed.TrySetResult(args.Message);
            }
        };

        Assert.True(await device.ConnectAsync());
        await transport.WriteIncomingAsync(new byte[] { 0x01 });
        // DataReceived 在收件箱投递成功之后才触发，所以这一刻收件箱确实已满。
        await firstAccepted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await transport.WriteIncomingAsync(new byte[] { 0x02 });

        var reason = await closed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(ConnectionState.Faulted, ((IConnection)device).State);
        Assert.Contains("接收队列已达容量上限", reason);
        Assert.DoesNotContain("远端连接已断开", reason);
    }

    [Fact]
    public async Task Serial_device_reports_local_inbox_overflow_as_a_local_failure()
    {
        await using var transport = new ScriptedByteStreamTransport();
        await using var device = new SerialPortDevice(SerialParamWith(null, receiveQueueCapacity: 1), transport);
        var firstAccepted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var closed = new TaskCompletionSource<string?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        device.DataReceived += (_, _) => firstAccepted.TrySetResult();
        device.ConnectionStateChanged += (_, args) =>
        {
            if (!args.IsConnected)
            {
                closed.TrySetResult(args.Message);
            }
        };

        Assert.True(await device.ConnectAsync());
        await transport.WriteIncomingAsync(new byte[] { 0x01 });
        await firstAccepted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await transport.WriteIncomingAsync(new byte[] { 0x02 });

        var reason = await closed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(ConnectionState.Faulted, ((IConnection)device).State);
        Assert.Contains("接收队列已达容量上限", reason);
        Assert.DoesNotContain("串口字节流已结束", reason);
    }

    // ── 4. 请求关联不能被收件箱的拒绝连累 ──────────────────────────────

    [Fact]
    public async Task Tcp_pending_request_completes_even_when_the_inbox_rejects_the_block()
    {
        await using var transport = new ScriptedByteStreamTransport();
        await using var device = new TcpClientDevice(TcpParam(null, receiveQueueCapacity: 1), transport);
        var firstAccepted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        device.DataReceived += (_, _) => firstAccepted.TrySetResult();

        Assert.True(await device.ConnectAsync());
        await transport.WriteIncomingAsync(new byte[] { 0x01 });
        await firstAccepted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        var response = device.SendAndReceiveAsync([0x10]);
        await transport.WriteIncomingAsync(new byte[] { 0x20 });

        Assert.Equal(new byte[] { 0x20 }, await response.WaitAsync(TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task Serial_pending_request_completes_even_when_the_inbox_rejects_the_block()
    {
        await using var transport = new ScriptedByteStreamTransport();
        await using var device = new SerialPortDevice(SerialParamWith(null, receiveQueueCapacity: 1), transport);
        var firstAccepted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        device.DataReceived += (_, _) => firstAccepted.TrySetResult();

        Assert.True(await device.ConnectAsync());
        await transport.WriteIncomingAsync(new byte[] { 0x01 });
        await firstAccepted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        // 改造前串口把 pendingResponse 的关联放在 OnPacketReceived 之后：收件箱一拒绝，
        // 抛出的异常就会跳过关联，等待中的请求只能挂到超时。
        var response = device.SendAndReceiveAsync([0x10]);
        await transport.WriteIncomingAsync(new byte[] { 0x20 });

        Assert.Equal(new byte[] { 0x20 }, await response.WaitAsync(TimeSpan.FromSeconds(1)));
    }

    /// <summary>
    /// 可脚本化的字节流 transport：入站方向由测试写入，出站方向由测试读取。
    /// 与 <c>SerialPortDeviceCompatibilityTests</c> 内的同名实现保持同一形状，额外提供「结束入站字节流」。
    /// </summary>
    private sealed class ScriptedByteStreamTransport : IByteStreamTransport
    {
        private readonly Pipe _incoming = new();
        private readonly Pipe _outgoing = new();

        public PipeReader Input => _incoming.Reader;

        public PipeWriter Output => _outgoing.Writer;

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
            if (!IsOpen)
            {
                return;
            }

            IsOpen = false;
            await _incoming.Writer.CompleteAsync();
            await _outgoing.Writer.CompleteAsync();
        }

        public ValueTask<FlushResult> WriteIncomingAsync(ReadOnlyMemory<byte> data) =>
            _incoming.Writer.WriteAsync(data);

        /// <summary>结束入站字节流：读取端会看到 <c>IsCompleted</c>，模拟端口被外部关闭/拔出。</summary>
        public ValueTask CompleteIncomingAsync() => _incoming.Writer.CompleteAsync();

        public async ValueTask DisposeAsync()
        {
            await CloseAsync();
            await _incoming.Reader.CompleteAsync();
            await _outgoing.Reader.CompleteAsync();
        }
    }
}
