// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：McAddressValueTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core.Protocols.Mitsubishi、Communication.Runtime.Protocols.Mitsubishi、Xunit。导入不代表所有类型都会在运行时加载。
using Communication.Core.Protocols.Mitsubishi;
using Communication.Runtime.Protocols.Mitsubishi;
using Xunit;

namespace Communication.Tests.Protocols.Mitsubishi;

/// <summary>
/// McAddressValueTests 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class McAddressValueTests
{
    /// <summary>
    /// 测试相关入口 Timer_and_counter_addresses_preserve_contact_coil_and_value_semantics；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 AssertAddress；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Timer_and_counter_addresses_preserve_contact_coil_and_value_semantics()
    {
        AssertAddress("TS10", 0xC1, isBit: true);
        AssertAddress("TC10", 0xC0, isBit: true);
        AssertAddress("TN10", 0xC2, isBit: false);
        AssertAddress("CS10", 0xC4, isBit: true);
        AssertAddress("CC10", 0xC3, isBit: true);
        AssertAddress("CN10", 0xC5, isBit: false);
    }

    /// <summary>
    /// 测试相关入口 Parsed_device_address_round_trips_using_its_declared_radix；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Equal、ToString、Parse；具体条件和先后次序见方法体。
    /// </summary>
    [Theory]
    [InlineData("D100")]
    [InlineData("M100")]
    [InlineData("B10")]
    [InlineData("X1A")]
    [InlineData("WFF")]
    public void Parsed_device_address_round_trips_using_its_declared_radix(string text)
    {
        Assert.Equal(text, McAddressValue.Parse(text).ToString());
    }

    /// <summary>
    /// 测试相关入口 Address_offset_preserves_device_type_and_number_radix；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Equal、ToString、Offset、Parse；具体条件和先后次序见方法体。
    /// </summary>
    [Theory]
    [InlineData("D100", 1, "D101")]
    [InlineData("B10", 1, "B11")]
    [InlineData("X1F", 1, "X20")]
    public void Address_offset_preserves_device_type_and_number_radix(
        string address,
        int offset,
        string expected)
    {
        Assert.Equal(expected, McAddressValue.Parse(address).Offset(offset).ToString());
    }

    /// <summary>
    /// 测试相关入口 Address_offset_rejects_a_number_beyond_the_protocol_range；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Throws、Offset、Parse；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Address_offset_rejects_a_number_beyond_the_protocol_range()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            McAddressValue.Parse("D16777215").Offset(1));
    }

    /// <summary>
    /// 测试相关入口 Mc_request_rejects_an_uninitialized_address；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Throws、Read；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Mc_request_rejects_an_uninitialized_address()
    {
        Assert.Throws<ArgumentException>(() => McRequest.Read(default, 1));
    }

    /// <summary>
    /// 测试相关入口 Mc_request_rejects_a_range_beyond_the_24_bit_device_number；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Parse、Throws、Read；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Mc_request_rejects_a_range_beyond_the_24_bit_device_number()
    {
        var lastDevice = McAddressValue.Parse("D16777215");

        Assert.Throws<ArgumentOutOfRangeException>(() => McRequest.Read(lastDevice, 2));
    }

    /// <summary>
    /// 测试相关入口 Public_device_type_matches_the_typed_address_device_code；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Equal、Parse；具体条件和先后次序见方法体。
    /// </summary>
    [Theory]
    [InlineData("SM10", McDeviceType.SM)]
    [InlineData("SD10", McDeviceType.SD)]
    [InlineData("SB10", McDeviceType.SB)]
    [InlineData("SW10", McDeviceType.SW)]
    [InlineData("ZR10", McDeviceType.ZR)]
    [InlineData("TS10", McDeviceType.TS)]
    [InlineData("TC10", McDeviceType.TC)]
    [InlineData("TN10", McDeviceType.TN)]
    [InlineData("CS10", McDeviceType.CS)]
    [InlineData("CC10", McDeviceType.CC)]
    [InlineData("CN10", McDeviceType.CN)]
    [InlineData("M10", McDeviceType.M)]
    [InlineData("L10", McDeviceType.L)]
    [InlineData("F10", McDeviceType.F)]
    [InlineData("V10", McDeviceType.V)]
    [InlineData("B10", McDeviceType.B)]
    [InlineData("S10", McDeviceType.S)]
    [InlineData("X10", McDeviceType.X)]
    [InlineData("Y10", McDeviceType.Y)]
    [InlineData("D10", McDeviceType.D)]
    [InlineData("W10", McDeviceType.W)]
    [InlineData("R10", McDeviceType.R)]
    public void Public_device_type_matches_the_typed_address_device_code(
        string text,
        McDeviceType deviceType)
    {
        Assert.Equal((byte)deviceType, McAddressValue.Parse(text).DeviceCode);
    }

    /// <summary>
    /// 测试相关入口 Public_batch_command_metadata_matches_the_codec_commands；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Public_batch_command_metadata_matches_the_codec_commands()
    {
        Assert.Equal((ushort)0x0401, McCommand.BatchRead);
        Assert.Equal((ushort)0x1401, McCommand.BatchWrite);
        Assert.Equal((ushort)0x0000, McCommand.WordSubcommand);
        Assert.Equal((ushort)0x0001, McCommand.BitSubcommand);
    }

    /// <summary>
    /// 测试相关入口 AssertAddress；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Parse、Equal；具体条件和先后次序见方法体。
    /// </summary>
    private static void AssertAddress(string text, byte deviceCode, bool isBit)
    {
        var address = McAddressValue.Parse(text);

        Assert.Equal(text[..^2], address.Prefix);
        Assert.Equal(10, address.Number);
        Assert.Equal(deviceCode, address.DeviceCode);
        Assert.Equal(isBit, address.IsBit);
    }
}
