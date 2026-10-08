// 【中文阅读说明】
// MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ModbusTcpClientTests、ScriptedModbusStream。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：MotionControl.Runtime.Vendors.Inovance、Xunit。导入不代表所有类型都会在运行时加载。
using MotionControl.Runtime.Vendors.Inovance;
using Xunit;

namespace MotionControl.Tests;

/// <summary>
/// ModbusTcpClientTests 的类型定义。MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class ModbusTcpClientTests
{
    [Theory]
    [InlineData(3)]
    [InlineData(8)]
    public void TruncatedResponse_ClosesConnectionAndReturnsFailure(int length)
    {
        using var stream = new ScriptedModbusStream(request =>
            BuildResponse(request, 0x03, [0x02, 0x00, 0x01])[..length], maxReadChunk: 2);
        using var client = new ModbusTcpClient(stream);

        Assert.Null(client.ReadHoldingRegisters(1, 0, 1));
        Assert.False(client.IsConnected);
    }

    /// <summary>
    /// 测试相关入口 ReadHoldingRegisters_FragmentedResponse_IsReassembledAndValidated；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 BuildResponse、ReadHoldingRegisters、NotNull、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ReadHoldingRegisters_FragmentedResponse_IsReassembledAndValidated()
    {
        using var stream = new ScriptedModbusStream(
            request => BuildResponse(request, 0x03, [0x04, 0x12, 0x34, 0xFE, 0xDC]),
            maxReadChunk: 3);
        using var client = new ModbusTcpClient(stream);

        var values = client.ReadHoldingRegisters(2, 100, 2);

        Assert.NotNull(values);
        Assert.Equal([unchecked((short)0x1234), unchecked((short)0xFEDC)], values);
    }

    /// <summary>
    /// 测试相关入口 ReadHoldingRegisters_ExceptionResponse_DoesNotReadBeyondDeclaredLength；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 BuildResponse、ReadHoldingRegisters、Null、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ReadHoldingRegisters_ExceptionResponse_DoesNotReadBeyondDeclaredLength()
    {
        using var stream = new ScriptedModbusStream(
            request => BuildResponse(request, 0x83, [0x02]));
        using var client = new ModbusTcpClient(stream);

        var values = client.ReadHoldingRegisters(1, 0, 1);

        Assert.Null(values);
        Assert.Equal(0, stream.ReadBeyondResponseCount);
    }

    /// <summary>
    /// 测试相关入口 ResponseWithWrongTransactionId_IsRejectedAndConnectionClosed；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 BuildResponse、ReadHoldingRegisters、Null、False；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ResponseWithWrongTransactionId_IsRejectedAndConnectionClosed()
    {
        using var stream = new ScriptedModbusStream(request =>
        {
            var response = BuildResponse(request, 0x03, [0x02, 0x00, 0x01]);
            response[1]++;
            return response;
        });
        using var client = new ModbusTcpClient(stream);

        var values = client.ReadHoldingRegisters(1, 0, 1);

        Assert.Null(values);
        Assert.False(client.IsConnected);
    }

    /// <summary>
    /// 测试相关入口 WriteSingleRegister_WrongEcho_IsRejected；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 BuildResponse、ToArray、AsSpan、WriteSingleRegister、False；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void WriteSingleRegister_WrongEcho_IsRejected()
    {
        using var stream = new ScriptedModbusStream(request =>
        {
            var response = BuildResponse(request, 0x06, request.AsSpan(8, 4).ToArray());
            response[11]++;
            return response;
        });
        using var client = new ModbusTcpClient(stream);

        var succeeded = client.WriteSingleRegister(1, 100, 42);

        Assert.False(succeeded);
    }

    /// <summary>
    /// 测试相关入口 BuildResponse；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 CopyTo；具体条件和先后次序见方法体。
    /// </summary>
    private static byte[] BuildResponse(byte[] request, byte functionCode, byte[] payload)
    {
        var length = 2 + payload.Length;
        var response = new byte[8 + payload.Length];
        response[0] = request[0];
        response[1] = request[1];
        response[4] = (byte)(length >> 8);
        response[5] = (byte)length;
        response[6] = request[6];
        response[7] = functionCode;
        payload.CopyTo(response, 8);
        return response;
    }

    /// <summary>
    /// ScriptedModbusStream 的类型定义。MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：Stream。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class ScriptedModbusStream(Func<byte[], byte[]> respond, int maxReadChunk = int.MaxValue) : Stream
    {
        private byte[]? _response;
        private int _offset;

        public int ReadBeyondResponseCount { get; private set; }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        /// <summary>
        /// 测试相关入口 Flush；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public override void Flush() { }

        /// <summary>
        /// 测试相关入口 Read；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Min、BlockCopy；具体条件和先后次序见方法体。
        /// </summary>
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_response is null || _offset == _response.Length)
            {
                ReadBeyondResponseCount++;
                return 0;
            }
            var length = Math.Min(Math.Min(count, maxReadChunk), _response.Length - _offset);
            Buffer.BlockCopy(_response, _offset, buffer, offset, length);
            _offset += length;
            return length;
        }

        /// <summary>
        /// 测试相关入口 Write；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 respond、ToArray、AsSpan；具体条件和先后次序见方法体。
        /// </summary>
        public override void Write(byte[] buffer, int offset, int count)
        {
            _response = respond(buffer.AsSpan(offset, count).ToArray());
            _offset = 0;
        }

        /// <summary>
        /// 测试相关入口 Seek；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        /// <summary>
        /// 测试相关入口 SetLength；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}

