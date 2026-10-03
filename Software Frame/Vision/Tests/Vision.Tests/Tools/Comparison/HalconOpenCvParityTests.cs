// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：HalconOpenCvParityTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Tests.Tools、Vision.Tools.Halcon.Inspection.BlobTool、Vision.Tools.Halcon.Measurement.FindLineTool、Vision.Tools.OpenCv.Inspection.BlobTool、Vision.Tools.OpenCv.Measurement.FindLineTool、Xunit。导入不代表所有类型都会在运行时加载。
using Vision.Tests.Tools;
// ─────────────────────────────────────────────────────────────────────────────
// P4 验收测试 —— HALCON / OpenCV 同类 Tool 结果对比（计划 §P4
// 「建立 HALCON/OpenCV 同类 Tool 的结果对比测试」）。
//
// 口径（计划 §P10）：按业务容差比较，不要求算法输出逐像素一致；
// 普通 CI 无 HALCON Runtime，因此对比分两层：
//   ① Validate 语义一致性（双边真实执行，无 Runtime 需求）；
//   ② OpenCV 实测值 vs 解析真值（HALCON AreaCenter 同为像素均值语义，真值即跨后端期望）。
// 真正的双边同图执行是集成测试（RequiresRuntime），见底部占位。
// ─────────────────────────────────────────────────────────────────────────────

using HalconBlob = Vision.Tools.Halcon.Inspection.BlobTool;
using HalconFindLine = Vision.Tools.Halcon.Measurement.FindLineTool;
using OpenCvBlob = Vision.Tools.OpenCv.Inspection.BlobTool;
using OpenCvFindLine = Vision.Tools.OpenCv.Measurement.FindLineTool;
using Xunit;

namespace Vision.Tests.Tools.Comparison;

/// <summary>HALCON / OpenCV 同类 Tool 语义对比（业务容差口径）。</summary>
[Trait("Category", "Contract")]
public sealed class HalconOpenCvParityTests
{
    public static IEnumerable<object[]> ThresholdCases => new[]
    {
        new object[] { 0, 255, true },
        new object[] { 30, 200, true },
        new object[] { 200, 100, false },
        new object[] { -1, 100, false },
        new object[] { 0, 256, false },
    };

    /// <summary>
    /// 阈值校验双边一致：同一下限/上限，两边 IsValid 相同、首个错误码相同。
    /// </summary>
    [Theory]
    [MemberData(nameof(ThresholdCases))]
    public void Blob_ThresholdValidation_AgreesAcrossBackends(int min, int max, bool expectedValid)
    {
        using var halcon = new HalconBlob();
        halcon.Parameters.MinGray = min;
        halcon.Parameters.MaxGray = max;
        halcon.Parameters.Features.Clear();
        var halconResult = halcon.Validate();

        using var openCv = new OpenCvBlob();
        openCv.Parameters.MinGray = min;
        openCv.Parameters.MaxGray = max;
        openCv.Parameters.Features.Clear();
        var openCvResult = openCv.Validate();

        Assert.Equal(expectedValid, halconResult.IsValid);
        Assert.Equal(expectedValid, openCvResult.IsValid);
        if (!expectedValid)
            Assert.Equal(halconResult.Issues[0].Code, openCvResult.Issues[0].Code);
    }

    /// <summary>
    /// 退化线段双边一致：起点 == 终点时两边都报 LINE_DEGENERATE。
    /// </summary>
    [Fact]
    public void FindLine_DegenerateLine_AgreesAcrossBackends()
    {
        using var halcon = new HalconFindLine();
        halcon.Parameters.LineEnd = halcon.Parameters.LineStart;
        var halconResult = halcon.Validate();

        using var openCv = new OpenCvFindLine();
        openCv.Parameters.LineEnd = openCv.Parameters.LineStart;
        var openCvResult = openCv.Validate();

        Assert.False(halconResult.IsValid);
        Assert.False(openCvResult.IsValid);
        Assert.Contains(halconResult.Issues, i => i.Code == "LINE_DEGENERATE");
        Assert.Contains(openCvResult.Issues, i => i.Code == "LINE_DEGENERATE");
    }

    /// <summary>
    /// 已知能力差（非 1:1 的诚实记录，计划 §1）：HALCON 接受任意 SelectShape
    /// 特征名（含 circularity），OpenCV P4 仅支持 area/width/height。
    /// </summary>
    [Fact]
    public void Blob_FeatureCoverage_DivergenceIsExplicit()
    {
        using var halcon = new HalconBlob();
        halcon.Parameters.Features.Clear();
        halcon.Parameters.Features.Add(new Vision.Tools.Halcon.Inspection.BlobFeatureRange
        {
            Feature = "circularity",
            Min = 0,
            Max = 1,
        });
        Assert.True(halcon.Validate().IsValid);

        using var openCv = new OpenCvBlob();
        openCv.Parameters.Features.Clear();
        openCv.Parameters.Features.Add(new Vision.Tools.OpenCv.Inspection.BlobFeatureRange
        {
            Feature = "circularity",
            Min = 0,
            Max = 1,
        });
        var openCvResult = openCv.Validate();
        Assert.False(openCvResult.IsValid);
        Assert.Contains(openCvResult.Issues, i => i.Code == "FEATURE_UNSUPPORTED");
    }

    /// <summary>
    /// OpenCV Blob 实测 vs 解析真值（HALCON AreaCenter 同为像素均值语义）：
    /// 中心容差 1 像素，面积精确相等 —— 即跨后端业务容差。
    /// </summary>
    [Fact]
    public void Blob_OpenCvResult_MatchesAnalyticTruthWithinTolerance()
    {
        var image = SyntheticImages.WhiteRectangle(100, 100, 20, 30, 40, 60);
        using var tool = new OpenCvBlob();
        tool.Parameters.MinGray = 128;
        tool.Parameters.Features = [];
        var result = tool.Run(image);
        Assert.True(result.Success, result.Message);
        var item = Assert.Single(result.Items);
        Assert.Equal(600, item.Area);
        Assert.True(item.Center.DistanceTo(new Vision.Core.Geometry.Point2D(29.5, 44.5)) <= 1.0,
            $"center={item.Center}");
    }

    // ── 集成测试占位（RequiresRuntime，不进普通 CI） ──────────────────

    /// <summary>
    /// 双边同图执行对比（有 HALCON Runtime 时：同合成图跑两边 Blob，
    /// 中心 ≤1px、面积相对误差 ≤5%）。当前环境无 Runtime，占位不断言。
    /// </summary>
    [Fact(Skip = "需要真实 HALCON Runtime / License（计划 §9.1 Integration，有环境时执行）。")]
    [Trait("Category", "RequiresRuntime")]
    public void Blob_SideBySide_OnSameImage_RequiresHalconRuntime()
    {
    }
}
