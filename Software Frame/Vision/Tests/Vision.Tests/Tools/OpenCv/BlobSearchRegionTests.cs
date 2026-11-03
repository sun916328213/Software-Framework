using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Operations;
using Vision.Core.Results;
using Vision.Core.Roi;
using Vision.Tools;
using Xunit;

namespace Vision.Tests.Tools.OpenCv;

[Trait("Category", "Unit")]
public sealed class BlobSearchRegionTests
{
    [Fact]
    public async Task DomainAppliedBeforeConnection_SplitsTargetsConnectedOnlyOutsideRoi()
    {
        var pixels = new byte[20 * 20];
        for (var y = 0; y < 16; y++)
        {
            foreach (var x in new[] { 2, 3, 10, 11 })
            {
                pixels[y * 20 + x] = 255;
            }
        }
        for (var y = 12; y < 14; y++)
        {
            for (var x = 2; x < 12; x++)
            {
                pixels[y * 20 + x] = 255;
            }
        }
        var image = Frame(pixels);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new BlobOperation(image, 128, 255, Fixed(Roi.Rectangle(10, 7, 20, 6))), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(2, result.Value!.Items.Count);
        Assert.All(result.Value.Items, item => Assert.Equal(12, item.Area));
        Assert.Equal(new Rect2D(2, 4, 2, 6), result.Value.Items[0].Bounds);
        Assert.Equal(new Rect2D(10, 4, 2, 6), result.Value.Items[1].Bounds);
        Assert.All(result.Value.Items.SelectMany(item => item.Contours).SelectMany(contour => contour.Points), point => Assert.InRange(point.Y, 4, 9));
        var full = await engine.ExecuteAsync(context, new BlobOperation(image, 128, 255), default);
        Assert.Equal(VisionExecutionStatus.Success, full.Status);
        Assert.Equal(76, Assert.Single(full.Value!.Items).Area);
    }

    [Fact]
    public async Task ZeroGrayThreshold_DoesNotTurnMaskedOutsidePixelsIntoForeground()
    {
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new BlobOperation(Frame(new byte[400]), 0, 0, Fixed(Roi.Rectangle(5.5, 5.5, 3, 3))), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        var item = Assert.Single(result.Value!.Items);
        Assert.Equal(9, item.Area);
        Assert.Equal(new Rect2D(4, 4, 3, 3), item.Bounds);
    }

    [Fact]
    public async Task CircleDoesNotFallBackToBounds_AndDisjointRegionIsRejected()
    {
        var image = Frame(new byte[400]);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new BlobOperation(image, 0, 255, Fixed(Roi.Circle(5, 5, 4))), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(13, Assert.Single(result.Value!.Items).Area);
        // 新契约在构造请求时拒绝无参与像素的区域，不把错误配置伪装成有效零目标。
        Assert.Throws<ArgumentException>(() => new BlobOperation(image, 0, 255, Fixed(Roi.Rectangle(100, 100, 5, 5))));
    }

    [Fact]
    public async Task RequestFreezesFrameFilterAndRegion_AfterOriginalInputsChange()
    {
        var pixels = new byte[400];
        var roi = Roi.Rectangle(5, 5, 4, 4);
        var filters = new List<BlobFeatureRange> { new(BlobFeature.Area, 16, 16) };
        var request = new BlobOperation(Frame(pixels), 0, 0, Fixed(roi), filters: filters);
        Array.Fill(pixels, (byte)255);
        filters.Clear();
        roi.Pose = new Pose2D(100, 100, 0);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, request, default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(16, Assert.Single(result.Value!.Items).Area);
        await engine.ReleaseContextAsync(context, default);
        Assert.NotEmpty(result.Value.Items[0].Contours);
    }

    private static VisionFrame Frame(byte[] pixels) => new(Guid.NewGuid(), Guid.NewGuid(), 20, 20, PixelFormat.Mono8, pixels);
    private static VisionRegionBinding Fixed(Roi roi) => new(new SearchRegion(roi));
}
