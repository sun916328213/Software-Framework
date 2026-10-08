// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：LabelPrintEngineFactoryTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Core.Devices、Device.Core.LabelPrinting、Device.Runtime.LabelPrinting、Xunit。导入不代表所有类型都会在运行时加载。
using Device.Core.Devices;
using Device.Core.LabelPrinting;
using Device.Runtime.LabelPrinting;
using Xunit;

namespace Device.Tests.Runtime;

/// <summary>
/// 验证统一标签打印软件工厂和无需启动 BarTender 即可检查的行为。
/// </summary>
public sealed class LabelPrintEngineFactoryTests
{
    private const string BarTenderDriverId = "label-print.bartender.sdk";

    /// <summary>工厂只应公开具有真实实现的 BarTender 驱动。</summary>
    [Fact]
    public void AvailableDrivers_ContainsImplementedDriverOnly()
    {
        var descriptor = Assert.Single(new LabelPrintEngineFactory().AvailableDrivers);

        Assert.Equal(BarTenderDriverId, descriptor.Id.Value);
        Assert.Equal(DeviceCategory.LabelPrintEngine, descriptor.Category);
    }

    /// <summary>创建适配器不应启动 BarTender 或枚举系统打印机。</summary>
    [Fact]
    public async Task Create_KnownDriver_DoesNotStartVendorEngine()
    {
        var options = ValidOptions();

        await using var engine = new LabelPrintEngineFactory().Create(options);

        Assert.Equal(options, engine.Options);
        Assert.Null(engine.TemplatePath);
    }

    /// <summary>没有模板时读取字段应返回稳定错误码。</summary>
    [Fact]
    public async Task GetFields_WhenTemplateNotOpen_ReturnsFailure()
    {
        await using var engine = new LabelPrintEngineFactory().Create(ValidOptions());

        var result = await engine.GetFieldsAsync();

        Assert.False(result.Success);
        Assert.Equal("label-print.bartender.template_not_open", result.ErrorCode);
    }

    /// <summary>模板格式错误时应在启动 BarTender 前拒绝请求。</summary>
    [Fact]
    public async Task OpenTemplate_WhenExtensionInvalid_ReturnsFailure()
    {
        await using var engine = new LabelPrintEngineFactory().Create(ValidOptions());

        var result = await engine.OpenTemplateAsync("Label.txt");

        Assert.False(result.Success);
        Assert.Equal("label-print.bartender.template_format_invalid", result.ErrorCode);
    }

    /// <summary>不存在的 BTW 文件应在启动 BarTender 前返回明确错误。</summary>
    [Fact]
    public async Task OpenTemplate_WhenFileDoesNotExist_ReturnsFailure()
    {
        await using var engine = new LabelPrintEngineFactory().Create(ValidOptions());

        var result = await engine.OpenTemplateAsync("missing-label.btw");

        Assert.False(result.Success);
        Assert.Equal("label-print.bartender.template_not_found", result.ErrorCode);
    }

    /// <summary>未知驱动应被拒绝并报告当前可选值。</summary>
    [Fact]
    public void Create_UnknownDriver_ReportsInstalledDriver()
    {
        var options = ValidOptions() with
        {
            DriverId = new DeviceDriverId("label-print.unknown"),
        };

        var exception = Assert.Throws<LabelPrintEngineDriverNotFoundException>(() =>
            new LabelPrintEngineFactory().Create(options));

        Assert.Contains(BarTenderDriverId, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 测试相关入口 ValidOptions；准备条件、协作调用与结果检查以方法正文为准。
    /// </summary>
    private static LabelPrintEngineOptions ValidOptions() => new()
    {
        Name = "LabelEngine01",
        DriverId = new DeviceDriverId(BarTenderDriverId),
        DefaultPrinterName = "Zebra ZT411",
    };
}
