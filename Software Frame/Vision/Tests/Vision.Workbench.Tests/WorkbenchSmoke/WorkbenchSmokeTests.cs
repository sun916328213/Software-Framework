// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：WorkbenchSmokeTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Workbench、Vision.Workbench.Tests。导入不代表所有类型都会在运行时加载。
using Vision.Workbench.Tests;
// ─────────────────────────────────────────────────────────────────────────────
// P10 —— WorkbenchSmoke（计划 §P10 测试域 WorkbenchSmoke + §P9 验收
// 「Workbench 成为所有新 Tool 的标准研发/验收入口」）。
//
// 只装配 VM（TestBrowserViewModel），不实例化任何可视化控件/窗口，
// 因此无 UI 线程依赖。原先住在 Vision.Tests（无 WPF），
// 按分层规则「宿主层测试独占 WPF」迁到本工程。
// ─────────────────────────────────────────────────────────────────────────────

namespace Vision.Workbench.Tests.WorkbenchSmoke;

/// <summary>Workbench 装配冒烟测试。</summary>
[Trait("Category", "Unit")]
public sealed class WorkbenchSmokeTests
{
    /// <summary>
    /// 测试相关入口 Browser_BuildsFullCatalog_AndSelectsFirst；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Equal、NotNull、NotEmpty；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Browser_BuildsFullCatalog_AndSelectsFirst()
    {
        using var browser = new TestBrowserViewModel();
        Assert.Equal(28, browser.ToolEntries.Count);
        Assert.Equal("OpenCV/Blob", browser.SelectedEntry?.Name);
        Assert.NotNull(browser.CurrentCard);
        Assert.NotNull(browser.CurrentParameters);
        Assert.NotEmpty(browser.ProviderEntries);
        Assert.NotEmpty(browser.SelectedProviderName);
        Assert.NotNull(browser.RoiEditor);
    }

    /// <summary>
    /// 测试相关入口 Browser_SelectsEveryEntry_WithoutThrowing；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 NotNull、Same；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Browser_SelectsEveryEntry_WithoutThrowing()
    {
        using var browser = new TestBrowserViewModel();
        foreach (var entry in browser.ToolEntries)
        {
            browser.SelectedEntry = entry;
            Assert.NotNull(browser.CurrentCard);
            Assert.NotNull(browser.CurrentParameters);
            Assert.Same(entry, browser.SelectedEntry);
        }
    }

    /// <summary>
    /// 通过现用导图和原卡片异步命令执行真实 OpenCV；合成像素只由测试夹具提供。
    /// </summary>
    [Fact]
    public async Task Browser_LoadedInput_ExecutesOriginalCard_AndPublishesBlob()
    {
        using var browser = new TestBrowserViewModel();
        browser.LoadImageCommand.Execute(null);
        Assert.Contains("synthetic-rect", browser.ImageInfoText);
        await browser.CurrentCard!.RunCommand.ExecuteAsync(null);
        Assert.Equal("执行成功", browser.CurrentCard.StatusText);
        var result = Assert.IsType<Vision.Tools.OpenCv.Inspection.BlobResult>(browser.CurrentCard!.LastResult);
        Assert.True(result.Success, result.Message);
        Assert.Equal(400, Assert.Single(result.Items).Area);
    }

    /// <summary>
    /// 无输入时执行明确失败，并断言已退役的演示/对比/基准及图像选项 API 不再公开。
    /// </summary>
    [Fact]
    public async Task Browser_WithoutInput_ReportsFailure_AndRetiredCommandsStayAbsent()
    {
        using var browser = new TestBrowserViewModel();
        Assert.Null(browser.InputImage);
        Assert.Null(browser.CurrentCard!.LastResult);
        await browser.CurrentCard.RunCommand.ExecuteAsync(null);
        var result = Assert.IsType<Vision.Tools.OpenCv.Inspection.BlobResult>(browser.CurrentCard.LastResult);
        Assert.False(result.Success);
        Assert.Contains("image", result.Message);
        Assert.StartsWith("执行失败", browser.CurrentCard.StatusText);
        foreach (var name in new[] { "UseDemoImageCommand", "CompareBlobCommand", "RunBenchmarkCommand", "ImageViews" })
            Assert.Null(typeof(Vision.Controls.ViewModels.VisionBrowserViewModel).GetProperty(name));
    }
}