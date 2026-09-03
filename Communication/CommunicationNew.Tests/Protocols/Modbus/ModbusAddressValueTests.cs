using CommunicationNew.Protocols.Modbus;
using Xunit;

namespace CommunicationNew.Tests.Protocols.Modbus;

public sealed class ModbusAddressValueTests
{
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

    [Theory]
    [InlineData("")]
    [InlineData("banana")]
    [InlineData("40000")]
    public void Invalid_addresses_are_rejected_instead_of_reading_register_zero(string text)
    {
        Assert.Throws<FormatException>(() => ModbusAddressValue.Parse(text));
    }
}
