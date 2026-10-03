// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ModbusServerDataCodecTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Runtime.Protocols.Modbus、Xunit。导入不代表所有类型都会在运行时加载。
using Communication.Runtime.Protocols.Modbus;
using Xunit;

namespace Communication.Tests.Protocols.Modbus;

/// <summary>
/// ModbusServerDataCodecTests 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class ModbusServerDataCodecTests
{
    /// <summary>
    /// 验证 Unaligned_bit_range_is_repacked_from_bit_zero 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Equal、ReadBits；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Unaligned_bit_range_is_repacked_from_bit_zero()
    {
        var storage = new byte[] { 0xAC, 0x03 };

        Assert.Equal(
            new byte[] { 0x75 },
            ModbusServerDataCodec.ReadBits(storage, startBit: 3, quantity: 7));
    }

    /// <summary>
    /// 验证 Unaligned_bit_write_preserves_bits_outside_the_target_range 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 WriteBits、Equal；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 Bit_window_rejects_a_range_beyond_the_storage 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Throws、ReadBits、WriteBits；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 Register_window_preserves_the_raw_big_endian_bytes 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Equal、ReadRegisters；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Register_window_preserves_the_raw_big_endian_bytes()
    {
        var storage = new byte[] { 0xAA, 0xBB, 0x12, 0x34, 0xFE, 0xDC };

        Assert.Equal(
            new byte[] { 0x12, 0x34, 0xFE, 0xDC },
            ModbusServerDataCodec.ReadRegisters(storage, startRegister: 1, quantity: 2));
    }

    /// <summary>
    /// 验证 Complete_big_endian_registers_are_written_without_host_endian_conversion 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 WriteRegisters、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Complete_big_endian_registers_are_written_without_host_endian_conversion()
    {
        var storage = new byte[6];

        ModbusServerDataCodec.WriteRegisters(
            storage, startRegister: 1, new byte[] { 0x12, 0x34, 0xFE, 0xDC });

        Assert.Equal(new byte[] { 0x00, 0x00, 0x12, 0x34, 0xFE, 0xDC }, storage);
    }

    /// <summary>
    /// 验证 Final_unpaired_byte_is_written_as_the_high_byte 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 WriteRegisters、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Final_unpaired_byte_is_written_as_the_high_byte()
    {
        var storage = new byte[4];

        ModbusServerDataCodec.WriteRegisters(
            storage, startRegister: 0, new byte[] { 0x12, 0x34, 0x56 });

        Assert.Equal(new byte[] { 0x12, 0x34, 0x56, 0x00 }, storage);
    }
}
