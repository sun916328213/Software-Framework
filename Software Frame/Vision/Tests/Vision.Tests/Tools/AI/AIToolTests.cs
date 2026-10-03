// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：AIToolTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Tests.Tools、System.Reflection、System.Text.Json、System.Threading、Vision.Core.AI、Vision.Tools.AI.AnomalyDetection。导入不代表所有类型都会在运行时加载。
using Vision.Tests.Tools;
// ─────────────────────────────────────────────────────────────────────────────
// P5 验收测试 —— 统一 AI Tool 层（计划 §9.2「P5 | Vision.Tools.AI 使用 fake
// Provider 完整测试 Classification/Detection/Segmentation/Anomaly Tool」）。
//
// 执行边界：全部用内存 fake，无真实 SDK / DLL / GPU / License。
// 覆盖：四 Tool 全流程、参数校验、能力声明、会话生命周期（加载失败回滚、
// 释放一次、Dispose 幂等）、品牌无关（同一 Tool 可换 Provider）。
// ─────────────────────────────────────────────────────────────────────────────

using System.Reflection;
using System.Text.Json;
using System.Threading;
using Vision.Core.AI;
using Vision.Tools.AI.AnomalyDetection;
using Vision.Tools.AI.Classification;
using Vision.Tools.AI.Detection;
using Vision.Tools.AI.Segmentation;
using Xunit;

namespace Vision.Tests.Tools.AI;

/// <summary>P5 统一 AI Tool 的 fake 全流程测试。</summary>
[Trait("Category", "Contract")]
public sealed class AIToolTests
{
    /// <summary>
    /// 验证 TestImage 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 WhiteRectangle；具体条件和先后次序见方法体。
    /// </summary>
    private static Vision.Core.Imaging.VisionImage TestImage() =>
        SyntheticImages.WhiteRectangle(64, 64, 10, 10, 30, 30);

    /// <summary>
    /// 验证 Provider 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// </summary>
    private static FakeAIProvider Provider(string name = "Fake") => new() { Name = name };

    // ── 四 Tool 全流程 ──────────────────────────────────────────────

    [Fact]
    public void Classification_FullFlow_WithFakeProvider()
    {
        using var tool = new ClassificationTool();
        tool.Provider = Provider();
        tool.Parameters.ModelPath = "cls.model";
        Assert.True(tool.Validate().IsValid);

        tool.LoadModel();
        Assert.True(tool.IsModelLoaded);

        var result = tool.Run(TestImage());
        Assert.True(result.Success, result.Message);
        var typed = Assert.IsType<AIClassificationResult>(result);
        Assert.Equal("OK", typed.TopLabel);
        Assert.Equal(0.9, typed.TopScore);
        Assert.NotNull(typed.Validation);
    }

    /// <summary>
    /// 验证 Run_WhenValidationFails_ResetsLastElapsedMs 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Provider、LoadModel、True、Run、TestImage；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Run_WhenValidationFails_ResetsLastElapsedMs()
    {
        var provider = Provider();
        provider.OpenSessionFunc = request => new FakeAIModelSession(
            request.ModelPath, request.ModelType, provider.Capabilities)
        {
            RunFunc = _ => new AIClassificationResult { ElapsedMs = 37 },
        };
        using var tool = new ClassificationTool { Provider = provider };
        tool.Parameters.ModelPath = "cls.model";
        tool.LoadModel();
        Assert.True(tool.Run(TestImage()).Success);
        Assert.Equal(37, tool.LastElapsedMs);

        tool.Parameters.ModelPath = string.Empty;
        var result = tool.Run(TestImage());

        Assert.False(result.Success);
        Assert.Equal(0, result.ElapsedMs);
        Assert.Equal(0, tool.LastElapsedMs);
    }

    /// <summary>
    /// 验证 Detection_FullFlow_WithFakeProvider 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Provider、LoadModel、Run、TestImage、True；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Detection_FullFlow_WithFakeProvider()
    {
        using var tool = new DetectionTool();
        tool.Provider = Provider();
        tool.Parameters.ModelPath = "det.model";
        tool.LoadModel();

        var result = tool.Run(TestImage());
        Assert.True(result.Success, result.Message);
        var typed = Assert.IsType<AIDetectionResult>(result);
        Assert.Equal(2, typed.Boxes.Count);
        Assert.Equal("defect", typed.Boxes[0].Label);
    }

    /// <summary>
    /// 验证 Segmentation_FullFlow_WithFakeProvider 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Provider、LoadModel、Run、TestImage、True；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Segmentation_FullFlow_WithFakeProvider()
    {
        using var tool = new SegmentationTool();
        tool.Provider = Provider();
        tool.Parameters.ModelPath = "seg.model";
        tool.LoadModel();

        var result = tool.Run(TestImage());
        Assert.True(result.Success, result.Message);
        var typed = Assert.IsType<AISegmentationResult>(result);
        Assert.Equal(2, typed.Items.Count);
    }

    /// <summary>
    /// 验证 AnomalyDetection_FullFlow_WithFakeProvider 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Provider、LoadModel、Run、TestImage、True；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void AnomalyDetection_FullFlow_WithFakeProvider()
    {
        using var tool = new AnomalyDetectionTool();
        tool.Provider = Provider();
        tool.Parameters.ModelPath = "anomaly.model";
        tool.LoadModel();

        var result = tool.Run(TestImage());
        Assert.True(result.Success, result.Message);
        var typed = Assert.IsType<AIAnomalyResult>(result);
        Assert.True(typed.IsAnomaly);
        Assert.Equal(0.66, typed.Score);
    }

    // ── Tool 层后过滤（统一语义，不依赖 Provider） ────────────────────

    [Fact]
    public void Detection_MinAreaFilter_RemovesSmallBoxes()
    {
        using var tool = new DetectionTool();
        tool.Provider = Provider();
        tool.Parameters.ModelPath = "det.model";
        tool.Parameters.MinArea = 100; // 大框 400 留，小框 25 去。
        tool.LoadModel();

        var result = tool.Run(TestImage());
        Assert.True(result.Success, result.Message);
        var single = Assert.Single(result.Boxes);
        Assert.Equal("defect", single.Label);
    }

    /// <summary>
    /// 验证 Detection_MaxResults_CapsToTopBoxes 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Provider、LoadModel、Run、TestImage、True；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Detection_MaxResults_CapsToTopBoxes()
    {
        using var tool = new DetectionTool();
        tool.Provider = Provider();
        tool.Parameters.ModelPath = "det.model";
        tool.Parameters.MaxResults = 1;
        tool.LoadModel();

        var result = tool.Run(TestImage());
        Assert.True(result.Success, result.Message);
        var single = Assert.Single(result.Boxes);
        Assert.Equal(0.8, single.Score);
    }

    /// <summary>
    /// 验证 Segmentation_MinAreaFilter_RemovesSmallItems 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Provider、LoadModel、Run、TestImage、True；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Segmentation_MinAreaFilter_RemovesSmallItems()
    {
        using var tool = new SegmentationTool();
        tool.Provider = Provider();
        tool.Parameters.ModelPath = "seg.model";
        tool.Parameters.MinArea = 100; // 900 留，16 去。
        tool.LoadModel();

        var result = tool.Run(TestImage());
        Assert.True(result.Success, result.Message);
        var single = Assert.Single(result.Items);
        Assert.Equal("defect", single.Label);
    }

    /// <summary>
    /// 验证 InferenceRequest_ForwardsThresholdAndMaxResults 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Provider、LoadModel、Run、TestImage、NotNull；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void InferenceRequest_ForwardsThresholdAndMaxResults()
    {
        FakeAIModelSession? session = null;
        var provider = Provider();
        provider.OpenSessionFunc = request =>
        {
            session = new FakeAIModelSession(request.ModelPath, request.ModelType, provider.Capabilities);
            return session;
        };

        using var tool = new DetectionTool();
        tool.Provider = provider;
        tool.Parameters.ModelPath = "det.model";
        tool.Parameters.ScoreThreshold = 0.7;
        tool.Parameters.MaxResults = 5;
        tool.LoadModel();
        tool.Run(TestImage());

        Assert.NotNull(session?.LastRequest);
        Assert.Equal(0.7, session!.LastRequest!.ScoreThreshold);
        Assert.Equal(5, session.LastRequest.MaxResults);
        Assert.Equal("det.model", provider.LastRequest?.ModelPath);
        Assert.Equal(AIModelType.Detection, provider.LastRequest?.ModelType);
    }

    // ── Validate ────────────────────────────────────────────────────

    /// <summary>
    /// 验证 Validate_WithoutProvider_ReportsMissing 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Validate、False、Contains；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Validate_WithoutProvider_ReportsMissing()
    {
        using var tool = new DetectionTool();
        tool.Parameters.ModelPath = "det.model";
        var validation = tool.Validate();
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Issues, i => i.Code == "PROVIDER_MISSING");
    }

    /// <summary>
    /// 验证 Validate_EmptyModelPath_ReportsError 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Provider、Validate、False、Contains；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Validate_EmptyModelPath_ReportsError()
    {
        using var tool = new DetectionTool();
        tool.Provider = Provider();
        var validation = tool.Validate();
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Issues, i => i.Code == "MODEL_PATH_EMPTY");
    }

    /// <summary>
    /// 验证 Validate_ScoreThresholdAboveOne_ReportsError 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Provider、Validate、False、Contains；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Validate_ScoreThresholdAboveOne_ReportsError()
    {
        using var tool = new DetectionTool();
        tool.Provider = Provider();
        tool.Parameters.ModelPath = "det.model";
        tool.Parameters.ScoreThreshold = 1.5;
        var validation = tool.Validate();
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Issues, i => i.Code == "SCORE_THRESHOLD");
    }

    /// <summary>
    /// 验证 Validate_UnsupportedCapability_ReportsError 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Provider、Validate、False、Contains；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Validate_UnsupportedCapability_ReportsError()
    {
        using var tool = new DetectionTool();
        var provider = Provider();
        provider.Capabilities = AICapabilities.Classification; // 只会分类。
        tool.Provider = provider;
        tool.Parameters.ModelPath = "det.model";
        var validation = tool.Validate();
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Issues, i => i.Code == "CAPABILITY_UNSUPPORTED");
    }

    /// <summary>
    /// 验证 Validate_UnavailableProvider_WarnsButStaysValid 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Provider、Validate、True、Contains；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Validate_UnavailableProvider_WarnsButStaysValid()
    {
        using var tool = new DetectionTool();
        var provider = Provider();
        provider.IsAvailable = false;
        tool.Provider = provider;
        tool.Parameters.ModelPath = "det.model";
        var validation = tool.Validate();
        Assert.True(validation.IsValid);
        Assert.Contains(validation.Issues, i => i.Code == "PROVIDER_UNAVAILABLE");
    }

    // ── 生命周期：加载失败回滚、释放一次、Dispose 幂等 ───────────────

    [Fact]
    public void LoadModel_WhenProviderThrows_RollsBackAndAllowsRetry()
    {
        var provider = Provider();
        provider.OpenSessionFunc = _ => throw new InvalidOperationException("native 加载失败");

        using var tool = new DetectionTool();
        tool.Provider = provider;
        tool.Parameters.ModelPath = "det.model";

        var ex = Assert.Throws<InvalidOperationException>(() => tool.LoadModel());
        Assert.Contains("native 加载失败", ex.Message);
        Assert.Null(tool.Session);
        Assert.False(tool.IsModelLoaded);

        // 回滚干净：修好后可直接重试，无需重建 Tool。
        provider.OpenSessionFunc = null;
        tool.LoadModel();
        Assert.True(tool.IsModelLoaded);
        Assert.Equal(2, provider.OpenSessionCalls);
    }

    /// <summary>
    /// 验证 LoadModel_WhenSessionTypeMismatches_ThrowsAndLeavesNoSession 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Provider、Throws、LoadModel、Null、False；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void LoadModel_WhenSessionTypeMismatches_ThrowsAndLeavesNoSession()
    {
        var provider = Provider();
        provider.OpenSessionFunc = request =>
            new FakeAIModelSession(request.ModelPath, AIModelType.Classification, provider.Capabilities);

        using var tool = new DetectionTool();
        tool.Provider = provider;
        tool.Parameters.ModelPath = "det.model";

        Assert.Throws<InvalidOperationException>(() => tool.LoadModel());
        Assert.Null(tool.Session);
        Assert.False(tool.IsModelLoaded);
    }

    /// <summary>
    /// 验证 LoadModel_WhenProviderReturnsUnloadedSession_ClosesItAndAllowsRetry 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Provider、MarkClosed、Throws、LoadModel、Null；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void LoadModel_WhenProviderReturnsUnloadedSession_ClosesItAndAllowsRetry()
    {
        var provider = Provider();
        provider.OpenSessionFunc = request =>
        {
            var session = new FakeAIModelSession(request.ModelPath, request.ModelType, provider.Capabilities);
            session.MarkClosed();
            return session;
        };

        using var tool = new DetectionTool();
        tool.Provider = provider;
        tool.Parameters.ModelPath = "det.model";

        Assert.Throws<InvalidOperationException>(() => tool.LoadModel());
        Assert.Null(tool.Session);
        Assert.False(tool.IsModelLoaded);
        Assert.Equal(1, provider.CloseSessionCalls);

        provider.OpenSessionFunc = null;
        tool.LoadModel();
        Assert.True(tool.IsModelLoaded);
    }

    /// <summary>
    /// 验证 LoadModel_WhenInvalid_ThrowsWithoutTouchingProvider 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Provider、Throws、LoadModel、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void LoadModel_WhenInvalid_ThrowsWithoutTouchingProvider()
    {
        var provider = Provider();
        using var tool = new DetectionTool();
        tool.Provider = provider; // ModelPath 为空。
        Assert.Throws<InvalidOperationException>(() => tool.LoadModel());
        Assert.Equal(0, provider.OpenSessionCalls);
    }

    /// <summary>
    /// 验证 UnloadModel_ClosesOnce_AndDisposeIsIdempotent 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Provider、LoadModel、UnloadModel、Null、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void UnloadModel_ClosesOnce_AndDisposeIsIdempotent()
    {
        var provider = Provider();
        using var tool = new DetectionTool();
        tool.Provider = provider;
        tool.Parameters.ModelPath = "det.model";
        tool.LoadModel();

        tool.UnloadModel();
        Assert.Null(tool.Session);
        Assert.Equal(1, provider.CloseSessionCalls);

        tool.UnloadModel(); // 无会话：无操作。
        tool.Dispose();
        tool.Dispose(); // 幂等：不再关闭。
        Assert.Equal(1, provider.CloseSessionCalls);
    }

    /// <summary>
    /// 验证 Run_WithoutSession_ReturnsFailedResult 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Provider、Run、TestImage、False、Contains；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Run_WithoutSession_ReturnsFailedResult()
    {
        using var tool = new DetectionTool();
        tool.Provider = Provider();
        tool.Parameters.ModelPath = "det.model";
        var result = tool.Run(TestImage());
        Assert.False(result.Success);
        Assert.Contains("LoadModel", result.Message);
    }

    /// <summary>
    /// 验证 Run_AfterProviderSwitch_DoesNotUseOldSession 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Provider、LoadModel、False、Run、TestImage；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Run_AfterProviderSwitch_DoesNotUseOldSession()
    {
        var original = Provider("Original");
        using var tool = new DetectionTool();
        tool.Provider = original;
        tool.Parameters.ModelPath = "det.model";
        tool.LoadModel();

        tool.Provider = Provider("Replacement");
        Assert.False(tool.IsModelLoaded);
        var result = tool.Run(TestImage());

        Assert.False(result.Success);
        Assert.Contains("Provider 已切换", result.Message);
        Assert.Equal(0, original.CloseSessionCalls);
    }

    /// <summary>
    /// 验证 Run_AfterModelPathChange_RequiresReloadAndThenUsesNewModel 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Provider、LoadModel、False、Run、TestImage；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Run_AfterModelPathChange_RequiresReloadAndThenUsesNewModel()
    {
        var provider = Provider();
        using var tool = new DetectionTool();
        tool.Provider = provider;
        tool.Parameters.ModelPath = "old.model";
        tool.LoadModel();

        tool.Parameters.ModelPath = "new.model";
        Assert.False(tool.IsModelLoaded);
        var staleResult = tool.Run(TestImage());
        Assert.False(staleResult.Success);
        Assert.Contains("重新加载", staleResult.Message);

        tool.LoadModel();
        Assert.True(tool.IsModelLoaded);
        Assert.Equal("new.model", provider.LastRequest?.ModelPath);
        Assert.Equal(1, provider.CloseSessionCalls);
    }

    /// <summary>
    /// 验证 Run_WhenSessionReturnsWrongType_ReportsMismatch 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Provider、Sleep、LoadModel、Run、TestImage；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Run_WhenSessionReturnsWrongType_ReportsMismatch()
    {
        var provider = Provider();
        provider.OpenSessionFunc = request =>
        {
            var session = new FakeAIModelSession(request.ModelPath, request.ModelType, provider.Capabilities);
            session.RunFunc = _ =>
            {
                Thread.Sleep(20);
                return new AIClassificationResult(); // 检测会话吐出分类结果。
            };
            return session;
        };

        using var tool = new DetectionTool();
        tool.Provider = provider;
        tool.Parameters.ModelPath = "det.model";
        tool.LoadModel();

        var result = tool.Run(TestImage());
        Assert.False(result.Success);
        Assert.Contains("AIClassificationResult", result.Message);
        Assert.True(result.ElapsedMs > 0);
        Assert.Equal(result.ElapsedMs, tool.LastElapsedMs);
    }

    // ── 品牌无关：同一 Tool 可换 Provider，无品牌分支 ─────────────────

    [Fact]
    public void SameTool_WorksAcrossProviders_WithoutBrandBranching()
    {
        using var tool = new DetectionTool();
        tool.Parameters.ModelPath = "det.model";

        tool.Provider = Provider("Halcon");
        tool.LoadModel();
        Assert.True(tool.Run(TestImage()).Success);
        tool.UnloadModel();

        tool.Provider = Provider("Invi");
        tool.LoadModel();
        Assert.True(tool.Run(TestImage()).Success);

        // Tool 公开成员里没有品牌概念（对应“不直接 if/else 判断厂商品牌”）。
        var brandMembers = typeof(DetectionTool)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Where(m => m.Name.Contains("Brand", StringComparison.OrdinalIgnoreCase))
            .Select(m => m.Name)
            .ToArray();
        Assert.Empty(brandMembers);
    }

    /// <summary>
    /// 验证 Parameters_RoundTripThroughJson 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Deserialize、Serialize、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Parameters_RoundTripThroughJson()
    {
        var detection = new DetectionParameters { ModelPath = "m.model", ScoreThreshold = 0.6, MinArea = 50 };
        var back = JsonSerializer.Deserialize<DetectionParameters>(JsonSerializer.Serialize(detection))!;
        Assert.Equal("m.model", back.ModelPath);
        Assert.Equal(0.6, back.ScoreThreshold);
        Assert.Equal(50, back.MinArea);
    }

    // ── 集成测试占位（RequiresRuntime，不进普通 CI） ──────────────────

    [Fact(Skip = "需要真实 HALCON AI / Aiditron / Invi Runtime、GPU 与模型（计划 §9.1 Integration）。")]
    [Trait("Category", "RequiresRuntime")]
    public void Run_AgainstRealProvider_RequiresRuntime()
    {
    }
}
