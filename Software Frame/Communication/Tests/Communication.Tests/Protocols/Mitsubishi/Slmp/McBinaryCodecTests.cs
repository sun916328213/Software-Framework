// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：McBinaryCodecTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Buffers、Communication.Core.Protocols.Mitsubishi、Communication.Runtime.Protocols.Mitsubishi、Xunit。导入不代表所有类型都会在运行时加载。
using System.Buffers;
using Communication.Core.Protocols.Mitsubishi;
using Communication.Runtime.Protocols.Mitsubishi;
using Xunit;

namespace Communication.Tests.Protocols.Mitsubishi;

/// <summary>
/// McBinaryCodecTests 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class McBinaryCodecTests
{
    /// <summary>
    /// 验证 Three_e_word_read_matches_official_slmp_field_order 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Read、Parse、Encode、Equal、ToArray；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Three_e_word_read_matches_official_slmp_field_order()
    {
        var request = McRequest.Read(McAddressValue.Parse("D100"), quantity: 2);
        var codec = new McBinaryCodec(new McCodecOptions
        {
            FrameFormat = McFrameFormat.ThreeE,
            NetworkNumber = 0,
            StationNumber = 0xFF,
            ModuleIoNumber = 0x03FF,
            ModuleStationNumber = 0,
            MonitoringTimer = 0x0010,
        });

        var frame = codec.Encode(request, correlationId: 0x1234);

        Assert.Equal(new byte[]
        {
            0x50, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00,
            0x0C, 0x00, 0x10, 0x00,
            0x01, 0x04, 0x00, 0x00,
            0x64, 0x00, 0x00, 0xA8, 0x02, 0x00,
        }, frame.ToArray());
    }

    /// <summary>
    /// 验证 Decoder_waits_for_complete_three_e_response_and_checks_end_code 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 False、TryDecode、Equal、True、ToArray；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Decoder_waits_for_complete_three_e_response_and_checks_end_code()
    {
        var codec = new McBinaryCodec(new McCodecOptions
        {
            FrameFormat = McFrameFormat.ThreeE,
            NetworkNumber = 0,
            StationNumber = 0xFF,
            ModuleIoNumber = 0x03FF,
        });
        var partial = new ReadOnlySequence<byte>(new byte[]
        {
            0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x06,
        });
        Assert.False(codec.TryDecode(partial, 0, out _, out var partialConsumed));
        Assert.Equal(partial.Start, partialConsumed);

        var complete = new ReadOnlySequence<byte>(new byte[]
        {
            0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00,
            0x06, 0x00, 0x00, 0x00, 0x34, 0x12, 0x78, 0x56,
        });
        Assert.True(codec.TryDecode(complete, 0, out var response, out var consumed));
        Assert.Equal(complete.End, consumed);
        Assert.Equal(new byte[] { 0x34, 0x12, 0x78, 0x56 }, response.Data.ToArray());

        var error = new ReadOnlySequence<byte>(new byte[]
        {
            0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00,
            0x02, 0x00, 0x59, 0xC0,
        });
        var exception = Assert.Throws<McProtocolException>(() =>
        {
            codec.TryDecode(error, 0, out _, out _);
        });
        Assert.Equal((ushort)0xC059, exception.EndCode);
    }

    /// <summary>
    /// 验证 Address_parser_prefers_multi_letter_devices_and_uses_device_radix 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Parse、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Address_parser_prefers_multi_letter_devices_and_uses_device_radix()
    {
        var specialRelay = McAddressValue.Parse("SM100");
        var linkRelay = McAddressValue.Parse("B10");

        Assert.Equal((byte)0x91, specialRelay.DeviceCode);
        Assert.Equal(100, specialRelay.Number);
        Assert.Equal((byte)0xA0, linkRelay.DeviceCode);
        Assert.Equal(0x10, linkRelay.Number);
    }

    /// <summary>
    /// 验证 Four_e_frame_carries_and_validates_request_serial_number 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Read、Parse、ToArray、Encode、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Four_e_frame_carries_and_validates_request_serial_number()
    {
        var codec = new McBinaryCodec(new McCodecOptions
        {
            FrameFormat = McFrameFormat.FourE,
            NetworkNumber = 0,
            StationNumber = 0xFF,
            ModuleIoNumber = 0x03FF,
        });
        var request = McRequest.Read(McAddressValue.Parse("D100"), 1);

        var encoded = codec.Encode(request, correlationId: 0x1234).ToArray();
        Assert.Equal(
            new byte[] { 0x54, 0x00, 0x34, 0x12, 0x00, 0x00 },
            encoded[..6]);

        var response = new ReadOnlySequence<byte>(new byte[]
        {
            0xD4, 0x00, 0x34, 0x12, 0x00, 0x00,
            0x00, 0xFF, 0xFF, 0x03, 0x00,
            0x04, 0x00, 0x00, 0x00, 0x2A, 0x00,
        });
        Assert.True(codec.TryDecode(response, 0x1234, out var decoded, out _));
        Assert.Equal(new byte[] { 0x2A, 0x00 }, decoded.Data.ToArray());

        Assert.Throws<McProtocolException>(() =>
        {
            codec.TryDecode(response, 0x1235, out _, out _);
        });
    }
}
