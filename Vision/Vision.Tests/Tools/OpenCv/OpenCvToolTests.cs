// ─────────────────────────────────────────────────────────────────────────────
// P4 验收测试 —— OpenCV 首批 Tool（计划 §9.2「P4 | OpenCV Tool 单元/回归测试」）。
//
// 执行边界：全部跑真实 Cv2 算子（OpenCvSharp 自带 CPU 实现，无需 GPU / HALCON /
// License），输入为解析解已知的合成图，断言用业务容差（计划 §P10「不要求逐像素一致」）。
// ─────────────────────────────────────────────────────────────────────────────

using System.Reflection;
using System.Text.Json;
using Vision.Core.Results;
using Vision.Tools.OpenCv.Inspection;
using Vision.Tools.OpenCv.Matching;
using Vision.Tools.OpenCv.Measurement;
using Vision.Tools.OpenCv.Processing;
using Xunit;

namespace Vision.Tests.Tools.OpenCv;

/// <summary>P4 OpenCV 首批 Tool 的契约 + 真实执行测试。</summary>
[Trait("Category", "Unit")]
public sealed class OpenCvToolTests
{
    // ── Validate ──────────────────────────────────────────────────────

    [Fact]
    public void AllTools_DefaultParameters_AreValid()
    {
        using var threshold = new ThresholdTool();
        Assert.True(threshold.Validate().IsValid);
        using var morphology = new MorphologyTool();
        Assert.True(morphology.Validate().IsValid);
        using var blob = new BlobTool();
        Assert.True(blob.Validate().IsValid);
        using var findLine = new FindLineTool();
        Assert.True(findLine.Validate().IsValid);
        using var findCircle = new FindCircleTool();
        Assert.True(findCircle.Validate().IsValid);
    }

    [Fact]
    public void TemplateMatch_WithoutTemplate_IsInvalid()
    {
        using var tool = new TemplateMatchTool();
        var validation = tool.Validate();
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Issues, i => i.Code == "TEMPLATE_EMPTY");
    }

    [Fact]
    public void Threshold_MinGreaterThanMax_IsInvalid()
    {
        using var tool = new ThresholdTool();
        tool.Parameters.MinGray = 200;
        tool.Parameters.MaxGray = 100;
        var validation = tool.Validate();
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Issues, i => i.Code == "THRESHOLD_RANGE");
    }

    [Fact]
    public void Morphology_EvenKernel_IsInvalid()
    {
        using var tool = new MorphologyTool();
        tool.Parameters.KernelSize = 4;
        Assert.False(tool.Validate().IsValid);
    }

    [Fact]
    public void FindLine_DegenerateLine_IsInvalid()
    {
        using var tool = new FindLineTool();
        tool.Parameters.LineEnd = tool.Parameters.LineStart;
        var validation = tool.Validate();
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Issues, i => i.Code == "LINE_DEGENERATE");
    }

    [Fact]
    public void FindCircle_RadiusRangeReversed_IsInvalid()
    {
        using var tool = new FindCircleTool();
        tool.Parameters.RadiusMax = 5;
        var validation = tool.Validate();
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Issues, i => i.Code == "RADIUS_RANGE");
    }

    [Fact]
    public void Run_WhenInvalid_ReturnsFailedResult_WithoutThrowing()
    {
        using var tool = new ThresholdTool();
        tool.Parameters.MinGray = 200;
        tool.Parameters.MaxGray = 100;
        var result = tool.Run(null);
        Assert.False(result.Success);
        Assert.NotNull(result.Validation);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Blob_MissingFeatureConfig_ReturnsFailedResult(bool missingList)
    {
        using var tool = new BlobTool();
        tool.Parameters.Features = missingList ? null! : [null!];

        var result = tool.Run(null);

        Assert.False(result.Success);
        Assert.Contains(result.Validation!.Issues,
            issue => issue.Code == (missingList ? "FEATURES_MISSING" : "FEATURE_MISSING"));
    }

    // ── 真实执行：合成图 + 解析期望 ────────────────────────────────────

    [Fact]
    public void Threshold_CountsWhitePixels()
    {
        // 100×100，[20,40)×[30,60) 白块 ⇒ 20×30 = 600 前景。
        var image = SyntheticImages.WhiteRectangle(100, 100, 20, 30, 40, 60);
        using var tool = new ThresholdTool();
        tool.Parameters.MinGray = 128;
        var result = tool.Run(image);
        Assert.True(result.Success, result.Message);
        Assert.Equal(600, result.WhitePixelCount);
        Assert.Equal(0.06, result.ForegroundRatio, 6);
        Assert.NotNull(result.OutputImage);
    }

    [Fact]
    public void Morphology_Erode_ShrinksRectangleByOnePixelBorder()
    {
        // 3×3 腐蚀吃掉 1 像素边 ⇒ (20-2)×(30-2) = 504。
        var image = SyntheticImages.WhiteRectangle(100, 100, 20, 30, 40, 60);
        using var tool = new MorphologyTool();
        tool.Parameters.Operation = MorphologyOperation.Erode;
        tool.Parameters.KernelSize = 3;
        var result = tool.Run(image);
        Assert.True(result.Success, result.Message);
        Assert.Equal(504, result.WhitePixelCount);
    }

    [Fact]
    public void Blob_FindsRectangle_WithAnalyticCenterAndArea()
    {
        // 质心 = 像素坐标均值：((20+39)/2, (30+59)/2) = (29.5, 44.5)，面积 600。
        var image = SyntheticImages.WhiteRectangle(100, 100, 20, 30, 40, 60);
        using var tool = new BlobTool();
        tool.Parameters.MinGray = 128;
        tool.Parameters.Features = [];
        var result = tool.Run(image);
        Assert.True(result.Success, result.Message);
        var item = Assert.Single(result.Items);
        Assert.Equal(600, item.Area);
        Assert.Equal(29.5, item.Center.X, 1);
        Assert.Equal(44.5, item.Center.Y, 1);
        Assert.Equal(new Vision.Core.Geometry.Rect2D(20, 30, 20, 30), item.Bounds);
    }

    [Fact]
    public void Blob_FeatureFilter_RejectsSmallArea()
    {
        var image = SyntheticImages.WhiteRectangle(100, 100, 20, 30, 40, 60);
        using var tool = new BlobTool();
        tool.Parameters.MinGray = 128;
        tool.Parameters.Features =
        [
            new BlobFeatureRange { Feature = "area", Min = 1000, Max = 99999 },
        ];
        var result = tool.Run(image);
        Assert.True(result.Success, result.Message);
        Assert.Empty(result.Items);
    }

    [Fact]
    public void TemplateMatch_FindsPastedPattern()
    {
        var template = SyntheticImages.PatternTemplate();
        var image = SyntheticImages.PasteTemplate(100, 100, template, 50, 60);
        using var tool = new TemplateMatchTool();
        tool.Parameters.Template = template;
        tool.Parameters.MinScore = 0.9;
        var result = tool.Run(image);
        Assert.True(result.Success, result.Message);
        var match = Assert.Single(result.Matches);
        // 期望左上 (50,60)：容差 1 像素；归一化相关峰 ≈1。
        Assert.InRange(match.Bounds.X, 49, 51);
        Assert.InRange(match.Bounds.Y, 59, 61);
        Assert.True(match.Score > 0.95, $"score={match.Score}");
    }

    [Fact]
    public void FindLine_FitsSyntheticHorizontalLine()
    {
        // y=100 水平线（20..180）：拟合 y 应贴近 100，x 覆盖大部。
        var image = SyntheticImages.HorizontalLine(200, 200, 100, 20, 180);
        using var tool = new FindLineTool();
        tool.Parameters.LineStart = new Vision.Core.Geometry.Point2D(20, 100);
        tool.Parameters.LineEnd = new Vision.Core.Geometry.Point2D(180, 100);
        var result = tool.Run(image);
        Assert.True(result.Success, result.Message);
        Assert.InRange(result.Start.Y, 97.5, 102.5);
        Assert.InRange(result.End.Y, 97.5, 102.5);
        Assert.True(result.Start.X <= 25, $"startX={result.Start.X}");
        Assert.True(result.End.X >= 175, $"endX={result.End.X}");
        Assert.True(result.EdgePointCount >= 100, $"edges={result.EdgePointCount}");
    }

    [Fact]
    public void FindCircle_FindsSyntheticDisc()
    {
        // 实心圆盘 r=30 @ (100,120)：Hough 梯度法应在业务容差内找回。
        var image = SyntheticImages.FilledDisc(200, 100, 120, 30);
        using var tool = new FindCircleTool();
        tool.Parameters.ExpectedCenter = new Vision.Core.Geometry.Point2D(100, 120);
        tool.Parameters.RadiusMin = 20;
        tool.Parameters.RadiusMax = 40;
        var result = tool.Run(image);
        Assert.True(result.Success, result.Message);
        Assert.True(result.Center.DistanceTo(new Vision.Core.Geometry.Point2D(100, 120)) <= 4,
            $"center={result.Center}");
        Assert.InRange(result.Radius, 27, 33);
    }

    // ── Result：稳定类型，不暴露 Mat ──────────────────────────────────

    public static IEnumerable<object[]> P4ResultTypes => new[]
    {
        new object[] { typeof(ThresholdResult) },
        new object[] { typeof(MorphologyResult) },
        new object[] { typeof(BlobResult) },
        new object[] { typeof(TemplateMatchResult) },
        new object[] { typeof(FindLineResult) },
        new object[] { typeof(FindCircleResult) },
    };

    [Theory]
    [MemberData(nameof(P4ResultTypes))]
    public void Result_DerivesFromToolResult_AndExposesNoNativeTypes(Type resultType)
    {
        Assert.True(typeof(ToolResult).IsAssignableFrom(resultType));
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
        foreach (var property in resultType.GetProperties(flags))
        {
            foreach (var exposed in Unwrap(property.PropertyType))
            {
                var assembly = exposed.Assembly.GetName().Name ?? string.Empty;
                Assert.False(
                    assembly.StartsWith("OpenCvSharp", StringComparison.Ordinal) ||
                    assembly.StartsWith("HalconDotNet", StringComparison.Ordinal) ||
                    assembly.StartsWith("MVTec", StringComparison.Ordinal),
                    $"{resultType.Name}.{property.Name} 暴露了原生类型：{exposed.FullName}");
            }
        }
    }

    [Fact]
    public void Parameters_RoundTripThroughJson()
    {
        var threshold = new ThresholdParameters { MinGray = 30, MaxGray = 200 };
        Assert.Equal(30, JsonSerializer.Deserialize<ThresholdParameters>(JsonSerializer.Serialize(threshold))!.MinGray);

        var morphology = new MorphologyParameters { Operation = MorphologyOperation.Close, KernelSize = 5 };
        Assert.Equal(MorphologyOperation.Close,
            JsonSerializer.Deserialize<MorphologyParameters>(JsonSerializer.Serialize(morphology))!.Operation);

        var circle = new FindCircleParameters { RadiusMin = 8, RadiusMax = 60 };
        Assert.Equal(60, JsonSerializer.Deserialize<FindCircleParameters>(JsonSerializer.Serialize(circle))!.RadiusMax);
    }

    private static IEnumerable<Type> Unwrap(Type type)
    {
        if (type.IsGenericType && !type.IsGenericTypeDefinition)
        {
            foreach (var argument in type.GetGenericArguments())
            {
                if (argument.IsGenericParameter)
                    continue;
                var assembly = argument.Assembly.GetName().Name ?? string.Empty;
                if (assembly.StartsWith("OpenCvSharp", StringComparison.Ordinal) ||
                    assembly.StartsWith("HalconDotNet", StringComparison.Ordinal))
                    yield return argument;
            }

            yield break;
        }

        yield return type;
    }
}
