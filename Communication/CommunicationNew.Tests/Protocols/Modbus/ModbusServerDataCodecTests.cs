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
    public void Registers_are_encoded_as_big_endian_without_changing_the_bit_pattern()
    {
        var registers = new short[] { 0x1234, unchecked((short)0xFEDC) };

        Assert.Equal(
            new byte[] { 0x12, 0x34, 0xFE, 0xDC },
            ModbusServerDataCodec.EncodeRegisters(registers));
    }

    [Fact]
    public void Complete_big_endian_registers_are_decoded()
    {
        Assert.Equal(
            new ushort[] { 0x1234, 0xFEDC },
            ModbusServerDataCodec.DecodeRegisters(new byte[] { 0x12, 0x34, 0xFE, 0xDC }));
    }

    [Fact]
    public void Final_unpaired_byte_is_preserved_as_the_high_byte()
    {
        Assert.Equal(
            new ushort[] { 0x1234, 0x5600 },
            ModbusServerDataCodec.DecodeRegisters(new byte[] { 0x12, 0x34, 0x56 }));
    }
}
