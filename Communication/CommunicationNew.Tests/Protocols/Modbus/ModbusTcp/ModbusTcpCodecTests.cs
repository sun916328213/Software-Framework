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
        var address = ModbusAddressValue.Parse("HR1");
        var request = ModbusTcpRequest.Read(
            unitId: 1,
            address,
            quantity: 2);

        var frame = codec.Encode(request, correlationId: 0x1234);

        Assert.Equal(address, request.Address);
        Assert.Equal(
            new byte[] { 0x12, 0x34, 0, 0, 0, 6, 1, 3, 0, 0, 0, 2 },
            frame.ToArray());
    }

    [Fact]
    public void Write_request_preserves_the_typed_modbus_address()
    {
        var address = ModbusAddressValue.Parse("HR10");

        var request = ModbusTcpRequest.Write(
            unitId: 1,
            address,
            quantity: 1,
            payload: new byte[] { 0, 42 });

        Assert.Equal(address, request.Address);
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

    [Fact]
    public void Every_segment_boundary_preserves_two_coalesced_responses()
    {
        var codec = new ModbusTcpCodec();
        var bytes = new byte[]
        {
            0x12, 0x34, 0, 0, 0, 7, 1, 3, 4, 0, 0x2A, 0, 0x2B,
            0x12, 0x35, 0, 0, 0, 5, 1, 3, 2, 0, 0x2C,
        };

        // 穷举每个切分位置，覆盖 MBAP、PDU 和两帧交界处的分段读取。
        for (var split = 1; split < bytes.Length; split++)
        {
            var sequence = CreateSegmentedSequence(bytes, split);

            Assert.True(codec.TryDecode(
                sequence,
                0x1234,
                out var first,
                out var firstConsumed));
            var remaining = sequence.Slice(firstConsumed);
            Assert.Equal(new byte[] { 0, 0x2A, 0, 0x2B }, first.Data.ToArray());

            Assert.True(codec.TryDecode(
                remaining,
                0x1235,
                out var second,
                out var secondConsumed));
            Assert.Equal(new byte[] { 0, 0x2C }, second.Data.ToArray());
            Assert.Equal(remaining.End, secondConsumed);
        }
    }

    private static ReadOnlySequence<byte> CreateSegmentedSequence(
        byte[] bytes,
        int split)
    {
        var first = new BufferSegment(bytes.AsMemory(0, split));
        var last = first.Append(bytes.AsMemory(split));
        return new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
    }

    private sealed class BufferSegment : ReadOnlySequenceSegment<byte>
    {
        public BufferSegment(ReadOnlyMemory<byte> memory)
        {
            Memory = memory;
        }

        public BufferSegment Append(ReadOnlyMemory<byte> memory)
        {
            var segment = new BufferSegment(memory)
            {
                RunningIndex = RunningIndex + Memory.Length,
            };
            Next = segment;
            return segment;
        }
    }
}
