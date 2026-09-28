// ─────────────────────────────────────────────────────────────────────────────
// P6 验收测试 —— HALCON AI Provider。
// 无 Runtime 环境：构造/声明/类型映射/失败回滚可测；OpenSession 走真实
// ReadDlModel，缺 Runtime 或缺文件时断言“带路径上下文的失败”；映射器纯测全覆盖。
// 真实模型推理留 RequiresRuntime。
// ─────────────────────────────────────────────────────────────────────────────

using Vision.AI.Halcon;
using Vision.Core.AI;
using Vision.Core.Geometry;
using Xunit;

namespace Vision.Tests.Tools.AI.Halcon;

/// <summary>HALCON AI Provider 声明、无 Runtime 行为与映射测试。</summary>
[Trait("Category", "Contract")]
public sealed class HalconProviderTests
{
    [Fact]
    public void Provider_DeclaresNameAndAllFourCapabilities()
    {
        var provider = new HalconAIProvider();
        Assert.Equal("Halcon", provider.Name);
        Assert.True(provider.Capabilities.HasFlag(AICapabilities.Classification));
        Assert.True(provider.Capabilities.HasFlag(AICapabilities.Detection));
        Assert.True(provider.Capabilities.HasFlag(AICapabilities.Segmentation));
        Assert.True(provider.Capabilities.HasFlag(AICapabilities.AnomalyDetection));
    }

    [Fact]
    public void Provider_IsAvailable_DoesNotThrow()
    {
        var provider = new HalconAIProvider();
        _ = provider.IsAvailable; // 有/无 Runtime 都是合法值，关键是不抛。
    }

    [Fact]
    public void OpenSession_UnknownType_ThrowsNotSupported()
    {
        var provider = new HalconAIProvider();
        Assert.Throws<NotSupportedException>(() =>
            provider.OpenSession(new AIModelRequest { ModelPath = "m.hdl", ModelType = AIModelType.Unknown }));
    }

    [Fact]
    public void OpenSession_MissingFile_ThrowsWithPathContext()
    {
        var provider = new HalconAIProvider();
        // 缺 Runtime 时 ReadDlModel 走 DllNotFound；有 Runtime 时走文件不存在——
        // 两种都必须包模型路径上下文后上抛，且不留下半个会话（抛异常即无会话）。
        var ex = Assert.Throws<InvalidOperationException>(() =>
            provider.OpenSession(new AIModelRequest { ModelPath = "no-such-model.hdl", ModelType = AIModelType.Classification }));
        Assert.Contains("no-such-model.hdl", ex.Message);
    }

    [Fact]
    public void CloseSession_Null_IsNoOp()
    {
        var provider = new HalconAIProvider();
        provider.CloseSession(null);
    }

    [Fact]
    public void Mapper_MapsClassification_PickingTopScore()
    {
        var result = HalconResultMapper.MapClassification(
            [0, 1], [0.2, 0.85], ["ok", "ng"]);
        Assert.Equal("ng", result.TopLabel);
        Assert.Equal(0.85, result.TopScore);
        Assert.Equal(2, result.Scores.Count);
        Assert.Equal(1, result.Scores[1].ClassId);
    }

    [Fact]
    public void Mapper_MapsClassification_WithoutNames_FallsBackToIds()
    {
        var result = HalconResultMapper.MapClassification([3], [0.5]);
        Assert.Equal("class_3", result.TopLabel);
        Assert.Empty(HalconResultMapper.MapClassification([], []).Scores);
    }

    [Fact]
    public void Mapper_MapsDetection_WithPixelBoxes()
    {
        var result = HalconResultMapper.MapDetection(
            [10], [20], [29], [49], [2], [0.77], ["a", "b", "c"]);
        var box = Assert.Single(result.Boxes);
        Assert.Equal("c", box.Label);
        Assert.Equal(2, box.ClassId);
        Assert.Equal(0.77, box.Score);
        Assert.Equal(new Rect2D(20, 10, 30, 20), box.Box);
    }

    [Fact]
    public void Mapper_MapsSegmentation_AndAnomaly()
    {
        var segmentation = HalconResultMapper.MapSegmentation(
            [(new Rect2D(0, 0, 10, 10), 1, 0.6)]);
        var item = Assert.Single(segmentation.Items);
        Assert.Equal("class_1", item.Label);
        Assert.Empty(item.Polygon); // P6-v1 只有外接框。

        var anomaly = HalconResultMapper.MapAnomaly(0.9, true, new Rect2D(5, 5, 8, 8));
        Assert.True(anomaly.IsAnomaly);
        Assert.Equal(0.9, anomaly.Score);
        Assert.Equal(new Rect2D(5, 5, 8, 8), anomaly.AnomalyBounds);
    }

    [Fact(Skip = "需要真实 HALCON Runtime、License 与 .hdl 模型（计划 §9.1 Integration）。")]
    [Trait("Category", "RequiresRuntime")]
    public void Run_AgainstRealRuntime_RequiresRuntime()
    {
    }
}
