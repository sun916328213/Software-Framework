using Device.Core.Devices;
using Device.Core.Rfid;
using Xunit;

namespace Device.Core.Tests;

/// <summary>
/// 验证 RFID 公共配置约束。
/// </summary>
public sealed class RfidContractTests
{
    /// <summary>无效串口和分页配置应一次返回全部问题。</summary>
    [Fact]
    public void Validate_InvalidConfiguration_ReturnsAllErrors()
    {
        var options = new RfidOptions
        {
            Name = "",
            DriverId = default,
            PortName = "USB1",
            BaudRate = 0,
            DataBits = 4,
            StopBits = 3,
            Parity = "invalid",
            SlaveId = 0,
            PageSizeBytes = 16,
            TimeoutMilliseconds = 0,
        };

        var exception = Assert.Throws<RfidConfigurationException>(() =>
            RfidOptionsValidator.Validate(options));

        Assert.Equal(10, exception.Errors.Count);
    }

    /// <summary>串口名为空时应直接提示名称缺失，而不是报告串口格式错误。</summary>
    [Fact]
    public void Validate_BlankPortName_ReportsMissingPortName()
    {
        var exception = Assert.Throws<RfidConfigurationException>(() =>
            RfidOptionsValidator.Validate(new RfidOptions
            {
                Name = "Rfid01",
                DriverId = new DeviceDriverId("rfid.fake.serial"),
                PortName = "   ",
            }));

        var error = Assert.Single(exception.Errors);
        Assert.Contains("串口名称不能为空", error, StringComparison.Ordinal);
    }

    /// <summary>串口名称和校验方式应忽略大小写。</summary>
    [Fact]
    public void Validate_CaseInsensitiveSerialValues_AreAccepted()
    {
        RfidOptionsValidator.Validate(new RfidOptions
        {
            Name = "Rfid01",
            DriverId = new DeviceDriverId("rfid.fake.serial"),
            PortName = "com3",
            Parity = "NONE",
        });
    }
}
