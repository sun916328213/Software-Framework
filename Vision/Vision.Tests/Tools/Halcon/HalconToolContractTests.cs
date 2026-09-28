// ─────────────────────────────────────────────────────────────────────────────
// P3 验收测试 —— HALCON Tool Contract Tests（计划 §9.2「P3 | HALCON Tool Contract
// Tests；Parameters/Validate/Run/Result；资源释放与历史工具行为回归」）。
//
// 执行边界（计划 §9.1）：本文件全部是普通 CI 用例 —— 只碰 Validate / Parameters /
// Result / 纯 helper，不调用任何 HOperatorSet，不需要 HALCON Runtime / License /
// GPU。真实图像执行是集成测试（RequiresRuntime），见底部的 Skip 用例占位。
// ─────────────────────────────────────────────────────────────────────────────

using System.Reflection;
using System.Text.Json;
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

    [Fact]
    public void FindLine_DefaultParameters_AreValid()
    {
        using var tool = new FindLineTool();
        var validation = tool.Validate();
        Assert.True(validation.IsValid, validation.ToMessage());
    }

    [Fact]
    public void Fixture_DefaultParameters_AreValid()
    {
        using var tool = new FixtureTool();
        var validation = tool.Validate();
        Assert.True(validation.IsValid, validation.ToMessage());
    }

    [Fact]
    public void Barcode_DataCode_DefaultParameters_AreValid()
    {
        using var barcode = new BarcodeTool();
        Assert.True(barcode.Validate().IsValid);
        using var dataCode = new DataCodeTool();
        Assert.True(dataCode.Validate().IsValid);
    }

    [Fact]
    public void ShapeMatch_WithoutModel_ReportsNotTrained()
    {
        using var tool = new ShapeMatchTool();
        var validation = tool.Validate();
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Issues, i => i.Code == "MODEL_NOT_TRAINED");
    }

    [Fact]
    public void Calibration_WithoutPoints_ReportsTooFew()
    {
        using var tool = new NPointsCalibrationTool();
        var validation = tool.Validate();
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Issues, i => i.Code == "POINTS_TOO_FEW");
    }

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
    public void Fixture_NonPositiveScale_IsInvalid()
    {
        using var tool = new FixtureTool();
        tool.Parameters.Scale = 0;
        var validation = tool.Validate();
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Issues, i => i.Code == "SCALE_INVALID");
    }

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

    [Theory]
    [MemberData(nameof(P3ResultTypes))]
    public void Result_DerivesFromToolResult(Type resultType)
    {
        Assert.True(typeof(ToolResult).IsAssignableFrom(resultType), $"{resultType.Name} 必须派生自 ToolResult。");
    }

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

    // ── 集成测试占位（RequiresRuntime，不进普通 CI） ─────────────────────

    [Fact(Skip = "需要真实 HALCON Runtime / License（计划 §9.1 Integration，有环境时执行）。")]
    [Trait("Category", "RequiresRuntime")]
    public void Run_OnRealImage_RequiresHalconRuntime()
    {
    }

    // ── 工具 ────────────────────────────────────────────────────────────

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
