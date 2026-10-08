// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：StandardToolContractTests、Case。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Tests.Tools、Vision.Core.Results、Xunit。导入不代表所有类型都会在运行时加载。
using Vision.Tests.Tools;
// ─────────────────────────────────────────────────────────────────────────────
// P10 —— 标准 Tool 统一契约套件（14 个 P3/P4 传统 Tool × 同一断言形状）。
// AI 四 Tool 由 P5 套件覆盖（需 Provider 注入，不在本表）；
// HALCON/OpenCV 语义对比由 Comparison 覆盖（业务容差口径）。
// 全程无 Runtime 依赖（Run(null) 只走校验失败路径）。
// ─────────────────────────────────────────────────────────────────────────────

using Vision.Core.Results;
using Xunit;

namespace Vision.Tests.Tools.Contracts;

/// <summary>统一契约：一个条目 = 一次工厂 + 默认有效性 + 可选的致 invalid 修改器。</summary>
[Trait("Category", "Contract")]
public sealed class StandardToolContractTests
{
    /// <summary>
    /// Case 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// </summary>
    private sealed record Case(
        string Name,
        Func<(IDisposable Tool, Func<ValidationResult> Validate, Func<ToolResult> RunInvalid, Func<object> Params, Type ResultType)> Create,
        bool DefaultValid,
        Action<object>? MakeInvalid);

    private static readonly IReadOnlyList<Case> Cases = new List<Case>
    {
        new("Halcon.Blob",
            () => { var t = new Vision.Tools.Halcon.Inspection.BlobTool(); return ((IDisposable)t, (Func<ValidationResult>)(() => t.Validate()), (Func<ToolResult>)(() => t.Run(null)), (Func<object>)(() => t.Parameters), typeof(Vision.Tools.Halcon.Inspection.BlobResult)); },
            true,
            p => { var x = (Vision.Tools.Halcon.Inspection.BlobParameters)p; x.MinGray = 200; x.MaxGray = 100; }),
        new("Halcon.ShapeMatch",
            () => { var t = new Vision.Tools.Halcon.Matching.ShapeMatchTool(); return ((IDisposable)t, (Func<ValidationResult>)(() => t.Validate()), (Func<ToolResult>)(() => t.Run(null)), (Func<object>)(() => t.Parameters), typeof(Vision.Tools.Halcon.Matching.ShapeMatchResult)); },
            false, // 未训练模型（MODEL_NOT_TRAINED）。
            p => ((Vision.Tools.Halcon.Matching.ShapeMatchParameters)p).MinScore = 2),
        new("Halcon.FindLine",
            () => { var t = new Vision.Tools.Halcon.Measurement.FindLineTool(); return ((IDisposable)t, (Func<ValidationResult>)(() => t.Validate()), (Func<ToolResult>)(() => t.Run(null)), (Func<object>)(() => t.Parameters), typeof(Vision.Tools.Halcon.Measurement.FindLineResult)); },
            true,
            p => ((Vision.Tools.Halcon.Measurement.FindLineParameters)p).CaliperCount = 1),
        new("Halcon.Barcode",
            () => { var t = new Vision.Tools.Halcon.Identification.BarcodeTool(); return ((IDisposable)t, (Func<ValidationResult>)(() => t.Validate()), (Func<ToolResult>)(() => t.Run(null)), (Func<object>)(() => t.Parameters), typeof(Vision.Tools.Halcon.Identification.BarcodeResult)); },
            true, null),
        new("Halcon.DataCode",
            () => { var t = new Vision.Tools.Halcon.Identification.DataCodeTool(); return ((IDisposable)t, (Func<ValidationResult>)(() => t.Validate()), (Func<ToolResult>)(() => t.Run(null)), (Func<object>)(() => t.Parameters), typeof(Vision.Tools.Halcon.Identification.DataCodeResult)); },
            true, null),
        new("Halcon.NPointsCalibration",
            () => { var t = new Vision.Tools.Halcon.Calibration.NPointsCalibrationTool(); return ((IDisposable)t, (Func<ValidationResult>)(() => t.Validate()), (Func<ToolResult>)(() => t.Run(null)), (Func<object>)(() => t.Parameters), typeof(Vision.Tools.Halcon.Calibration.NPointsCalibrationResult)); },
            false, // 默认 0 点（POINTS_TOO_FEW）。
            null),
        new("Halcon.Fixture",
            () => { var t = new Vision.Tools.Halcon.Coordinate.FixtureTool(); return ((IDisposable)t, (Func<ValidationResult>)(() => t.Validate()), (Func<ToolResult>)(() => t.Run(null)), (Func<object>)(() => t.Parameters), typeof(Vision.Tools.Halcon.Coordinate.FixtureResult)); },
            true,
            p => ((Vision.Tools.Halcon.Coordinate.FixtureParameters)p).Scale = 0),
        new("Halcon.ImageFile",
            () => { var t = new Vision.Tools.Halcon.Acquisition.ImageFileTool(); return ((IDisposable)t, (Func<ValidationResult>)(() => t.Validate()), (Func<ToolResult>)(() => t.Run(null)), (Func<object>)(() => t.Parameters), typeof(Vision.Tools.Halcon.Acquisition.ImageFileResult)); },
            false, // 默认空路径（PATH_EMPTY）。
            null),
        new("OpenCv.Threshold",
            () => { var t = new Vision.Tools.OpenCv.Processing.ThresholdTool(); return ((IDisposable)t, (Func<ValidationResult>)(() => t.Validate()), (Func<ToolResult>)(() => t.Run(null)), (Func<object>)(() => t.Parameters), typeof(Vision.Tools.OpenCv.Processing.ThresholdResult)); },
            true,
            p => { var x = (Vision.Tools.OpenCv.Processing.ThresholdParameters)p; x.MinGray = 200; x.MaxGray = 100; }),
        new("OpenCv.Morphology",
            () => { var t = new Vision.Tools.OpenCv.Processing.MorphologyTool(); return ((IDisposable)t, (Func<ValidationResult>)(() => t.Validate()), (Func<ToolResult>)(() => t.Run(null)), (Func<object>)(() => t.Parameters), typeof(Vision.Tools.OpenCv.Processing.MorphologyResult)); },
            true,
            p => ((Vision.Tools.OpenCv.Processing.MorphologyParameters)p).KernelSize = 4),
        new("OpenCv.Blob",
            () => { var t = new Vision.Tools.OpenCv.Inspection.BlobTool(); return ((IDisposable)t, (Func<ValidationResult>)(() => t.Validate()), (Func<ToolResult>)(() => t.Run(null)), (Func<object>)(() => t.Parameters), typeof(Vision.Tools.OpenCv.Inspection.BlobResult)); },
            true,
            p => { var x = (Vision.Tools.OpenCv.Inspection.BlobParameters)p; x.MinGray = 200; x.MaxGray = 100; }),
        new("OpenCv.TemplateMatch",
            () => { var t = new Vision.Tools.OpenCv.Matching.TemplateMatchTool(); return ((IDisposable)t, (Func<ValidationResult>)(() => t.Validate()), (Func<ToolResult>)(() => t.Run(null)), (Func<object>)(() => t.Parameters), typeof(Vision.Tools.OpenCv.Matching.TemplateMatchResult)); },
            false, // 默认空模板（TEMPLATE_EMPTY）。
            null),
        new("OpenCv.FindLine",
            () => { var t = new Vision.Tools.OpenCv.Measurement.FindLineTool(); return ((IDisposable)t, (Func<ValidationResult>)(() => t.Validate()), (Func<ToolResult>)(() => t.Run(null)), (Func<object>)(() => t.Parameters), typeof(Vision.Tools.OpenCv.Measurement.FindLineResult)); },
            true,
            p => { var x = (Vision.Tools.OpenCv.Measurement.FindLineParameters)p; x.LineEnd = x.LineStart; }),
        new("OpenCv.FindCircle",
            () => { var t = new Vision.Tools.OpenCv.Measurement.FindCircleTool(); return ((IDisposable)t, (Func<ValidationResult>)(() => t.Validate()), (Func<ToolResult>)(() => t.Run(null)), (Func<object>)(() => t.Parameters), typeof(Vision.Tools.OpenCv.Measurement.FindCircleResult)); },
            true,
            p => { var x = (Vision.Tools.OpenCv.Measurement.FindCircleParameters)p; x.RadiusMax = 5; }),
    };

    public static IEnumerable<object[]> AllCases => Cases.Select(c => new object[] { c.Name });

    /// <summary>
    /// 测试相关入口 Find；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 First；具体条件和先后次序见方法体。
    /// </summary>
    private static Case Find(string name) => Cases.First(c => c.Name == name);

    /// <summary>
    /// 测试相关入口 DefaultValidation_MatchesExpectation；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 nameof、Find、Create、Equal、Validate；具体条件和先后次序见方法体。
    /// </summary>
    [Theory]
    [MemberData(nameof(AllCases))]
    public void DefaultValidation_MatchesExpectation(string name)
    {
        var entry = Find(name);
        var created = entry.Create();
        using (created.Tool)
        {
            Assert.Equal(entry.DefaultValid, created.Validate().IsValid);
        }
    }

    /// <summary>
    /// 测试相关入口 InvalidParams_ProduceFailedRun_WithoutThrowing；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 nameof、Find、Create、MakeInvalid、Params；具体条件和先后次序见方法体。
    /// </summary>
    [Theory]
    [MemberData(nameof(AllCases))]
    public void InvalidParams_ProduceFailedRun_WithoutThrowing(string name)
    {
        var entry = Find(name);
        if (entry.MakeInvalid == null)
            return; // 该 Tool 无参数致 invalid 路径（默认态已覆盖）。
        // 工厂契约：Params() 返回 Tool 内部 Parameters 同一引用，直接改即生效。
        var fresh = entry.Create();
        try
        {
            entry.MakeInvalid(fresh.Params());
            Assert.False(fresh.Validate().IsValid);
            ToolContractAsserts.IsFailedResult(fresh.RunInvalid());
        }
        finally
        {
            fresh.Tool.Dispose();
        }
    }

    /// <summary>
    /// 测试相关入口 Results_AreClean；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 nameof、Create、Find、IsCleanResult；具体条件和先后次序见方法体。
    /// </summary>
    [Theory]
    [MemberData(nameof(AllCases))]
    public void Results_AreClean(string name)
    {
        var created = Find(name).Create();
        using (created.Tool)
        {
            ToolContractAsserts.IsCleanResult(created.ResultType);
        }
    }

    /// <summary>
    /// 测试相关入口 Parameters_RoundTrip；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 nameof、Create、Find、RoundTripsThroughJson、Params；具体条件和先后次序见方法体。
    /// </summary>
    [Theory]
    [MemberData(nameof(AllCases))]
    public void Parameters_RoundTrip(string name)
    {
        var created = Find(name).Create();
        using (created.Tool)
        {
            ToolContractAsserts.RoundTripsThroughJson(created.Params());
        }
    }

    /// <summary>
    /// 测试相关入口 CaseTable_CoversFourteenTools；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void CaseTable_CoversFourteenTools()
    {
        Assert.Equal(14, Cases.Count);
    }
}
