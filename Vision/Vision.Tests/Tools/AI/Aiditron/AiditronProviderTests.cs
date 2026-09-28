// ─────────────────────────────────────────────────────────────────────────────
// P6 验收测试 —— Aiditron Provider（补齐旧空分支）。
// 无 SDK 环境：构造/声明/类型映射可测；OpenSession 走真实 InitModel，
// 缺 DLL 时断言“带模型路径上下文的失败”（不伪装状态）；转换器纯映射全覆盖。
// 真实模型推理留 RequiresRuntime。
// ─────────────────────────────────────────────────────────────────────────────

using Vision.AI.Aiditron;
using Vision.Core.AI;
using Xunit;

namespace Vision.Tests.Tools.AI.Aiditron;

/// <summary>Aiditron Provider 声明、无 SDK 行为与转换测试。</summary>
[Trait("Category", "Contract")]
public sealed class AiditronProviderTests
{
    [Fact]
    public void Provider_DeclaresNameAndCapabilities_WithoutAnomaly()
    {
        var provider = new AiditronProvider();
        Assert.Equal("Aiditron", provider.Name);
        Assert.True(provider.Capabilities.HasFlag(AICapabilities.Detection));
        Assert.True(provider.Capabilities.HasFlag(AICapabilities.Segmentation));
        Assert.True(provider.Capabilities.HasFlag(AICapabilities.Classification));
        Assert.False(provider.Capabilities.HasFlag(AICapabilities.AnomalyDetection));
    }

    [Fact]
    public void Provider_IsAvailable_DoesNotThrow()
    {
        var provider = new AiditronProvider();
        _ = provider.IsAvailable; // 有/无 DLL 都是合法值，关键是不抛。
    }

    [Fact]
    public void OpenSession_AnomalyType_ThrowsNotSupported_BeforeTouchingNative()
    {
        var provider = new AiditronProvider();
        Assert.Throws<NotSupportedException>(() =>
            provider.OpenSession(new AIModelRequest { ModelPath = "m", ModelType = AIModelType.AnomalyDetection }));
    }

    [Fact]
    public void OpenSession_WithoutSdk_ThrowsWithModelPathContext()
    {
        var provider = new AiditronProvider();
        // 缺 DLL 时走 DllNotFoundException；有 DLL 但模型非法时走 SDK 包装异常——
        // 两种都必须包模型路径上下文后上抛，且无会话逃逸（抛异常即无会话）。
        var ex = Assert.Throws<InvalidOperationException>(() =>
            provider.OpenSession(new AIModelRequest { ModelPath = "missing-model.proj", ModelType = AIModelType.Detection }));
        Assert.Contains("missing-model.proj", ex.Message);
    }

    [Fact]
    public void CloseSession_Null_IsNoOp()
    {
        var provider = new AiditronProvider();
        provider.CloseSession(null);
    }

    [Fact]
    public void Converter_MapsDetectionSegmentationClassification()
    {
        var defects = new List<AiditronDefect>
        {
            new() { Label = "ng", Confidence = 0.9, Area = 500, CenterX = 20, CenterY = 30, Width = 10, Height = 20 },
        };

        var detection = AiditronResultConverter.ToDetection(defects);
        var box = Assert.Single(detection.Boxes);
        Assert.Equal("ng", box.Label);
        Assert.Equal(0.9, box.Score);
        Assert.Equal(new Vision.Core.Geometry.Rect2D(15, 20, 10, 20), box.Box);

        var segmentation = AiditronResultConverter.ToSegmentation(defects);
        var item = Assert.Single(segmentation.Items);
        Assert.Equal(new Vision.Core.Geometry.Rect2D(15, 20, 10, 20), item.Bounds);
        Assert.Empty(item.Polygon); // P6-v1 只有外接框（见 AiditronResults 文件头）。

        var classification = AiditronResultConverter.ToClassification(defects);
        Assert.Equal("ng", classification.TopLabel);
        Assert.Equal(0.9, classification.TopScore);

        Assert.Empty(AiditronResultConverter.ToDetection([]).Boxes);
    }

    [Fact(Skip = "需要真实 teTreePredict.dll/teRuntime.dll、加密芯片与工程模型（计划 §9.1 Integration）。")]
    [Trait("Category", "RequiresRuntime")]
    public void Run_AgainstRealSdk_RequiresRuntime()
    {
    }
}
