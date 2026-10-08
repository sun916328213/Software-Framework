// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：WorkbenchSmokeTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Workbench、Vision.Workbench.Tests。导入不代表所有类型都会在运行时加载。
using Vision.Workbench;
using Vision.Workbench.Tests;
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
    /// <summary>
    /// 测试相关入口 Browser_BuildsFullCatalog_AndSelectsFirst；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Equal、NotNull、NotEmpty；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 Browser_SelectsEveryEntry_WithoutThrowing；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 NotNull、Same；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 Browser_DemoImage_Compare_AndBenchmark_FlowThrough；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Execute、Contains、NotEqual；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 Browser_Compare_WithoutImage_AsksForImage；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Execute、Contains；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Browser_Compare_WithoutImage_AsksForImage()
    {
        using var browser = new Vision.Workbench.ToolBrowserViewModel();
        browser.CompareBlobCommand.Execute(null);
        Assert.Contains("无图像", browser.CompareText);
    }
}