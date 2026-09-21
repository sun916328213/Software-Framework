using Device.Core.Devices;
using Device.Core.Printers;
using Xunit;

namespace Device.Core.Tests;

/// <summary>
/// 验证网络打印机公共配置约束。
/// </summary>
public sealed class PrinterContractTests
{
    /// <summary>无效网络配置应一次返回全部问题。</summary>
    [Fact]
    public void Validate_InvalidConfiguration_ReturnsAllErrors()
    {
        var options = new PrinterOptions
        {
            Name = "",
            DriverId = default,
            IpAddress = "printer.local",
            Port = 0,
            MaximumReadTimeoutMilliseconds = 0,
            TimeToWaitForMoreDataMilliseconds = 0,
        };

        var exception = Assert.Throws<PrinterConfigurationException>(() =>
            PrinterOptionsValidator.Validate(options));

        Assert.Equal(6, exception.Errors.Count);
    }

    /// <summary>后续数据等待时间不能超过整个读取的最大等待时间。</summary>
    [Fact]
    public void Validate_MoreDataWaitExceedsReadTimeout_ReturnsError()
    {
        var options = ValidOptions() with
        {
            MaximumReadTimeoutMilliseconds = 100,
            TimeToWaitForMoreDataMilliseconds = 101,
        };

        var exception = Assert.Throws<PrinterConfigurationException>(() =>
            PrinterOptionsValidator.Validate(options));

        Assert.Single(exception.Errors);
    }

    /// <summary>合法 IPv4 和 IPv6 地址都应通过公共校验。</summary>
    /// <param name="ipAddress">待验证的 IP 地址。</param>
    [Theory]
    [InlineData("192.168.1.100")]
    [InlineData("2001:db8::10")]
    public void Validate_ValidIpAddress_IsAccepted(string ipAddress)
    {
        PrinterOptionsValidator.Validate(ValidOptions() with { IpAddress = ipAddress });
    }

    private static PrinterOptions ValidOptions() => new()
    {
        Name = "Printer01",
        DriverId = new DeviceDriverId("printer.fake.tcp"),
        IpAddress = "192.168.1.100",
    };
}
