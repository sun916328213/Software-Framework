using Device.Core.Devices;
using Device.Core.Scanners;
using Xunit;

namespace Device.Core.Tests;

/// <summary>
/// 验证扫码器公共配置约束。
/// </summary>
public sealed class ScannerContractTests
{
    /// <summary>无效串口配置应一次返回全部问题。</summary>
    [Fact]
    public void Validate_InvalidConfiguration_ReturnsAllErrors()
    {
        var options = new ScannerOptions
        {
            Name = "",
            DriverId = default,
            PortName = "USB1",
            BaudRate = 0,
            DataBits = 4,
            StopBits = 3,
            Parity = "invalid",
            Delimiter = "",
            MaximumFrameBytes = 0,
            TimeoutMilliseconds = 0,
        };

        var exception = Assert.Throws<ScannerConfigurationException>(() =>
            ScannerOptionsValidator.Validate(options));

        Assert.Equal(10, exception.Errors.Count);
    }

    /// <summary>串口名称和校验方式应忽略大小写。</summary>
    [Fact]
    public void Validate_CaseInsensitiveSerialValues_AreAccepted()
    {
        ScannerOptionsValidator.Validate(new ScannerOptions
        {
            Name = "Scanner01",
            DriverId = new DeviceDriverId("scanner.fake.serial"),
            PortName = "com3",
            Parity = "NONE",
        });
    }
}
