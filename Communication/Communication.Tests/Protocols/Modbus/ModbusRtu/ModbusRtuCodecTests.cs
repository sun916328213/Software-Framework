using System.Buffers;
using CommunicationNew.Protocols.Modbus;
using Xunit;

namespace CommunicationNew.Tests.Protocols.Modbus;

public sealed class ModbusRtuCodecTests
{
    [Fact]
    public void Read_request_matches_known_modbus_rtu_frame()
    {
        var address = ModbusAddressValue.Parse("HR1");
        var request = ModbusRtuRequest.Read(
            1,
            address,
            quantity: 10);

        var frame = new ModbusRtuCodec().Encode(request, correlationId: 0);

        Assert.Equal(address, request.Address);
        Assert.Equal(
            new byte[] { 0x01, 0x03, 0x00, 0x00, 0x00, 0x0A, 0xC5, 0xCD },
            frame.ToArray());
    }

    [Fact]
    public void Write_request_preserves_the_typed_modbus_address()
    {
        var address = ModbusAddressValue.Parse("HR10");

        var request = ModbusRtuRequest.Write(
            unitId: 1,
            address,
            quantity: 1,
            payload: new byte[] { 0, 42 });

        Assert.Equal(address, request.Address);
    }

    [Fact]
    public void Decoder_waits_for_complete_frame_and_validates_crc()
    {
        var codec = new ModbusRtuCodec();
        var partial = new ReadOnlySequence<byte>(
            new byte[] { 0x01, 0x03, 0x02, 0x00 });

        Assert.False(codec.TryDecode(
            partial,
            correlationId: 0,
            out _,
            out var partialConsumed));
        Assert.Equal(partial.Start, partialConsumed);

        var complete = new ReadOnlySequence<byte>(
            new byte[] { 0x01, 0x03, 0x02, 0x00, 0x2A, 0x39, 0x9B });
        Assert.True(codec.TryDecode(
            complete,
            correlationId: 0,
            out var response,
            out var consumed));
        Assert.Equal(complete.End, consumed);
        Assert.Equal((byte)1, response.UnitId);
        Assert.Equal((byte)3, response.FunctionCode);
        Assert.Equal(new byte[] { 0x00, 0x2A }, response.Data.ToArray());

        var corrupt = new ReadOnlySequence<byte>(
            new byte[] { 0x01, 0x03, 0x02, 0x00, 0x2A, 0x39, 0x00 });
        Assert.Throws<ModbusProtocolException>(() =>
        {
            codec.TryDecode(corrupt, 0, out _, out _);
        });
    }
}
