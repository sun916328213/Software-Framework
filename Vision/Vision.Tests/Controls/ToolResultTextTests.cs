// ─────────────────────────────────────────────────────────────────────────────
// P8 测试 —— ToolResultText 摘要（已知类型按域语义，未知回退 ToString）.
// ─────────────────────────────────────────────────────────────────────────────

using Vision.Controls.Presentation;
using Vision.Core.AI;
using Vision.Core.Geometry;
using Xunit;

namespace Vision.Tests.Controls;

/// <summary>结果摘要测试。</summary>
[Trait("Category", "Unit")]
public sealed class ToolResultTextTests
{
    [Fact]
    public void Null_AndFailure_HavePlaceholders()
    {
        Assert.Equal("—", ToolResultText.Summarize(null));
        Assert.Equal("失败：爆了", ToolResultText.Summarize(new Vision.Core.Results.ToolResult
        {
            Success = false,
            Message = "爆了",
        }));
    }

    [Fact]
    public void AIResults_SummarizeByDomain()
    {
        Assert.Contains("OK", ToolResultText.Summarize(new AIClassificationResult
        {
            TopLabel = "OK",
            TopScore = 0.9,
            Scores = [new AIClassScore { Label = "OK", Score = 0.9 }],
        }));
        Assert.Contains("2 个目标", ToolResultText.Summarize(new AIDetectionResult
        {
            Boxes =
            [
                new AIDetectionBox { Label = "a", Score = 0.8, Box = new Rect2D(0, 0, 10, 10) },
                new AIDetectionBox { Label = "b", Score = 0.6, Box = new Rect2D(5, 5, 4, 4) },
            ],
        }));
        Assert.Contains("3 个区域", ToolResultText.Summarize(new AISegmentationResult
        {
            Items =
            [
                new AISegmentationItem { Label = "x" },
                new AISegmentationItem { Label = "y" },
                new AISegmentationItem { Label = "z" },
            ],
        }));
        Assert.Contains("正常", ToolResultText.Summarize(new AIAnomalyResult { IsAnomaly = false }));
        Assert.Contains("异常", ToolResultText.Summarize(new AIAnomalyResult { IsAnomaly = true, Score = 0.7 }));
    }

    [Fact]
    public void TraditionalTools_SummarizeCounts()
    {
        Assert.Contains("0 个", ToolResultText.Summarize(
            new Vision.Tools.Halcon.Inspection.BlobResult()));
        Assert.Contains("2 个", ToolResultText.Summarize(
            new Vision.Tools.Halcon.Inspection.BlobResult
            {
                Items =
                [
                    new Vision.Tools.Halcon.Inspection.BlobItem { Index = 1, Area = 10, Center = new Point2D(1, 2) },
                    new Vision.Tools.Halcon.Inspection.BlobItem { Index = 2, Area = 20, Center = new Point2D(3, 4) },
                ],
            }));
        Assert.Contains("圆", ToolResultText.Summarize(
            new Vision.Tools.OpenCv.Measurement.FindCircleResult { Center = new Point2D(5, 5), Radius = 10 }));
        Assert.Contains("前景", ToolResultText.Summarize(
            new Vision.Tools.OpenCv.Processing.ThresholdResult { WhitePixelCount = 600, ForegroundRatio = 0.06 }));
        Assert.Contains("直线", ToolResultText.Summarize(
            new Vision.Tools.Halcon.Measurement.FindLineResult
            {
                Start = new Point2D(0, 0),
                End = new Point2D(10, 10),
                EdgePointCount = 9,
            }));
    }
}
