// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：BaselineTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Xunit、Xunit.Abstractions。导入不代表所有类型都会在运行时加载。
// ─────────────────────────────────────────────────────────────────────────────
// P10 —— 性能基线（Trait=Performance，与正确性测试分离）。
// 口径（计划 §P10）：只记录耗时供人看，不设时间阈值断言——
// 性能波动永不染红普通单元测试；基线对比靠 CI 日志 / 人工 `--filter Category=Performance`。
// 断言仅保证功能成功（工具可用性），耗时走 ITestOutputHelper.
// ─────────────────────────────────────────────────────────────────────────────

using Xunit;
using Xunit.Abstractions;

namespace Vision.Tests.Performance;

/// <summary>性能基线（记录型，无时间阈值）。</summary>
[Trait("Category", "Performance")]
public sealed class BaselineTests
{
    private readonly ITestOutputHelper _output;

    /// <summary>构造（xUnit 注入输出）。</summary>
    public BaselineTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// 测试相关入口 OpenCv_GoldenPipeline_RecordsElapsed；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Rect600、Run、True、WriteLine；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void OpenCv_GoldenPipeline_RecordsElapsed()
    {
        var image = TestAssets.GoldenImages.Rect600();

        using var threshold = new Vision.Tools.OpenCv.Processing.ThresholdTool();
        threshold.Parameters.MinGray = 128;
        var r1 = threshold.Run(image);
        Assert.True(r1.Success, r1.Message);

        using var blob = new Vision.Tools.OpenCv.Inspection.BlobTool();
        blob.Parameters.MinGray = 128;
        blob.Parameters.Features = [];
        var r2 = blob.Run(image);
        Assert.True(r2.Success, r2.Message);

        _output.WriteLine($"[baseline] Threshold(Rect600)={r1.ElapsedMs}ms Blob(Rect600)={r2.ElapsedMs}ms");
    }

    /// <summary>
    /// 测试相关入口 OpenCv_TemplateMatch_RecordsElapsed；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 PatternTemplate、Run、PastePattern、True、WriteLine；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void OpenCv_TemplateMatch_RecordsElapsed()
    {
        using var tool = new Vision.Tools.OpenCv.Matching.TemplateMatchTool();
        tool.Parameters.Template = Vision.Tests.Tools.SyntheticImages.PatternTemplate();
        tool.Parameters.MinScore = 0.9;
        var result = tool.Run(TestAssets.GoldenImages.PastePattern());
        Assert.True(result.Success, result.Message);
        _output.WriteLine($"[baseline] TemplateMatch(PastePattern)={result.ElapsedMs}ms");
    }
}
