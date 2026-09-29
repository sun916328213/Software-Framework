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
