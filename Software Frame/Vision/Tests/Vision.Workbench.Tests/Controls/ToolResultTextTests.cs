// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ToolResultTextTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Workbench、Vision.Workbench.Tests、Vision.Controls.Presentation、Vision.Core.AI、Vision.Core.Geometry。导入不代表所有类型都会在运行时加载。
using Vision.Workbench;
using Vision.Workbench.Tests;
// ─────────────────────────────────────────────────────────────────────────────
// P8 测试 —— ToolResultText 摘要（已知类型按域语义，未知回退 ToString）.
// 原先住在 Vision.Tests；按分层规则「UI 层测试独占 WPF」迁到本工程。
// ─────────────────────────────────────────────────────────────────────────────

using Vision.Controls.Presentation;
using Vision.Core.AI;
using Vision.Core.Geometry;

namespace Vision.Workbench.Tests.Controls;

/// <summary>结果摘要测试。</summary>
[Trait("Category", "Unit")]
public sealed class ToolResultTextTests
{
    /// <summary>
    /// 验证 Null_AndFailure_HavePlaceholders 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Equal、Summarize；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 AIResults_SummarizeByDomain 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Contains、Summarize；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 TraditionalTools_SummarizeCounts 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Contains、Summarize；具体条件和先后次序见方法体。
    /// </summary>
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