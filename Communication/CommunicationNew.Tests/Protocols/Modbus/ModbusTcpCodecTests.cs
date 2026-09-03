using System.Buffers;
using CommunicationNew.Protocols.Modbus;
using Xunit;

namespace CommunicationNew.Tests.Protocols.Modbus;

public sealed class ModbusTcpCodecTests
{
    [Fact]
    public void Read_holding_register_request_matches_the_modbus_tcp_wire_format()
    {
        var codec = new ModbusTcpCodec();
        var request = ModbusTcpRequest.Read(
            unitId: 1,
            ModbusRegion.HoldingRegister,
            startAddress: 0,
            quantity: 2);

        var frame = codec.Encode(request, correlationId: 0x1234);

        Assert.Equal(
            new byte[] { 0x12, 0x34, 0, 0, 0, 6, 1, 3, 0, 0, 0, 2 },
            frame.ToArray());
    }

    [Fact]
    public void Split_response_waits_for_the_complete_frame_and_returns_register_bytes()
    {
        var codec = new ModbusTcpCodec();
        var bytes = new byte[]
        {
            0x12, 0x34, 0, 0, 0, 7, 1,
            3, 4, 0, 0x2A, 0, 0x2B,
        };

        var partial = new ReadOnlySequence<byte>(bytes.AsMemory(0, 8));
        Assert.False(codec.TryDecode(partial, 0x1234, out _, out _));

        var complete = new ReadOnlySequence<byte>(bytes);
        Assert.True(codec.TryDecode(complete, 0x1234, out var response, out var consumed));
        Assert.Equal(complete.End, consumed);
        Assert.Equal((byte)1, response.UnitId);
        Assert.Equal((byte)3, response.FunctionCode);
        Assert.Equal(new byte[] { 0, 0x2A, 0, 0x2B }, response.Data.ToArray());
    }
}
