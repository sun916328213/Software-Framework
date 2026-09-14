using CommunicationNew.Protocols.Modbus;
using Xunit;

namespace CommunicationNew.Tests.Protocols.Modbus;

public sealed class ModbusServerDataCodecTests
{
    [Fact]
    public void Unaligned_bit_range_is_repacked_from_bit_zero()
    {
        var storage = new byte[] { 0xAC, 0x03 };

        Assert.Equal(
            new byte[] { 0x75 },
            ModbusServerDataCodec.ReadBits(storage, startBit: 3, quantity: 7));
    }

    [Fact]
    public void Unaligned_bit_write_preserves_bits_outside_the_target_range()
    {
        var storage = new byte[] { 0xAA, 0x55 };

        ModbusServerDataCodec.WriteBits(
            storage,
            startBit: 3,
            packedBits: new byte[] { 0x02 });

        Assert.Equal(new byte[] { 0x12, 0x50 }, storage);
    }

    [Fact]
    public void Bit_window_rejects_a_range_beyond_the_storage()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ModbusServerDataCodec.ReadBits(new byte[1], startBit: 7, quantity: 2));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ModbusServerDataCodec.WriteBits(
                new byte[1],
                startBit: 1,
                packedBits: new byte[1]));
    }

    [Fact]
    public void Register_window_preserves_the_raw_big_endian_bytes()
    {
        var storage = new byte[] { 0xAA, 0xBB, 0x12, 0x34, 0xFE, 0xDC };

        Assert.Equal(
            new byte[] { 0x12, 0x34, 0xFE, 0xDC },
            ModbusServerDataCodec.ReadRegisters(storage, startRegister: 1, quantity: 2));
    }

    [Fact]
    public void Complete_big_endian_registers_are_written_without_host_endian_conversion()
    {
        var storage = new byte[6];

        ModbusServerDataCodec.WriteRegisters(
            storage, startRegister: 1, new byte[] { 0x12, 0x34, 0xFE, 0xDC });

        Assert.Equal(new byte[] { 0x00, 0x00, 0x12, 0x34, 0xFE, 0xDC }, storage);
    }

    [Fact]
    public void Final_unpaired_byte_is_written_as_the_high_byte()
    {
        var storage = new byte[4];

        ModbusServerDataCodec.WriteRegisters(
            storage, startRegister: 0, new byte[] { 0x12, 0x34, 0x56 });

        Assert.Equal(new byte[] { 0x12, 0x34, 0x56, 0x00 }, storage);
    }
}
