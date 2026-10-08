// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：GoldenRegressionTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Xunit。导入不代表所有类型都会在运行时加载。
// ─────────────────────────────────────────────────────────────────────────────
// P10 —— Golden 回归测试（Trait=Regression，与普通 Unit 分开执行、分开看）。
// 锁 OpenCV 后端在黄金上的输出；HALCON 侧对比见 Comparison（业务容差口径）。
// 普通 CI 跑稳定子集 = 本文件全部（无 Runtime 依赖，CPU 纯算子）。
// ─────────────────────────────────────────────────────────────────────────────

using Xunit;

namespace Vision.Tests.TestAssets;

/// <summary>Golden 回归（OpenCV 后端输出锁）。</summary>
[Trait("Category", "Regression")]
public sealed class GoldenRegressionTests
{
    /// <summary>
    /// 测试相关入口 Threshold_Rect600_CountIsLocked；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Run、Rect600、True、Rect600Expected、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Threshold_Rect600_CountIsLocked()
    {
        using var tool = new Vision.Tools.OpenCv.Processing.ThresholdTool();
        tool.Parameters.MinGray = 128;
        var result = tool.Run(GoldenImages.Rect600());
        Assert.True(result.Success, result.Message);
        var (area, _, _) = GoldenImages.Rect600Expected();
        Assert.Equal(area, result.WhitePixelCount);
    }

    /// <summary>
    /// 测试相关入口 Blob_Rect600_CenterAndAreaAreLocked；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Run、Rect600、True、Single、Rect600Expected；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Blob_Rect600_CenterAndAreaAreLocked()
    {
        using var tool = new Vision.Tools.OpenCv.Inspection.BlobTool();
        tool.Parameters.MinGray = 128;
        tool.Parameters.Features = [];
        var result = tool.Run(GoldenImages.Rect600());
        Assert.True(result.Success, result.Message);
        var item = Assert.Single(result.Items);
        var (area, cx, cy) = GoldenImages.Rect600Expected();
        Assert.Equal(area, item.Area);
        Assert.Equal(cx, item.Center.X, 1);
        Assert.Equal(cy, item.Center.Y, 1);
    }

    /// <summary>
    /// 测试相关入口 Morphology_Erode_Rect600_AreaIsLocked；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Run、Rect600、True、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Morphology_Erode_Rect600_AreaIsLocked()
    {
        using var tool = new Vision.Tools.OpenCv.Processing.MorphologyTool();
        tool.Parameters.Operation = Vision.Tools.OpenCv.Processing.MorphologyOperation.Erode;
        tool.Parameters.KernelSize = 3;
        var result = tool.Run(GoldenImages.Rect600());
        Assert.True(result.Success, result.Message);
        Assert.Equal(504, result.WhitePixelCount);
    }

    /// <summary>
    /// 测试相关入口 TemplateMatch_PastePattern_LocationIsLocked；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 PatternTemplate、Run、PastePattern、True、Single；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void TemplateMatch_PastePattern_LocationIsLocked()
    {
        using var tool = new Vision.Tools.OpenCv.Matching.TemplateMatchTool();
        tool.Parameters.Template = Vision.Tests.Tools.SyntheticImages.PatternTemplate();
        tool.Parameters.MinScore = 0.9;
        var result = tool.Run(GoldenImages.PastePattern());
        Assert.True(result.Success, result.Message);
        var match = Assert.Single(result.Matches);
        var (x, y) = GoldenImages.PastePatternExpected();
        Assert.InRange(match.Bounds.X, x - 1, x + 1);
        Assert.InRange(match.Bounds.Y, y - 1, y + 1);
    }

    /// <summary>
    /// 测试相关入口 FindLine_HLine200_RowIsLocked；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Run、HLine200、True、HLine200Expected、InRange；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void FindLine_HLine200_RowIsLocked()
    {
        using var tool = new Vision.Tools.OpenCv.Measurement.FindLineTool();
        tool.Parameters.LineStart = new Vision.Core.Geometry.Point2D(20, 100);
        tool.Parameters.LineEnd = new Vision.Core.Geometry.Point2D(180, 100);
        var result = tool.Run(GoldenImages.HLine200());
        Assert.True(result.Success, result.Message);
        var (y, _, _) = GoldenImages.HLine200Expected();
        Assert.InRange(result.Start.Y, y - 2.5, y + 2.5);
        Assert.InRange(result.End.Y, y - 2.5, y + 2.5);
    }

    /// <summary>
    /// 测试相关入口 FindCircle_Disc30_CenterAndRadiusAreLocked；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Run、Disc30、True、Disc30Expected、DistanceTo；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void FindCircle_Disc30_CenterAndRadiusAreLocked()
    {
        using var tool = new Vision.Tools.OpenCv.Measurement.FindCircleTool();
        tool.Parameters.ExpectedCenter = new Vision.Core.Geometry.Point2D(100, 120);
        tool.Parameters.RadiusMin = 20;
        tool.Parameters.RadiusMax = 40;
        var result = tool.Run(GoldenImages.Disc30());
        Assert.True(result.Success, result.Message);
        var (cx, cy, r) = GoldenImages.Disc30Expected();
        Assert.True(result.Center.DistanceTo(new Vision.Core.Geometry.Point2D(cx, cy)) <= 4);
        Assert.InRange(result.Radius, r - 3, r + 3);
    }
}
