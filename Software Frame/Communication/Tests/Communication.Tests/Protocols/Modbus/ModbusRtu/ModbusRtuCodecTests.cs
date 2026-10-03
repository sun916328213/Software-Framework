// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ModbusRtuCodecTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Buffers、Communication.Runtime.Protocols.Modbus、Xunit。导入不代表所有类型都会在运行时加载。
using System.Buffers;
using Communication.Runtime.Protocols.Modbus;
using Xunit;

namespace Communication.Tests.Protocols.Modbus;

/// <summary>
/// ModbusRtuCodecTests 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class ModbusRtuCodecTests
{
    /// <summary>
    /// 验证 Read_request_matches_known_modbus_rtu_frame 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Parse、Read、Encode、Equal、ToArray；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 Write_request_preserves_the_typed_modbus_address 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Parse、Write、Equal；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 Decoder_waits_for_complete_frame_and_validates_crc 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 False、TryDecode、Equal、True、ToArray；具体条件和先后次序见方法体。
    /// </summary>
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
