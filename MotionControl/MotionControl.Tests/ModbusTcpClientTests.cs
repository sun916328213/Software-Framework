using MotionControl.Runtime.Vendors.Inovance;
using Xunit;

namespace MotionControl.Tests;

public sealed class ModbusTcpClientTests
{
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

        public override void Flush() { }

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

        public override void Write(byte[] buffer, int offset, int count)
        {
            _response = respond(buffer.AsSpan(offset, count).ToArray());
            _offset = 0;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}

