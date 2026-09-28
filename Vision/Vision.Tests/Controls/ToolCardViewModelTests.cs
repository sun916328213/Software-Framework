// ─────────────────────────────────────────────────────────────────────────────
// P8 测试 —— ToolCardViewModel（计划 §P8「关键 ViewModel 逻辑测试」）。
// 全程无 UI 线程：OpenCV 真实执行（合成图）、HALCON/AI 走无 Runtime 路径.
// ─────────────────────────────────────────────────────────────────────────────

using Vision.Controls.ViewModels;
using Vision.Tests.Tools;
using Vision.Tests.Tools.AI;
using Xunit;

namespace Vision.Tests.Controls;

/// <summary>Tool 卡片 VM 测试。</summary>
[Trait("Category", "Unit")]
public sealed class ToolCardViewModelTests
{
    [Fact]
    public void OpenCvThreshold_RefreshRun_ShowsRealResult()
    {
        var tool = new Vision.Tools.OpenCv.Processing.ThresholdTool();
        using var adapter = new OpenCvToolCardAdapter<
            Vision.Tools.OpenCv.Processing.ThresholdParameters,
            Vision.Tools.OpenCv.Processing.ThresholdResult>(tool);
        var card = new ToolCardViewModel(adapter);
        Assert.Equal("Threshold", card.ToolName);

        card.RefreshValidationCommand.Execute(null);
        Assert.True(card.IsValid);
        Assert.Equal("校验通过", card.StatusText);

        card.SetImage(SyntheticImages.WhiteRectangle(100, 100, 20, 30, 40, 60));
        tool.Parameters.MinGray = 128;
        card.RunCommand.Execute(null);
        Assert.Equal("执行成功", card.StatusText);
        Assert.True(card.ElapsedMs >= 0);
        Assert.Contains("600", card.ResultSummary);
    }

    [Fact]
    public void HalconBlob_InvalidParams_ShowsValidationError_WithoutRuntime()
    {
        var tool = new Vision.Tools.Halcon.Inspection.BlobTool();
        tool.Parameters.MinGray = 200;
        tool.Parameters.MaxGray = 100;
        using var adapter = new HalconToolCardAdapter<
            Vision.Tools.Halcon.Inspection.BlobParameters,
            Vision.Tools.Halcon.Inspection.BlobResult>(tool);
        var card = new ToolCardViewModel(adapter);

        card.RefreshValidationCommand.Execute(null);
        Assert.False(card.IsValid);
        Assert.StartsWith("校验失败：", card.StatusText);
    }

    [Fact]
    public void AIDetection_WithoutProvider_LoadFailsIntoStatus()
    {
        var tool = new Vision.Tools.AI.Detection.DetectionTool();
        tool.Parameters.ModelPath = "det.model";
        using var adapter = new AIToolCardAdapter<
            Vision.Tools.AI.Detection.DetectionParameters,
            Vision.Core.AI.AIDetectionResult>(tool);
        var card = new ToolCardViewModel(adapter);

        card.EnsureLoadedCommand.Execute(null);
        Assert.False(card.IsValid);
        Assert.StartsWith("加载失败：", card.StatusText);
    }

    [Fact]
    public void AIDetection_WithFakeProvider_RunsEndToEnd()
    {
        var tool = new Vision.Tools.AI.Detection.DetectionTool();
        tool.Parameters.ModelPath = "det.model";
        using var adapter = new AIToolCardAdapter<
            Vision.Tools.AI.Detection.DetectionParameters,
            Vision.Core.AI.AIDetectionResult>(tool);
        adapter.Provider = new FakeAIProvider();
        var card = new ToolCardViewModel(adapter);

        card.EnsureLoadedCommand.Execute(null);
        Assert.Equal("就绪", card.StatusText);
        card.SetImage(SyntheticImages.WhiteRectangle(64, 64, 10, 10, 30, 30));
        card.RunCommand.Execute(null);
        Assert.Equal("执行成功", card.StatusText);
        Assert.Contains("检测：", card.ResultSummary);
    }

    [Fact]
    public void PropertyChanged_Fires_OnStatusUpdate()
    {
        var tool = new Vision.Tools.OpenCv.Processing.ThresholdTool();
        using var adapter = new OpenCvToolCardAdapter<
            Vision.Tools.OpenCv.Processing.ThresholdParameters,
            Vision.Tools.OpenCv.Processing.ThresholdResult>(tool);
        var card = new ToolCardViewModel(adapter);

        var raised = new List<string>();
        card.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "?");
        card.RefreshValidationCommand.Execute(null);
        Assert.Contains(nameof(ToolCardViewModel.StatusText), raised);
        Assert.Contains(nameof(ToolCardViewModel.IsValid), raised);
    }
}
