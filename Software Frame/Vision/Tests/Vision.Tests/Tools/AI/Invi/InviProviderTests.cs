// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：InviProviderTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Tests.Tools、Vision.Tests.Tools.AI、System.Text.Json、Vision.AI.Invi、Vision.AI.Invi.Native、Vision.Core.AI。导入不代表所有类型都会在运行时加载。
using Vision.Tests.Tools;
using Vision.Tests.Tools.AI;
// ─────────────────────────────────────────────────────────────────────────────
// P6 验收测试 —— Invi Provider（计划 §P6「加载/推理/释放行为独立验证」）。
// 生命周期场景延续 P10 现有思路（创建失败、失败回滚、重复幂等、释放失败保状态、
// Dispose 幂等），断言对象换成新边界（Provider / Session / Helper）。
// 推理 JSON 由 fake 构造，无需真实 DLL / GPU / 模型。
// ─────────────────────────────────────────────────────────────────────────────

using System.Text.Json;
using Vision.AI.Invi;
using Vision.AI.Invi.Native;
using Vision.Core.AI;
using Xunit;

namespace Vision.Tests.Tools.AI.Invi;

/// <summary>Invi Provider / Session / Helper 生命周期与转换测试。</summary>
[Trait("Category", "Contract")]
public sealed class InviProviderTests : IDisposable
{
    private readonly List<FakeInviNativeApi> _fakes = [];

    /// <summary>
    /// 验证 Dispose 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 FreeJson；具体条件和先后次序见方法体。
    /// </summary>
    public void Dispose()
    {
        foreach (var fake in _fakes)
            fake.FreeJson();
    }

    /// <summary>
    /// 验证 NewFake 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Add；具体条件和先后次序见方法体。
    /// </summary>
    private FakeInviNativeApi NewFake()
    {
        var fake = new FakeInviNativeApi();
        _fakes.Add(fake);
        return fake;
    }

    /// <summary>
    /// 验证 TestImage 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 WhiteRectangle；具体条件和先后次序见方法体。
    /// </summary>
    private static Vision.Core.Imaging.VisionImage TestImage() =>
        SyntheticImages.WhiteRectangle(64, 64, 10, 10, 30, 30);

    /// <summary>
    /// 验证 DetectionJson 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Serialize；具体条件和先后次序见方法体。
    /// </summary>
    private static string DetectionJson() => JsonSerializer.Serialize(new[]
    {
        new
        {
            class_id = 1,
            class_name = "defect",
            area = 400.0,
            confidence = 0.8,
            bd_rect = new { x = 10, y = 10, width = 20, height = 20 },
            contour_points = new[] { new { x = 10, y = 10 }, new { x = 30, y = 10 }, new { x = 30, y = 30 } },
        },
        new
        {
            class_id = 2,
            class_name = "scratch",
            area = 25.0,
            confidence = 0.6,
            bd_rect = new { x = 50, y = 50, width = 5, height = 5 },
            contour_points = new[] { new { x = 50, y = 50 } },
        },
    });

    // ── Provider 声明 ─────────────────────────────────────────────

    [Fact]
    public void Provider_DeclaresNameAndCapabilities_WithoutAnomaly()
    {
        var provider = new InviProvider(() => NewFake());
        Assert.Equal("Invi", provider.Name);
        Assert.True(provider.Capabilities.HasFlag(AICapabilities.Detection));
        Assert.True(provider.Capabilities.HasFlag(AICapabilities.Segmentation));
        Assert.True(provider.Capabilities.HasFlag(AICapabilities.Classification));
        Assert.False(provider.Capabilities.HasFlag(AICapabilities.AnomalyDetection));
    }

    /// <summary>
    /// 验证 OpenSession_AnomalyType_ThrowsNotSupported 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 NewFake、Throws、OpenSession；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void OpenSession_AnomalyType_ThrowsNotSupported()
    {
        var provider = new InviProvider(() => NewFake());
        Assert.Throws<NotSupportedException>(() =>
            provider.OpenSession(new AIModelRequest { ModelPath = "m", ModelType = AIModelType.AnomalyDetection }));
    }

    // ── Helper 生命周期（P10 思路延续） ─────────────────────────────

    [Fact]
    public void Helper_ManagerCreationFailure_ThrowsWithContext()
    {
        var fake = NewFake();
        fake.ManagerCreationThrows = true;
        var ex = Assert.Throws<InvalidOperationException>(() => new InviHelper(fake));
        Assert.Contains("CreateInferManager", ex.Message);
        Assert.Equal(1, fake.DisposeCalls);
    }

    /// <summary>
    /// 验证 Helper_ZeroManagerHandle_ThrowsAndDisposesNative 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 NewFake、Throws、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Helper_ZeroManagerHandle_ThrowsAndDisposesNative()
    {
        var fake = NewFake();
        fake.ManagerHandleIsZero = true;
        Assert.Throws<InvalidOperationException>(() => new InviHelper(fake));
        Assert.Equal(1, fake.DisposeCalls);
    }

    /// <summary>
    /// 验证 Helper_ModelLoadFailure_LeavesNoState_AndRetryWorks 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 NewFake、Add、Throws、InitModel、False；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Helper_ModelLoadFailure_LeavesNoState_AndRetryWorks()
    {
        var fake = NewFake();
        fake.CreateModelFailures.Add("m.model");
        using var helper = new InviHelper(fake);
        Assert.Throws<InvalidOperationException>(() => helper.InitModel("m.model", 2));
        Assert.False(helper.IsModelLoaded("m.model"));

        fake.CreateModelFailures.Clear();
        helper.InitModel("m.model", 2);
        Assert.True(helper.IsModelLoaded("m.model"));
        Assert.True(helper.CreateInferAPI("m.model", 2)); // 重复加载幂等，不再调 native。
        Assert.Equal(2, fake.CreatedModels.Count);
    }

    /// <summary>
    /// 验证 Helper_ReleaseFailure_KeepsState 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 NewFake、Add、InitModel、Throws、ReleaseModel；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Helper_ReleaseFailure_KeepsState()
    {
        var fake = NewFake();
        fake.DestroyModelFailures.Add("m.model");
        using var helper = new InviHelper(fake);
        helper.InitModel("m.model", 2);
        Assert.Throws<InvalidOperationException>(() => helper.ReleaseModel("m.model"));
        Assert.True(helper.IsModelLoaded("m.model"));
        Assert.False(helper.ReleaseModel("unknown.model")); // 未加载路径：返回 false，不碰 native。
        Assert.DoesNotContain("unknown.model", fake.DestroyedModels);
    }

    /// <summary>
    /// 验证 Helper_Dispose_IsIdempotent 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 NewFake、InitModel、Dispose、Equal、True；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Helper_Dispose_IsIdempotent()
    {
        var fake = NewFake();
        var helper = new InviHelper(fake);
        helper.InitModel("m.model", 2);
        helper.Dispose();
        helper.Dispose();
        Assert.Equal(1, fake.DestroyManagerCalls);
        Assert.True(helper.IsDisposed);
        Assert.Throws<ObjectDisposedException>(() => helper.CreateInferAPI("x", 2));
    }

    // ── 会话：加载/推理/释放 ───────────────────────────────────────

    [Fact]
    public void OpenSession_Detection_Succeeds_AndRunMapsResults()
    {
        var fake = NewFake();
        fake.InferJson = DetectionJson();
        var provider = new InviProvider(() => fake);

        using var session = provider.OpenSession(new AIModelRequest { ModelPath = "m.model", ModelType = AIModelType.Detection });
        Assert.True(session.IsLoaded);
        Assert.Equal(AIModelType.Detection, session.ModelType);
        Assert.Equal(AICapabilities.Detection, session.Capabilities);

        var result = session.Run(new AIInferenceRequest { Image = TestImage(), ScoreThreshold = 0.7 });
        Assert.True(result.Success, result.Message);
        var detection = Assert.IsType<AIDetectionResult>(result);
        Assert.Equal(2, detection.Boxes.Count);
        Assert.Equal("defect", detection.Boxes[0].Label);
        Assert.Equal(new Vision.Core.Geometry.Rect2D(10, 10, 20, 20), detection.Boxes[0].Box);
        Assert.Equal(0.7, fake.LastThreshold);
        Assert.Equal(64, fake.LastWidth);
    }

    /// <summary>
    /// 验证 OpenSession_ManagerFailure_Throws_AndLeavesNoSession 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 NewFake、Throws、OpenSession；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void OpenSession_ManagerFailure_Throws_AndLeavesNoSession()
    {
        var fake = NewFake();
        fake.ManagerCreationThrows = true;
        var provider = new InviProvider(() => fake);
        Assert.Throws<InvalidOperationException>(() =>
            provider.OpenSession(new AIModelRequest { ModelPath = "m", ModelType = AIModelType.Detection }));
    }

    /// <summary>
    /// 验证 CloseSession_Failure_Throws_AndKeepsLoadedState 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 NewFake、OpenSession、Add、Throws、CloseSession；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void CloseSession_Failure_Throws_AndKeepsLoadedState()
    {
        var fake = NewFake();
        var provider = new InviProvider(() => fake);
        var session = provider.OpenSession(new AIModelRequest { ModelPath = "m.model", ModelType = AIModelType.Detection });

        fake.DestroyModelFailures.Add("m.model");
        Assert.Throws<InvalidOperationException>(() => provider.CloseSession(session));
        Assert.True(session.IsLoaded);

        provider.CloseSession(null); // null 无操作，不抛。
        session.Dispose();
        session.Dispose(); // 幂等。
    }

    /// <summary>
    /// 验证 Session_RunAfterDispose_Throws 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 NewFake、OpenSession、Dispose、Throws、Run；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Session_RunAfterDispose_Throws()
    {
        var fake = NewFake();
        var provider = new InviProvider(() => fake);
        var session = provider.OpenSession(new AIModelRequest { ModelPath = "m.model", ModelType = AIModelType.Detection });
        session.Dispose();
        Assert.Throws<InvalidOperationException>(() =>
            session.Run(new AIInferenceRequest { Image = TestImage() }));
    }

    /// <summary>
    /// 验证 Session_RunEmptyImage_ReturnsFailedResult 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 NewFake、OpenSession、Run、False；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Session_RunEmptyImage_ReturnsFailedResult()
    {
        var fake = NewFake();
        var provider = new InviProvider(() => fake);
        using var session = provider.OpenSession(new AIModelRequest { ModelPath = "m.model", ModelType = AIModelType.Detection });
        var result = session.Run(new AIInferenceRequest());
        Assert.False(result.Success);
    }

    // ── 转换器纯映射 ───────────────────────────────────────────────

    [Fact]
    public void Converter_MapsDetectionSegmentationClassification()
    {
        var items = new List<InviResult>
        {
            new() { class_id = 1, class_name = "defect", confidence = 0.8, bd_rect = new Rect { x = 1, y = 2, width = 10, height = 20 }, contour_points = [new PointD { x = 1, y = 2 }] },
        };

        var detection = InviResultConverter.ToDetection(items);
        Assert.Single(detection.Boxes);
        Assert.Equal(new Vision.Core.Geometry.Rect2D(1, 2, 10, 20), detection.Boxes[0].Box);

        var segmentation = InviResultConverter.ToSegmentation(items);
        Assert.Single(segmentation.Items);
        Assert.Single(segmentation.Items[0].Polygon);

        var classification = InviResultConverter.ToClassification(items);
        Assert.Equal("defect", classification.TopLabel);
        Assert.Equal(0.8, classification.TopScore);

        Assert.Empty(InviResultConverter.ToDetection([]).Boxes);
        Assert.Empty(InviResultConverter.ToClassification([]).Scores);
    }

    // ── 集成测试占位 ───────────────────────────────────────────────

    [Fact(Skip = "需要真实 INVI API.dll、GPU 与模型（计划 §9.1 Integration）。")]
    [Trait("Category", "RequiresRuntime")]
    public void Run_AgainstRealDll_RequiresRuntime()
    {
    }
}
