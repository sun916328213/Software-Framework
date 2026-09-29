// ─────────────────────────────────────────────────────────────────────────────
// P10 —— WorkbenchSmoke（计划 §P10 测试域 WorkbenchSmoke + §P9 验收
// 「Workbench 成为所有新 Tool 的标准研发/验收入口」）。
//
// 只装配 VM（ToolBrowserViewModel），不实例化任何可视化控件/窗口，
// 因此无 UI 线程依赖。原先住在 Vision.Tests（无 WPF），
// 按分层规则「宿主层测试独占 WPF」迁到本工程。
// ─────────────────────────────────────────────────────────────────────────────

namespace Vision.Workbench.Tests.WorkbenchSmoke;

/// <summary>Workbench 装配冒烟测试。</summary>
[Trait("Category", "Unit")]
public sealed class WorkbenchSmokeTests
{
    [Fact]
    public void Browser_BuildsFullCatalog_AndSelectsFirst()
    {
        using var browser = new Vision.Workbench.ToolBrowserViewModel();
        Assert.Equal(18, browser.ToolEntries.Count);
        Assert.NotNull(browser.SelectedEntry);
        Assert.NotNull(browser.CurrentCard);
        Assert.NotNull(browser.CurrentParameters);
        Assert.NotEmpty(browser.ProviderEntries);
        Assert.NotEmpty(browser.SelectedProviderName);
        Assert.NotNull(browser.RoiEditor);
    }

    [Fact]
    public void Browser_SelectsEveryEntry_WithoutThrowing()
    {
        using var browser = new Vision.Workbench.ToolBrowserViewModel();
        foreach (var entry in browser.ToolEntries)
        {
            browser.SelectedEntry = entry;
            Assert.NotNull(browser.CurrentCard);
            Assert.NotNull(browser.CurrentParameters);
            Assert.Same(entry, browser.SelectedEntry);
        }
    }

    [Fact]
    public void Browser_DemoImage_Compare_AndBenchmark_FlowThrough()
    {
        using var browser = new Vision.Workbench.ToolBrowserViewModel();
        browser.UseDemoImageCommand.Execute(null);
        Assert.Contains("合成图", browser.ImageInfoText);

        browser.CompareBlobCommand.Execute(null);
        Assert.NotEqual("—", browser.CompareText);
        Assert.Contains("OpenCV", browser.CompareText);

        browser.RunBenchmarkCommand.Execute(null);
        Assert.Contains("Benchmark", browser.BenchmarkText);
    }

    [Fact]
    public void Browser_Compare_WithoutImage_AsksForImage()
    {
        using var browser = new Vision.Workbench.ToolBrowserViewModel();
        browser.CompareBlobCommand.Execute(null);
        Assert.Contains("无图像", browser.CompareText);
    }
}