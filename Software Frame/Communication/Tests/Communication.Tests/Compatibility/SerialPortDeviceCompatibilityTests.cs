// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：SerialPortDeviceCompatibilityTests、ScriptedByteStreamTransport。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Buffers、System.Threading.Channels、System.IO.Pipelines、Communication.Core.Abstractions、Communication.Core.Protocols.Serial、Communication.Runtime.Protocols.Serial。导入不代表所有类型都会在运行时加载。
using System.Buffers;
using System.Threading.Channels;
using System.IO.Pipelines;
using Communication.Core.Abstractions;
using Communication.Core.Protocols.Serial;
using Communication.Runtime.Protocols.Serial;
using Communication.Runtime.Transports;
using Xunit;

namespace Communication.Tests.Compatibility;

/// <summary>
/// SerialPortDeviceCompatibilityTests 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class SerialPortDeviceCompatibilityTests
{
    /// <summary>
    /// 测试相关入口 Legacy_serial_device_uses_byte_stream_transport_and_connection_seam；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 TrySetResult、IsAssignableFrom、True、ConnectAsync、WriteIncomingAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Legacy_serial_device_uses_byte_stream_transport_and_connection_seam()
    {
        await using var transport = new ScriptedByteStreamTransport();
        await using var device = new SerialPortDevice(new SerialParam
        {
            PortName = "COM_TEST",
            Delimiter = null,
        }, transport);
        var received = new TaskCompletionSource<byte[]>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        device.DataReceived += (_, args) => received.TrySetResult(args.Data);

        Assert.IsAssignableFrom<IConnection>(device);
        var rawSender = Assert.IsAssignableFrom<IRawSender>(device);
        var requestReply = Assert.IsAssignableFrom<IRequestReplyClient>(device);
        var receiveSource = Assert.IsAssignableFrom<IReceiveSource<ReceivedPacket>>(device);
        Assert.True(await device.ConnectAsync());
        await transport.WriteIncomingAsync(new byte[] { 0x41, 0x42 });

        Assert.Equal(
            new byte[] { 0x41, 0x42 },
            await received.Task.WaitAsync(TimeSpan.FromSeconds(1)));
        Assert.Equal(
            new byte[] { 0x41, 0x42 },
            (await receiveSource.ReceiveAsync()).Data.ToArray());
        Assert.Equal(ConnectionState.Connected, ((IConnection)device).State);

        Assert.True(await rawSender.SendAsync(new byte[] { 0x10 }));
        Assert.Equal(new byte[] { 0x10 }, await transport.ReadOutgoingAsync());

        var responseTask = requestReply.SendAndReceiveAsync(new byte[] { 0x11 });
        Assert.Equal(new byte[] { 0x11 }, await transport.ReadOutgoingAsync());
        await transport.WriteIncomingAsync(new byte[] { 0x22 });
        Assert.Equal(new byte[] { 0x22 }, await responseTask);
        Assert.Equal(
            new byte[] { 0x22 },
            (await receiveSource.ReceiveAsync()).Data.ToArray());

        var pendingReceive = receiveSource.ReceiveAsync().AsTask();
        await device.DisconnectAsync();
        await Assert.ThrowsAsync<ChannelClosedException>(() => pendingReceive);
        Assert.Equal(ConnectionState.Disconnected, ((IConnection)device).State);
    }

    /// <summary>
    /// ScriptedByteStreamTransport 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：IByteStreamTransport。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class ScriptedByteStreamTransport : IByteStreamTransport
    {
        private readonly Pipe _incoming = new();
        private readonly Pipe _outgoing = new();

        public PipeReader Input => _incoming.Reader;
        public PipeWriter Output => _outgoing.Writer;
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
            if (!IsOpen)
            {
                return;
            }
            IsOpen = false;
            await _incoming.Writer.CompleteAsync();
            await _outgoing.Writer.CompleteAsync();
        }

        /// <summary>
        /// 测试相关入口 WriteIncomingAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 WriteAsync；具体条件和先后次序见方法体。
        /// </summary>
        public ValueTask<FlushResult> WriteIncomingAsync(ReadOnlyMemory<byte> data) =>
            _incoming.Writer.WriteAsync(data);

        /// <summary>
        /// 测试相关入口 ReadOutgoingAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 ReadAsync、ToArray、AdvanceTo；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        public async Task<byte[]> ReadOutgoingAsync()
        {
            var result = await _outgoing.Reader.ReadAsync();
            var bytes = result.Buffer.ToArray();
            _outgoing.Reader.AdvanceTo(result.Buffer.End);
            return bytes;
        }

        /// <summary>
        /// 测试相关入口 DisposeAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 CloseAsync、CompleteAsync；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            await CloseAsync();
            await _incoming.Reader.CompleteAsync();
            await _outgoing.Reader.CompleteAsync();
        }
    }
}
