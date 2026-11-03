using Vision.Core.Capabilities;
using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Operations;
using Vision.Core.Results;
using Vision.Core.Roi;
using Vision.Tools;
using Xunit;

namespace Vision.Tests.Tools.Engine;

[Trait("Category", "Contract")]
public sealed class HsvColorSegmentEngineTests
{
    [Theory]
    [InlineData(PixelFormat.Rgb24)]
    [InlineData(PixelFormat.Bgr24)]
    public async Task WrappedHueRange_IncludesBothRedSides_NotNeighborBinsOrDifferentStorageOrder(PixelFormat format)
    {
        byte[][] colors = [[255, 0, 0], [255, 0, 42], [255, 42, 0], [255, 0, 51], [255, 51, 0], [0, 255, 0], [0, 0, 255]];
        var frame = RgbColorSegmentEngineTests.ColorFrame(7, 1, format, colors, 5);
        var before = frame.Pixels.ToArray();
        var range = new HsvColorRange(175, 5, 200, 255, 100, 255);
        Assert.True(range.HueWraps);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new HsvColorSegmentOperation(frame, range), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(new byte[] { 255, 255, 255, 0, 0, 0, 0 }, result.Value!.Image.Pixels.ToArray());
        Assert.Equal(before, frame.Pixels.ToArray());
        Assert.NotEqual(frame.FrameId, result.Value.Image.FrameId);
        Assert.Equal(frame.FrameId, result.Value.FrameId);
        Assert.Equal(frame.CoordinateSpaceId, result.Value.Image.CoordinateSpaceId);
        Assert.Equal(PixelFormat.Mono8, result.Value.Image.Format);
    }

    [Fact]
    public async Task BgraRequiresExplicitAlphaDiscard_BeforeWrappedHueSegmentation()
    {
        // 红、色相两侧的红、蓝、绿、白、近灰、暗红；alpha和每行填充不影响显式转换后的颜色。
        byte[] pixels = [0, 0, 255, 0, 10, 0, 255, 255, 88, 88,
            0, 10, 255, 0, 255, 0, 0, 255, 88, 88,
            0, 255, 0, 0, 255, 255, 255, 255, 88, 88,
            128, 128, 130, 0, 0, 0, 10, 255, 88, 88];
        var source = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 2, 4, PixelFormat.Bgra32, pixels, 10);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var range = new HsvColorRange(170, 10, 64, 255, 32, 255);
        var direct = await engine.ExecuteAsync(context, new HsvColorSegmentOperation(source, range), default);
        Assert.Equal(VisionExecutionStatus.Unsupported, direct.Status);
        Assert.Null(direct.Value);
        var converted = await engine.ExecuteAsync(context, new ImageConvertOperation(source, PixelFormat.Bgr24), default);
        Assert.Equal(VisionExecutionStatus.Success, converted.Status);
        Assert.Equal(source.FrameId, converted.Value!.FrameId);
        var mask = await engine.ExecuteAsync(context, new HsvColorSegmentOperation(converted.Value.Image, range), default);
        Assert.Equal(VisionExecutionStatus.Success, mask.Status);
        Assert.Equal(new byte[] { 255, 255, 255, 0, 0, 0, 0, 0 }, mask.Value!.Image.Pixels.ToArray());
        Assert.Equal(converted.Value.Image.FrameId, mask.Value.FrameId);
        Assert.Equal(source.CoordinateSpaceId, mask.Value.Image.CoordinateSpaceId);
        Assert.Equal(pixels, source.Pixels.ToArray());
    }

    [Fact]
    public async Task NonWrappedHueInterval_UsesQuantizedH0To179_NotDegreesOrFullRangeEncoding()
    {
        byte[][] colors = [[255, 0, 0], [255, 255, 0], [0, 255, 0], [0, 255, 255], [0, 0, 255], [255, 0, 255]];
        var frame = RgbColorSegmentEngineTests.ColorFrame(6, 1, PixelFormat.Rgb24, colors, 1);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new HsvColorSegmentOperation(frame, new(60, 120, 255, 255, 255, 255)), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(new byte[] { 0, 0, 255, 255, 255, 0 }, result.Value!.Image.Pixels.ToArray());
    }

    [Fact]
    public async Task SaturationAndValueClosedLimits_DoNotSilentlyExpandNearAchromaticOrIntensityBounds()
    {
        byte[][] colors = [[100, 0, 0], [200, 0, 0], [99, 0, 0], [201, 0, 0], [200, 1, 1], [0, 100, 0]];
        var frame = RgbColorSegmentEngineTests.ColorFrame(6, 1, PixelFormat.Bgr24, colors, 3);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new HsvColorSegmentOperation(frame, new(0, 0, 255, 255, 100, 200)), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(new byte[] { 255, 255, 0, 0, 0, 0 }, result.Value!.Image.Pixels.ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AchromaticPixels_NeedExplicitPolicyAndStillRespectSaturationValueLimits(bool include)
    {
        byte[][] colors = [[255, 255, 255], [128, 128, 128], [0, 0, 0], [0, 255, 0], [255, 0, 0]];
        var frame = RgbColorSegmentEngineTests.ColorFrame(5, 1, PixelFormat.Rgb24, colors, 0);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new HsvColorSegmentOperation(frame, new(60, 60, 0, 255, 100, 255, include)), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(include ? new byte[] { 255, 255, 0, 255, 0 } : [0, 0, 0, 255, 0], result.Value!.Image.Pixels.ToArray());
        var nonzeroS = await engine.ExecuteAsync(context, new HsvColorSegmentOperation(frame, new(60, 60, 1, 255, 100, 255, true)), default);
        Assert.Equal(new byte[] { 0, 0, 0, 255, 0 }, nonzeroS.Value!.Image.Pixels.ToArray());
        var red = await engine.ExecuteAsync(context, new HsvColorSegmentOperation(frame, new(0, 0, 0, 255, 100, 255)), default);
        Assert.Equal(new byte[] { 0, 0, 0, 0, 255 }, red.Value!.Image.Pixels.ToArray());
        var onlySZero = await engine.ExecuteAsync(context, new HsvColorSegmentOperation(frame, new(0, 179, 0, 0, 0, 255)), default);
        Assert.Equal(VisionExecutionStatus.Success, onlySZero.Status);
        Assert.All(onlySZero.Value!.Image.Pixels.ToArray(), pixel => Assert.Equal(0, pixel));
    }

    [Fact]
    public async Task FixedRegionIsFrozen_AndOutsideAlwaysZeroEvenWhenHueAndSvMatch()
    {
        var colors = Enumerable.Repeat(new byte[] { 0, 255, 0 }, 12).ToArray();
        var frame = RgbColorSegmentEngineTests.ColorFrame(4, 3, PixelFormat.Bgr24, colors, 2);
        var roi = new Roi { Kind = RoiKind.Rectangle, Pose = new Pose2D(2, 1, 0), Size = new Size2D(2, 2) };
        var request = new HsvColorSegmentOperation(frame, new(0, 179, 0, 255, 0, 255), new SearchRegion(roi));
        roi.Pose = new(100, 100, 0);
        Assert.Equal(VisionOperationFeatures.ProcessingRegion, request.RequiredFeatures);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, request, default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(new byte[] { 0, 255, 255, 0, 0, 255, 255, 0, 0, 0, 0, 0 }, result.Value!.Image.Pixels.ToArray());
    }

    [Fact]
    public async Task ActualTwoRedHueSides_FeedBlobAndCountAreaJudgmentsAfterSourceClose()
    {
        var colors = Enumerable.Range(0, 96).Select(_ => new byte[] { 0, 255, 0 }).ToArray();
        foreach (var (x, y, color) in new[] { (1, 2, new byte[] { 255, 0, 42 }), (7, 3, new byte[] { 255, 42, 0 }) })
        {
            colors[y * 12 + x] = colors[y * 12 + x + 1] = colors[(y + 1) * 12 + x] = colors[(y + 1) * 12 + x + 1] = color;
        }
        var frame = RgbColorSegmentEngineTests.ColorFrame(12, 8, PixelFormat.Bgr24, colors, 3);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var segmenting = await engine.CreateContextAsync(default);
        var mask = await engine.ExecuteAsync(segmenting, new HsvColorSegmentOperation(frame, new(175, 5, 200, 255, 100, 255)), default);
        Assert.Equal(VisionExecutionStatus.Success, mask.Status);
        await engine.ReleaseContextAsync(segmenting, default);
        var inspecting = await engine.CreateContextAsync(default);
        var blobs = await engine.ExecuteAsync(inspecting, new BlobOperation(mask.Value!.Image, 255, 255), default);
        Assert.Equal(VisionExecutionStatus.Success, blobs.Status);
        Assert.Equal(new long[] { 4, 4 }, blobs.Value!.Items.Select(item => item.Area));
        var count = await engine.ExecuteAsync(inspecting, BlobJudgmentOperation.ForCount(blobs.Value, 2, 2), default);
        var area = await engine.ExecuteAsync(inspecting, BlobJudgmentOperation.ForArea(blobs.Value, 1, 4, 4), default);
        var ng = await engine.ExecuteAsync(inspecting, BlobJudgmentOperation.ForCount(blobs.Value, 3, 3), default);
        Assert.True(count.Value!.Passed);
        Assert.True(area.Value!.Passed);
        Assert.Equal(VisionExecutionStatus.Success, ng.Status);
        Assert.False(ng.Value!.Passed);
    }

    [Theory]
    [InlineData(PixelFormat.Mono8, 1)]
    [InlineData(PixelFormat.Bgra32, 4)]
    public async Task UnspecifiedColorOrAlphaHandling_IsUnsupported(PixelFormat format, int bytes)
    {
        var frame = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 2, 2, format, new byte[4 * bytes]);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new HsvColorSegmentOperation(frame, new(0, 179, 0, 255, 0, 255)), default);
        Assert.Equal(VisionExecutionStatus.Unsupported, result.Status);
        Assert.Equal("UnsupportedPixelFormat", result.Issue!.Code);
        Assert.Null(result.Value);
    }

    [Fact]
    public void InvalidHueSvRangesNullRangeOrEmptyRegion_AreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new HsvColorRange(180, 179, 0, 255, 0, 255));
        Assert.Throws<ArgumentOutOfRangeException>(() => new HsvColorRange(0, 180, 0, 255, 0, 255));
        Assert.Throws<ArgumentException>(() => new HsvColorRange(0, 179, 100, 99, 0, 255));
        Assert.Throws<ArgumentException>(() => new HsvColorRange(0, 179, 0, 255, 100, 99));
        var frame = RgbColorSegmentEngineTests.ColorFrame(2, 2, PixelFormat.Rgb24, Enumerable.Repeat(new byte[] { 0, 0, 0 }, 4).ToArray(), 0);
        Assert.Throws<ArgumentNullException>(() => new HsvColorSegmentOperation(frame, null!));
        Assert.Throws<ArgumentException>(() => new HsvColorSegmentOperation(frame, new(0, 179, 0, 255, 0, 255), new SearchRegion(Roi.Rectangle(100, 100, 2, 2))));
    }
}
