// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：MxComponentBitCodecTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core.Protocols.Mitsubishi、Communication.Runtime.Protocols.Mitsubishi、Xunit。导入不代表所有类型都会在运行时加载。
using Communication.Core.Protocols.Mitsubishi;
using Communication.Runtime.Protocols.Mitsubishi;
using Xunit;

namespace Communication.Tests.Protocols.Mitsubishi;

/// <summary>
/// MxComponentBitCodecTests 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class MxComponentBitCodecTests
{
    /// <summary>
    /// 测试相关入口 Read_window_aligns_an_arbitrary_start_to_a_sixteen_point_word；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 CreateReadWindow、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Read_window_aligns_an_arbitrary_start_to_a_sixteen_point_word()
    {
        var window = MxComponentBitCodec.CreateReadWindow(start: 17, quantity: 17);

        Assert.Equal(16, window.AlignedStart);
        Assert.Equal(1, window.BitOffset);
        Assert.Equal(2, window.WordCount);
    }

    /// <summary>
    /// 测试相关入口 Read_words_are_packed_using_the_mc_bit_nibble_format；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 PackReadWords、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Read_words_are_packed_using_the_mc_bit_nibble_format()
    {
        // 从字内 bit 1 开始依次读取 1、0、1、1。
        var words = new short[] { 0b0000_0000_0001_1010 };

        var payload = MxComponentBitCodec.PackReadWords(words, bitOffset: 1, quantity: 4);

        Assert.Equal(new byte[] { 0x10, 0x11 }, payload);
    }

    /// <summary>
    /// 测试相关入口 Packed_write_payload_is_decoded_one_point_at_a_time；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Equal、GetWriteValue；具体条件和先后次序见方法体。
    /// </summary>
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    [InlineData(3, 1)]
    public void Packed_write_payload_is_decoded_one_point_at_a_time(int index, short expected)
    {
        var payload = new byte[] { 0x10, 0x11 };

        Assert.Equal(expected, MxComponentBitCodec.GetWriteValue(payload, index));
    }
}
