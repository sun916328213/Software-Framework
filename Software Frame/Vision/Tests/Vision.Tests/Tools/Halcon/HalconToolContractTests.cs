// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：HalconToolContractTests、HalconRuntimeFactAttribute。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Tests.Tools、System.Reflection、System.Text.Json、Vision.Core.Imaging、Vision.Core.Results、Vision.Tools.Halcon.Acquisition。导入不代表所有类型都会在运行时加载。
using Vision.Tests.Tools;
// ─────────────────────────────────────────────────────────────────────────────
// P3 验收测试 —— HALCON Tool Contract Tests（计划 §9.2「P3 | HALCON Tool Contract
// Tests；Parameters/Validate/Run/Result；资源释放与历史工具行为回归」）。
//
// 执行边界（计划 §9.1）：普通 CI 用例只碰 Validate / Parameters / Result / 纯 helper，
// 不需要 HALCON Runtime / License。文件末尾的真实图像集成测试须显式设置环境变量启用。
// ─────────────────────────────────────────────────────────────────────────────

using System.Reflection;
using System.Text.Json;
using Vision.Core.Imaging;
using Vision.Core.Results;
using Vision.Tools.Halcon.Acquisition;
using Vision.Tools.Halcon.Calibration;
using Vision.Tools.Halcon.Coordinate;
using Vision.Tools.Halcon.Identification;
using Vision.Tools.Halcon.Inspection;
using Vision.Tools.Halcon.Matching;
using Vision.Tools.Halcon.Measurement;
using Xunit;

namespace Vision.Tests.Tools.Halcon;

/// <summary>P3 标准化 Tool 的统一行为契约。</summary>
[Trait("Category", "Contract")]
public sealed class HalconToolContractTests
{
    // ── Validate：默认参数 ──────────────────────────────────────────────

    [Fact]
    public void Blob_DefaultParameters_AreValid()
    {
        using var tool = new BlobTool();
        var validation = tool.Validate();
        Assert.True(validation.IsValid, validation.ToMessage());
    }

    /// <summary>
    /// 验证 FindLine_DefaultParameters_AreValid 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Validate、True、ToMessage；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void FindLine_DefaultParameters_AreValid()
    {
        using var tool = new FindLineTool();
        var validation = tool.Validate();
        Assert.True(validation.IsValid, validation.ToMessage());
    }

    /// <summary>
    /// 验证 Fixture_DefaultParameters_AreValid 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Validate、True、ToMessage；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Fixture_DefaultParameters_AreValid()
    {
        using var tool = new FixtureTool();
        var validation = tool.Validate();
        Assert.True(validation.IsValid, validation.ToMessage());
    }

    /// <summary>
    /// 验证 Barcode_DataCode_DefaultParameters_AreValid 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 True、Validate；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Barcode_DataCode_DefaultParameters_AreValid()
    {
        using var barcode = new BarcodeTool();
        Assert.True(barcode.Validate().IsValid);
        using var dataCode = new DataCodeTool();
        Assert.True(dataCode.Validate().IsValid);
    }

    /// <summary>
    /// 验证 ShapeMatch_WithoutModel_ReportsNotTrained 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Validate、False、Contains；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ShapeMatch_WithoutModel_ReportsNotTrained()
    {
        using var tool = new ShapeMatchTool();
        var validation = tool.Validate();
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Issues, i => i.Code == "MODEL_NOT_TRAINED");
    }

    /// <summary>
    /// 验证 Calibration_WithoutPoints_ReportsTooFew 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Validate、False、Contains；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Calibration_WithoutPoints_ReportsTooFew()
    {
        using var tool = new NPointsCalibrationTool();
        var validation = tool.Validate();
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Issues, i => i.Code == "POINTS_TOO_FEW");
    }

    /// <summary>
    /// 验证 ImageFile_EmptyPath_ReportsEmpty 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Validate、False、Contains；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ImageFile_EmptyPath_ReportsEmpty()
    {
        using var tool = new ImageFileTool();
        var validation = tool.Validate();
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Issues, i => i.Code == "PATH_EMPTY");
    }

    // ── Validate：坏参数 ────────────────────────────────────────────────

    [Fact]
    public void Blob_MinGreaterThanMax_IsInvalid()
    {
        using var tool = new BlobTool();
        tool.Parameters.MinGray = 200;
        tool.Parameters.MaxGray = 100;
        var validation = tool.Validate();
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Issues, i => i.Code == "THRESHOLD_RANGE");
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
    /// 验证 Fixture_NonPositiveScale_IsInvalid 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Validate、False、Contains；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Fixture_NonPositiveScale_IsInvalid()
    {
        using var tool = new FixtureTool();
        tool.Parameters.Scale = 0;
        var validation = tool.Validate();
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Issues, i => i.Code == "SCALE_INVALID");
    }

    /// <summary>
    /// 验证 ShapeMatch_BadScoreRange_IsInvalid 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Validate、False、Contains；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ShapeMatch_BadScoreRange_IsInvalid()
    {
        using var tool = new ShapeMatchTool();
        tool.Parameters.MinScore = 2;
        var validation = tool.Validate();
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Issues, i => i.Code == "MIN_SCORE");
    }

    // ── Run：校验失败时直接返回失败结果（不碰 Runtime） ──────────────────

    [Fact]
    public void Run_WhenInvalid_ReturnsFailedResult_WithoutThrowing()
    {
        using var tool = new BlobTool();
        tool.Parameters.MinGray = 200;
        tool.Parameters.MaxGray = 100;
        var result = tool.Run(null);
        Assert.False(result.Success);
        Assert.NotNull(result.Validation);
        Assert.False(result.Validation!.IsValid);
        Assert.IsType<BlobResult>(result);
    }

    /// <summary>
    /// 验证 RunImage_WhenInvalid_ValidatesBeforeConverting 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 FromBuffer、RunImage、False、Contains；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void RunImage_WhenInvalid_ValidatesBeforeConverting()
    {
        using var tool = new BlobTool();
        tool.Parameters.MinGray = 200;
        tool.Parameters.MaxGray = 100;
        var unsupportedImage = VisionImage.FromBuffer(1, 1, PixelFormat.Mono16, new byte[2]);

        var result = tool.RunImage(unsupportedImage);

        Assert.False(result.Success);
        Assert.Contains(result.Validation!.Issues, issue => issue.Code == "THRESHOLD_RANGE");
        Assert.Contains("THRESHOLD_RANGE", result.Message);
    }

    /// <summary>
    /// 验证 RunImage_WhenFormatIsUnsupported_ReturnsFailedResult 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 FromBuffer、RunImage、False、True、Contains；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void RunImage_WhenFormatIsUnsupported_ReturnsFailedResult()
    {
        using var tool = new BlobTool();
        var unsupportedImage = VisionImage.FromBuffer(1, 1, PixelFormat.Mono16, new byte[2]);

        var result = tool.RunImage(unsupportedImage);

        Assert.False(result.Success);
        Assert.True(result.Validation!.IsValid);
        Assert.Contains("Mono8", result.Message);
    }

    // ── Result：稳定类型，不暴露 SDK 原生类型 ────────────────────────────

    public static IEnumerable<object[]> P3ResultTypes => new[]
    {
        new object[] { typeof(BlobResult) },
        new object[] { typeof(ShapeMatchResult) },
        new object[] { typeof(FindLineResult) },
        new object[] { typeof(BarcodeResult) },
        new object[] { typeof(DataCodeResult) },
        new object[] { typeof(NPointsCalibrationResult) },
        new object[] { typeof(FixtureResult) },
        new object[] { typeof(ImageFileResult) },
    };

    /// <summary>
    /// 验证 Result_DerivesFromToolResult 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 nameof、True、IsAssignableFrom；具体条件和先后次序见方法体。
    /// </summary>
    [Theory]
    [MemberData(nameof(P3ResultTypes))]
    public void Result_DerivesFromToolResult(Type resultType)
    {
        Assert.True(typeof(ToolResult).IsAssignableFrom(resultType), $"{resultType.Name} 必须派生自 ToolResult。");
    }

    /// <summary>
    /// 验证 Result_PublicSignature_ExposesNoHalconTypes 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 nameof、ExposedTypes、GetName、False、StartsWith；具体条件和先后次序见方法体。
    /// </summary>
    [Theory]
    [MemberData(nameof(P3ResultTypes))]
    public void Result_PublicSignature_ExposesNoHalconTypes(Type resultType)
    {
        // 计划 §P3 验收：「结果不再只暴露 HTuple/HObject」。
        // 公开属性/字段的类型（含泛型实参）不得来自 HalconDotNet / OpenCvSharp。
        foreach (var exposed in ExposedTypes(resultType))
        {
            var assembly = exposed.Assembly.GetName().Name ?? string.Empty;
            Assert.False(
                assembly.StartsWith("HalconDotNet", StringComparison.Ordinal) ||
                assembly.StartsWith("MVTec", StringComparison.Ordinal) ||
                assembly.StartsWith("OpenCvSharp", StringComparison.Ordinal),
                $"{resultType.Name} 暴露了 SDK 类型：{exposed.FullName}");
        }
    }

    // ── Parameters：JSON 往返 ───────────────────────────────────────────

    [Fact]
    public void Parameters_RoundTripThroughJson()
    {
        var blob = new BlobParameters { MinGray = 30, MaxGray = 200 };
        Assert.Equal(30, JsonSerializer.Deserialize<BlobParameters>(JsonSerializer.Serialize(blob))!.MinGray);

        var fixture = new FixtureParameters { RotationDeg = 15, Scale = 1.2 };
        var fixtureBack = JsonSerializer.Deserialize<FixtureParameters>(JsonSerializer.Serialize(fixture))!;
        Assert.Equal(15, fixtureBack.RotationDeg);
        Assert.Equal(1.2, fixtureBack.Scale);

        var shape = new ShapeMatchParameters { MinScore = 0.7, MaxMatches = 3 };
        Assert.Equal(0.7, JsonSerializer.Deserialize<ShapeMatchParameters>(JsonSerializer.Serialize(shape))!.MinScore);

        var line = new FindLineParameters { Threshold = 40, CaliperCount = 5 };
        Assert.Equal(5, JsonSerializer.Deserialize<FindLineParameters>(JsonSerializer.Serialize(line))!.CaliperCount);
    }

    // ── Dispose：可重复释放 ─────────────────────────────────────────────

    [Fact]
    public void ShapeMatch_Dispose_IsIdempotent()
    {
        var tool = new ShapeMatchTool();
        tool.Dispose();
        tool.Dispose();
        Assert.Throws<ObjectDisposedException>(() => tool.Validate());
    }

    // ── 真实 Runtime 集成测试（显式启用，不进普通 CI） ───────────────────

    [HalconRuntimeFact]
    [Trait("Category", "RequiresRuntime")]
    public void RunImage_OnRealMono8Image_FindsBlob()
    {
        using var tool = new BlobTool();
        tool.Parameters.MinGray = 200;
        tool.Parameters.Features.Clear();
        var image = Mono8BlobImage();

        var result = tool.RunImage(image);

        Assert.True(result.Success, result.Message);
        var blob = Assert.Single(result.Items);
        Assert.Equal(4, blob.Area);
        Assert.Equal(new Vision.Core.Geometry.Point2D(1.5, 1.5), blob.Center);
    }

    /// <summary>
    /// 验证 RunImage_RepeatedCalls_KeepStableResults 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Clear、Mono8BlobImage、Clone、RunImage、True；具体条件和先后次序见方法体。
    /// </summary>
    [HalconRuntimeFact]
    [Trait("Category", "RequiresRuntime")]
    public void RunImage_RepeatedCalls_KeepStableResults()
    {
        using var tool = new BlobTool();
        tool.Parameters.MinGray = 200;
        tool.Parameters.Features.Clear();
        var image = Mono8BlobImage();
        var originalPixels = (byte[])image.Pixels.Clone();

        for (var run = 0; run < 100; run++)
        {
            var result = tool.RunImage(image);
            Assert.True(result.Success, $"第 {run + 1} 次运行失败：{result.Message}");
            var blob = Assert.Single(result.Items);
            Assert.Equal(4, blob.Area);
            Assert.Equal(new Vision.Core.Geometry.Point2D(1.5, 1.5), blob.Center);
        }

        Assert.Equal(originalPixels, image.Pixels);
    }

    // ── 工具 ────────────────────────────────────────────────────────────

    private static VisionImage Mono8BlobImage() => VisionImage.FromBuffer(4, 4, PixelFormat.Mono8,
    [
        0, 0, 0, 0,
        0, 255, 255, 0,
        0, 255, 255, 0,
        0, 0, 0, 0,
    ]);

    /// <summary>
    /// 验证 ExposedTypes 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 GetProperties、Unwrap、GetFields、GetGenericArguments、StartsWith；具体条件和先后次序见方法体。
    /// </summary>
    private static IEnumerable<Type> ExposedTypes(Type type)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
        foreach (var property in type.GetProperties(flags))
            yield return Unwrap(property.PropertyType);
        foreach (var field in type.GetFields(flags))
            yield return Unwrap(field.FieldType);

        static Type Unwrap(Type t)
        {
            if (t.IsGenericType && !t.IsGenericTypeDefinition)
            {
                foreach (var argument in t.GetGenericArguments())
                {
                    if (!argument.IsGenericParameter &&
                        (argument.Assembly.GetName().Name ?? string.Empty).StartsWith("HalconDotNet", StringComparison.Ordinal))
                        return argument;
                }

                return t.GetGenericTypeDefinition();
            }

            return t;
        }
    }
}

/// <summary>
/// HalconRuntimeFactAttribute 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// 继承或实现的契约：FactAttribute。具体约束按这些契约及各成员实现解释。
/// </summary>
public sealed class HalconRuntimeFactAttribute : FactAttribute
{
    /// <summary>
    /// 初始化 HalconRuntimeFactAttribute 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 GetEnvironmentVariable；具体条件和先后次序见方法体。
    /// </summary>
    public HalconRuntimeFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("VISION_RUN_HALCON_INTEGRATION") != "1")
            Skip = "设置 VISION_RUN_HALCON_INTEGRATION=1 并准备 HALCON Runtime/License 后运行。";
    }
}
