using Xunit;

namespace CommunicationNew.MxComponentSmokeTest.Tests;

public sealed class SmokeOptionsTests
{
    [Fact]
    public void Defaults_are_read_only_d100()
    {
        var options = SmokeOptions.Parse([]);
        Assert.Equal(0, options.LogicalStationNumber);
        Assert.Equal("D100", options.Address);
        Assert.Equal(1, options.Quantity);
        Assert.False(options.HasWrite);
    }

    [Fact]
    public void Write_is_rejected_without_explicit_confirmation()
    {
        var error = Assert.Throws<ArgumentException>(() =>
            SmokeOptions.Parse(["--address", "D100", "--write-words", "0x1234"]));
        Assert.Contains("--confirm-write", error.Message);
    }

    [Theory]
    [InlineData("D100", "--write-bits", "1")]
    [InlineData("M100", "--write-words", "1")]
    public void Write_data_must_match_the_address_semantics(
        string address,
        string writeOption,
        string value)
    {
        Assert.Throws<ArgumentException>(() => SmokeOptions.Parse(
            ["--address", address, writeOption, value, "--confirm-write"]));
    }

    [Fact]
    public void Word_values_support_decimal_and_hex_and_create_big_endian_legacy_payload()
    {
        var options = SmokeOptions.Parse(
            ["--address", "D100", "--write-words", "0x1234,5678", "--confirm-write"]);
        Assert.Equal(2, options.Quantity);
        Assert.Equal(new byte[] { 0x12, 0x34, 0x16, 0x2E }, options.CreateWritePayload());
    }

    [Fact]
    public void Bit_values_create_one_byte_per_point_for_the_legacy_interface()
    {
        var options = SmokeOptions.Parse(
            ["--address", "M100", "--write-bits", "1,0,1", "--confirm-write"]);
        Assert.Equal(3, options.Quantity);
        Assert.Equal(new byte[] { 1, 0, 1 }, options.CreateWritePayload());
    }

    [Fact]
    public void Password_command_line_argument_is_not_supported()
    {
        Assert.Throws<ArgumentException>(() => SmokeOptions.Parse(["--password", "secret"]));
    }

    [Fact]
    public void Word_and_bit_write_options_cannot_be_combined()
    {
        Assert.Throws<ArgumentException>(() => SmokeOptions.Parse(
            ["--write-words", "1", "--write-bits", "1", "--confirm-write"]));
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("1024")]
    public void Logical_station_must_be_in_the_mx_range(string station)
    {
        Assert.Throws<ArgumentException>(() => SmokeOptions.Parse(["--station", station]));
    }

    [Theory]
    [InlineData("D100", "961")]
    [InlineData("M100", "7169")]
    public void Quantity_must_respect_the_mc_batch_limit(string address, string count)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            SmokeOptions.Parse(["--address", address, "--count", count]));
    }

    [Fact]
    public void Requested_range_must_stay_within_the_24_bit_device_number()
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            SmokeOptions.Parse(["--address", "D16777215", "--count", "2"]));
    }

    [Fact]
    public void Explicit_count_cannot_be_combined_with_write_values()
    {
        Assert.Throws<ArgumentException>(() => SmokeOptions.Parse(
            ["--address", "D100", "--count", "2", "--write-words", "1", "--confirm-write"]));
    }

    [Fact]
    public void Write_confirmation_requires_write_values()
    {
        Assert.Throws<ArgumentException>(() => SmokeOptions.Parse(["--confirm-write"]));
    }
}
