using Device.Core.Devices;
using Device.Core.LabelPrinting;
using Xunit;

namespace Device.Tests.Core;

/// <summary>
/// 验证标签打印软件公共配置约束。
/// </summary>
public sealed class LabelPrintEngineContractTests
{
    /// <summary>无效配置应一次返回全部问题。</summary>
    [Fact]
    public void Validate_InvalidConfiguration_ReturnsAllErrors()
    {
        var options = new LabelPrintEngineOptions
        {
            Name = "",
            DriverId = default,
            PrintTimeoutMilliseconds = 0,
            PreviewDpi = 71,
        };

        var exception = Assert.Throws<LabelPrintEngineConfigurationException>(() =>
            LabelPrintEngineOptionsValidator.Validate(options));

        Assert.Equal(4, exception.Errors.Count);
    }

    /// <summary>合理的打印超时和预览分辨率应通过校验。</summary>
    [Fact]
    public void Validate_ValidConfiguration_IsAccepted()
    {
        LabelPrintEngineOptionsValidator.Validate(new LabelPrintEngineOptions
        {
            Name = "LabelEngine01",
            DriverId = new DeviceDriverId("label-print.test"),
            PrintTimeoutMilliseconds = 30_000,
            PreviewDpi = 600,
        });
    }
}
