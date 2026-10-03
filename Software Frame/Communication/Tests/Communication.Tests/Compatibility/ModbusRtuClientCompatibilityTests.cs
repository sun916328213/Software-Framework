// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ModbusRtuClientCompatibilityTests、ScriptedByteStreamTransport。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Buffers、System.IO.Pipelines、Communication.Core.Abstractions、Communication.Core.Protocols.Modbus.ModbusRtu、Communication.Runtime.Protocols.Modbus.ModbusRtu、Communication.Runtime.Transports。导入不代表所有类型都会在运行时加载。
using System.Buffers;
using System.IO.Pipelines;
using Communication.Core.Abstractions;
using Communication.Core.Protocols.Modbus.ModbusRtu;
using Communication.Runtime.Protocols.Modbus.ModbusRtu;
using Communication.Runtime.Transports;
using Xunit;

namespace Communication.Tests.Compatibility;

/// <summary>
/// ModbusRtuClientCompatibilityTests 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class ModbusRtuClientCompatibilityTests
{
    /// <summary>
    /// 验证 Legacy_read_uses_serial_transport_codec_and_connection_session 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 IsAssignableFrom、True、ConnectAsync、ReadAsync、Equal；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Legacy_read_uses_serial_transport_codec_and_connection_session()
    {
        await using var transport = new ScriptedByteStreamTransport();
        await using var device = new ModbusRtuClientDevice(new ModbusRtuParam
        {
            PortName = "COM_TEST",
            SlaveId = 1,
            TimeoutMs = 1000,
            RetryCount = 1,
        }, transport);

        Assert.IsAssignableFrom<IConnection>(device);
        Assert.True(await device.ConnectAsync());
        var readTask = device.ReadAsync("40001", 2);

        Assert.Equal(
            new byte[] { 0x01, 0x03, 0x00, 0x00, 0x00, 0x02, 0xC4, 0x0B },
            await transport.ReadRequestAsync(8));
        await transport.WriteResponseAsync(
            new byte[] { 0x01, 0x03, 0x04, 0x00, 0x2A, 0x00, 0x2B, 0x9B, 0xE4 });

        Assert.Equal(new byte[] { 0x00, 0x2A, 0x00, 0x2B }, await readTask);
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
            await _requests.Writer.CompleteAsync();
            await _responses.Writer.CompleteAsync();
        }

        /// <summary>
        /// 验证 ReadRequestAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
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
        /// 验证 WriteResponseAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 WriteAsync；具体条件和先后次序见方法体。
        /// </summary>
        public ValueTask<FlushResult> WriteResponseAsync(ReadOnlyMemory<byte> response) =>
            _responses.Writer.WriteAsync(response);

        /// <summary>
        /// 验证 DisposeAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
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
}
