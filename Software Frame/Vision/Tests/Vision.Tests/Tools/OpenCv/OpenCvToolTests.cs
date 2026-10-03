// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：OpenCvToolTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Tests.Tools、System.Reflection、System.Text.Json、Vision.Core.Results、Vision.Tools.OpenCv.Inspection、Vision.Tools.OpenCv.Matching。导入不代表所有类型都会在运行时加载。
using Vision.Tests.Tools;
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

    /// <summary>
    /// 验证 AllTools_DefaultParameters_AreValid 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 True、Validate；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 TemplateMatch_WithoutTemplate_IsInvalid 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Validate、False、Contains；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void TemplateMatch_WithoutTemplate_IsInvalid()
    {
        using var tool = new TemplateMatchTool();
        var validation = tool.Validate();
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Issues, i => i.Code == "TEMPLATE_EMPTY");
    }

    /// <summary>
    /// 验证 Threshold_MinGreaterThanMax_IsInvalid 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Validate、False、Contains；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 Morphology_EvenKernel_IsInvalid 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 False、Validate；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Morphology_EvenKernel_IsInvalid()
    {
        using var tool = new MorphologyTool();
        tool.Parameters.KernelSize = 4;
        Assert.False(tool.Validate().IsValid);
    }

    /// <summary>
    /// 验证 FindLine_DegenerateLine_IsInvalid 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Validate、False、Contains；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void FindLine_DegenerateLine_IsInvalid()
    {
        using var tool = new FindLineTool();
        tool.Parameters.LineEnd = tool.Parameters.LineStart;
        var validation = tool.Validate();
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Issues, i => i.Code == "LINE_DEGENERATE");
    }

    /// <summary>
    /// 验证 FindCircle_RadiusRangeReversed_IsInvalid 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Validate、False、Contains；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void FindCircle_RadiusRangeReversed_IsInvalid()
    {
        using var tool = new FindCircleTool();
        tool.Parameters.RadiusMax = 5;
        var validation = tool.Validate();
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Issues, i => i.Code == "RADIUS_RANGE");
    }

    /// <summary>
    /// 验证 Run_WhenInvalid_ReturnsFailedResult_WithoutThrowing 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Run、False、NotNull；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 Blob_MissingFeatureConfig_ReturnsFailedResult 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Run、False、Contains；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 Morphology_Erode_ShrinksRectangleByOnePixelBorder 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 WhiteRectangle、Run、True、Equal；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 Blob_FindsRectangle_WithAnalyticCenterAndArea 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 WhiteRectangle、Run、True、Single、Equal；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 Blob_FeatureFilter_RejectsSmallArea 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 WhiteRectangle、Run、True、Empty；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 TemplateMatch_FindsPastedPattern 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 PatternTemplate、PasteTemplate、Run、True、Single；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 FindLine_FitsSyntheticHorizontalLine 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 HorizontalLine、Run、True、InRange；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 FindCircle_FindsSyntheticDisc 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 FilledDisc、Run、True、DistanceTo、InRange；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 Result_DerivesFromToolResult_AndExposesNoNativeTypes 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 nameof、True、IsAssignableFrom、GetProperties、Unwrap；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 Parameters_RoundTripThroughJson 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Equal、Deserialize、Serialize；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 Unwrap 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 GetGenericArguments、GetName、StartsWith；具体条件和先后次序见方法体。
    /// </summary>
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
