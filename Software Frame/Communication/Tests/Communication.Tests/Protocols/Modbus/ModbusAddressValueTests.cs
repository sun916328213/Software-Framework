// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ModbusAddressValueTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Runtime.Protocols.Modbus、Xunit。导入不代表所有类型都会在运行时加载。
using Communication.Runtime.Protocols.Modbus;
using Xunit;

namespace Communication.Tests.Protocols.Modbus;

/// <summary>
/// ModbusAddressValueTests 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class ModbusAddressValueTests
{
    /// <summary>
    /// 测试相关入口 Supported_address_formats_map_to_a_zero_based_value；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Parse、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Theory]
    [InlineData("40001", ModbusRegion.HoldingRegister, 0)]
    [InlineData("HR10", ModbusRegion.HoldingRegister, 9)]
    [InlineData("30001", ModbusRegion.InputRegister, 0)]
    [InlineData("DI2", ModbusRegion.DiscreteInput, 1)]
    [InlineData("C1", ModbusRegion.Coil, 0)]
    public void Supported_address_formats_map_to_a_zero_based_value(
        string text,
        ModbusRegion region,
        ushort startAddress)
    {
        var address = ModbusAddressValue.Parse(text);

        Assert.Equal(region, address.Region);
        Assert.Equal(startAddress, address.StartAddress);
    }

    /// <summary>
    /// 测试相关入口 Invalid_addresses_are_rejected_instead_of_reading_register_zero；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Throws、Parse；具体条件和先后次序见方法体。
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("banana")]
    [InlineData("40000")]
    public void Invalid_addresses_are_rejected_instead_of_reading_register_zero(string text)
    {
        Assert.Throws<FormatException>(() => ModbusAddressValue.Parse(text));
    }

    /// <summary>
    /// 测试相关入口 Parser_preserves_the_typed_address_and_explicit_element_quantity；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 ParseValue、Equal、Parse；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Parser_preserves_the_typed_address_and_explicit_element_quantity()
    {
        var (address, quantity) = ModbusAddress.ParseValue("HR10", length: 2);

        Assert.Equal(ModbusAddressValue.Parse("HR10"), address);
        Assert.Equal((ushort)2, quantity);
    }
}
