// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：LabelPrintEngineContractTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Core.Devices、Device.Core.LabelPrinting、Xunit。导入不代表所有类型都会在运行时加载。
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
