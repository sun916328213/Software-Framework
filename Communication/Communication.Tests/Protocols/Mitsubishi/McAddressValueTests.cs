using CommunicationNew.Protocols.Mitsubishi;
using Xunit;

namespace CommunicationNew.Tests.Protocols.Mitsubishi;

public sealed class McAddressValueTests
{
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

    [Fact]
    public void Address_offset_rejects_a_number_beyond_the_protocol_range()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            McAddressValue.Parse("D16777215").Offset(1));
    }

    [Fact]
    public void Mc_request_rejects_an_uninitialized_address()
    {
        Assert.Throws<ArgumentException>(() => McRequest.Read(default, 1));
    }

    [Fact]
    public void Mc_request_rejects_a_range_beyond_the_24_bit_device_number()
    {
        var lastDevice = McAddressValue.Parse("D16777215");

        Assert.Throws<ArgumentOutOfRangeException>(() => McRequest.Read(lastDevice, 2));
    }

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

    [Fact]
    public void Public_batch_command_metadata_matches_the_codec_commands()
    {
        Assert.Equal((ushort)0x0401, McCommand.BatchRead);
        Assert.Equal((ushort)0x1401, McCommand.BatchWrite);
        Assert.Equal((ushort)0x0000, McCommand.WordSubcommand);
        Assert.Equal((ushort)0x0001, McCommand.BitSubcommand);
    }

    private static void AssertAddress(string text, byte deviceCode, bool isBit)
    {
        var address = McAddressValue.Parse(text);

        Assert.Equal(text[..^2], address.Prefix);
        Assert.Equal(10, address.Number);
        Assert.Equal(deviceCode, address.DeviceCode);
        Assert.Equal(isBit, address.IsBit);
    }
}
