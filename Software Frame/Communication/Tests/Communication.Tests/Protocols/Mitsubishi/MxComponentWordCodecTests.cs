// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：MxComponentWordCodecTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core.Protocols.Mitsubishi、Communication.Runtime.Protocols.Mitsubishi、Xunit。导入不代表所有类型都会在运行时加载。
using Communication.Core.Protocols.Mitsubishi;
using Communication.Runtime.Protocols.Mitsubishi;
using Xunit;

namespace Communication.Tests.Protocols.Mitsubishi;

/// <summary>
/// MxComponentWordCodecTests 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class MxComponentWordCodecTests
{
    /// <summary>
    /// 测试相关入口 Mx_words_are_encoded_as_explicit_little_endian_mc_payload；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Equal、ToPayload；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Mx_words_are_encoded_as_explicit_little_endian_mc_payload()
    {
        var words = new short[] { 0x1234, unchecked((short)0xFEDC) };

        Assert.Equal(
            new byte[] { 0x34, 0x12, 0xDC, 0xFE },
            MxComponentWordCodec.ToPayload(words));
    }

    /// <summary>
    /// 测试相关入口 Mc_payload_is_decoded_to_signed_mx_words_without_using_host_endianness；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Equal、FromPayload；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Mc_payload_is_decoded_to_signed_mx_words_without_using_host_endianness()
    {
        var payload = new byte[] { 0x34, 0x12, 0xDC, 0xFE };

        Assert.Equal(
            new short[] { 0x1234, unchecked((short)0xFEDC) },
            MxComponentWordCodec.FromPayload(payload));
    }

    /// <summary>
    /// 测试相关入口 Odd_length_word_payload_is_rejected；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Throws、FromPayload；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Odd_length_word_payload_is_rejected()
    {
        Assert.Throws<ArgumentException>(() =>
            MxComponentWordCodec.FromPayload(new byte[] { 0x34 }));
    }
}
