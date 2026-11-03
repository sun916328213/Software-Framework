using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Models;
using Vision.Core.Operations;
using Vision.Core.Results;
using Vision.Tools;
using Xunit;

namespace Vision.Tests.TestAssets;

/// <summary>六项原Golden需求迁至唯一引擎；保留解析像素/容差，不用fake替代真实OpenCV。</summary>
[Trait("Category", "Regression")]
public sealed class GoldenRegressionTests
{
    [Fact]
    public async Task Threshold_Rect600_CountIsLocked()
    {
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var image = GoldenImages.Rect600();
        var result = await engine.ExecuteAsync(context, new ThresholdRangeOperation(image, 128, 255), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(GoldenImages.Rect600Expected().Area, WhitePixelCount(result.Value!.Image));
        Assert.Equal(image.FrameId, result.Value.FrameId);
        await engine.ReleaseContextAsync(context, default);
    }

    [Fact]
    public async Task Blob_Rect600_CenterAndAreaAreLocked()
    {
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new BlobOperation(GoldenImages.Rect600(), 128, 255), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        var item = Assert.Single(result.Value!.Items);
        var (area, cx, cy) = GoldenImages.Rect600Expected();
        Assert.Equal(area, item.Area);
        Assert.Equal(cx, item.Center.X, 1);
        Assert.Equal(cy, item.Center.Y, 1);
        Assert.Equal(new Rect2D(20, 30, 20, 30), item.Bounds);
        await engine.ReleaseContextAsync(context, default);
    }

    [Fact]
    public async Task Morphology_Erode_Rect600_AreaIsLocked()
    {
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new MorphologyOperation(GoldenImages.Rect600(),
            MorphologyKind.Erode, MorphologyKernelShape.Rectangle, 3, 3, ImageBorderMode.ConstantZero), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        // 原20×30实心矩形被中心3×3核各侧腐蚀1px，解析面积18×28=504。
        Assert.Equal(504, WhitePixelCount(result.Value!.Image));
        await engine.ReleaseContextAsync(context, default);
    }

    [Fact]
    public async Task TemplateMatch_PastePattern_LocationIsLocked()
    {
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var model = await engine.PrepareModelAsync(context,
            new TemplateModelDefinition(GoldenImages.PatternTemplate(), new(0, 0), new()), 1, default);
        var result = await engine.ExecuteAsync(context,
            new TemplateMatchOperation(GoldenImages.PastePattern(), model, new(), 0.9), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        var match = Assert.Single(result.Value!.Matches);
        var (x, y) = GoldenImages.PastePatternExpected();
        // 新结果没有旧Bounds；明确锚点(0,0)代表原模板左上像素中心。
        var topLeft = match.TemplateToImage.Transform(new(0, 0));
        Assert.InRange(topLeft.X, x - 1, x + 1);
        Assert.InRange(topLeft.Y, y - 1, y + 1);
        Assert.Equal(new Point2D(0, 0), match.ModelOrigin);
        Assert.InRange(match.Score, 0.99, 1);
        await engine.ReleaseContextAsync(context, default);
    }

    [Fact]
    public async Task FindLine_HLine200_RowIsLocked()
    {
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var (y, x0, x1) = GoldenImages.HLine200Expected();
        var result = await engine.ExecuteAsync(context, new CaliperLineOperation(GoldenImages.HLine200(),
            new(x0, y), new(x1, y), normalHalfLength: 8, tangentHalfWidth: 0,
            polarity: CaliperEdgePolarity.DarkToLight, selection: CaliperEdgeSelection.First,
            gaussianSigmaPixels: 0), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        var line = Assert.IsType<CaliperLineFit>(result.Value!.Line);
        // 保留旧黄金的带中心±2.5px容差；新卡尺明确测下侧边缘，不冒充带中心线。
        Assert.InRange(line.Start.Y, y - 2.5, y + 2.5);
        Assert.InRange(line.End.Y, y - 2.5, y + 2.5);
        Assert.Equal(101.5, line.Start.Y, 6);
        Assert.Equal(101.5, line.End.Y, 6);
        await engine.ReleaseContextAsync(context, default);
    }

    [Fact]
    public async Task FindCircle_Disc30_CenterAndRadiusAreLocked()
    {
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var (cx, cy, radius) = GoldenImages.Disc30Expected();
        var result = await engine.ExecuteAsync(context, new CaliperCircleOperation(GoldenImages.Disc30(),
            new(cx, cy), radius, caliperCount: 64, tangentHalfWidth: 0,
            polarity: CaliperEdgePolarity.LightToDark, gaussianSigmaPixels: 0,
            minimumPoints: 48, maximumRadialResidualPixels: 1.2, minimumCoverageDegrees: 300), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        var circle = Assert.IsType<CaliperCircleFit>(result.Value!.Circle);
        Assert.InRange(circle.Center.DistanceTo(new(cx, cy)), 0, 4);
        Assert.InRange(circle.RadiusPixels, radius - 3, radius + 3);
        Assert.True(circle.CoverageDegrees >= 300);
        await engine.ReleaseContextAsync(context, default);
    }

    private static int WhitePixelCount(VisionFrame image)
    {
        var count = 0;
        for (var y = 0; y < image.Height; y++)
        {
            foreach (var pixel in image.Pixels.Slice(y * image.Stride, image.Width))
            {
                if (pixel == 255)
                {
                    count++;
                }
            }
        }

        return count;
    }
}
