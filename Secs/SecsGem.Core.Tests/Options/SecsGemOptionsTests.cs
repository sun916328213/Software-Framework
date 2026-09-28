using SecsGem.Core;
using Xunit;

namespace SecsGem.Core.Tests.Options;

public sealed class SecsGemOptionsTests
{
    [Fact]
    public void Default_Hsms_Options_Are_Valid()
    {
        new SecsGemOptions().Validate();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public void Port_Outside_Tcp_Range_Is_Rejected(int port)
    {
        var options = new SecsGemOptions { Port = port };

        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(32768)]
    public void Device_Id_Outside_Hsms_Range_Is_Rejected(int deviceId)
    {
        var options = new SecsGemOptions { DeviceId = deviceId };

        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    [Fact]
    public void Empty_Address_Is_Rejected()
    {
        var options = new SecsGemOptions { IpAddress = " " };

        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    [Fact]
    public void Nonpositive_Buffer_Size_Is_Rejected()
    {
        var options = new SecsGemOptions { BufferSize = 0 };

        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }
}
