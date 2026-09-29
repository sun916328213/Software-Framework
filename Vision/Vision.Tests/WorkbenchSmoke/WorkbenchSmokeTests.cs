// ─────────────────────────────────────────────────────────────────────────────
// P10 —— WorkbenchSmoke（计划 §P10 测试域 WorkbenchSmoke + §P9 验收
// 「Workbench 成为所有新 Tool 的标准研发/验收入口」）。
//
// 只装配 VM（ToolBrowserViewModel），不实例化任何可视化控件/窗口，
// 因此无 UI 线程依赖，在普通 CI 可跑：目录完整、条目可建卡、
// Provider 列表可用、合成图→对比→Benchmark 全链路走通。
// ─────────────────────────────────────────────────────────────────────────────

using Xunit;

namespace Vision.Tests.WorkbenchSmoke;

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
