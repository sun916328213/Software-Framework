using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Models;
using Vision.Core.Operations;
using Vision.Core.Results;
using Vision.Core.Roi;
using Vision.Tools;
using Vision.Tools.Execution;
using Xunit;

namespace Vision.Tests.Tools.Engine;

[Trait("Category", "Contract")]
public sealed class RgbColorSegmentEngineTests
{
    [Theory]
    [InlineData(PixelFormat.Rgb24)]
    [InlineData(PixelFormat.Bgr24)]
    public async Task ClosedRgbRanges_AreIndependentOfStorageOrder_AndIgnoreStridePadding(PixelFormat format)
    {
        byte[][] colors = [[100, 20, 30], [200, 40, 60], [99, 20, 30], [201, 40, 60],
            [150, 19, 45], [150, 41, 45], [150, 30, 29], [150, 30, 61]];
        var frame = ColorFrame(4, 2, format, colors, 5);
        var before = frame.Pixels.ToArray();
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var request = new RgbColorSegmentOperation(frame, new(100, 200, 20, 40, 30, 60));
        Assert.Null(request.Model);
        var result = await engine.ExecuteAsync(context, request, default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(new byte[] { 255, 255, 0, 0, 0, 0, 0, 0 }, result.Value!.Image.Pixels.ToArray());
        Assert.Equal(frame.FrameId, result.Value.FrameId);
        Assert.NotEqual(frame.FrameId, result.Value.Image.FrameId);
        Assert.Equal(frame.CoordinateSpaceId, result.Value.Image.CoordinateSpaceId);
        Assert.Equal(frame.Width, result.Value.Image.Width);
        Assert.Equal(frame.Height, result.Value.Image.Height);
        Assert.Equal(PixelFormat.Mono8, result.Value.Image.Format);
        Assert.Equal(before, frame.Pixels.ToArray());
    }

    [Fact]
    public async Task FrozenFixedRegion_AlwaysZerosOutside_EvenWhenBlackAndWhiteBothMatch()
    {
        var colors = Enumerable.Range(0, 12).Select(index => index % 2 == 0 ? new byte[] { 0, 0, 0 } : [255, 255, 255]).ToArray();
        var frame = ColorFrame(4, 3, PixelFormat.Rgb24, colors, 2);
        var roi = new Roi { Kind = RoiKind.Rectangle, Pose = new Pose2D(2, 1, 0), Size = new Size2D(2, 2) };
        var region = new SearchRegion(roi);
        var request = new RgbColorSegmentOperation(frame, new(0, 255, 0, 255, 0, 255), region);
        roi.Pose = new(100, 100, 0);
        Assert.Equal(VisionOperationFeatures.ProcessingRegion, request.RequiredFeatures);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, request, default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        // 矩形Pose是中心：本例参与像素为x=1,2及y=0,1，不把中心误当左上角。
        Assert.Equal(new byte[] { 0, 255, 255, 0, 0, 255, 255, 0, 0, 0, 0, 0 }, result.Value!.Image.Pixels.ToArray());
    }

    [Fact]
    public async Task BlackMask_IsSuccess_ThenBlobNoMatch_AndExplicitZeroCountPasses()
    {
        var frame = ColorFrame(3, 2, PixelFormat.Bgr24, Enumerable.Repeat(new byte[] { 0, 0, 255 }, 6).ToArray(), 0);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new RgbColorSegmentOperation(frame, new(200, 255, 0, 50, 0, 50)), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.All(result.Value!.Image.Pixels.ToArray(), value => Assert.Equal(0, value));
        var blobs = await engine.ExecuteAsync(context, new BlobOperation(result.Value.Image, 255, 255), default);
        Assert.Equal(VisionExecutionStatus.NoMatch, blobs.Status);
        Assert.Empty(blobs.Value!.Items);
        var decision = await engine.ExecuteAsync(context, BlobJudgmentOperation.ForCount(blobs.Value, 0, 0), default);
        Assert.Equal(VisionExecutionStatus.Success, decision.Status);
        Assert.True(decision.Value!.Passed);
    }

    [Fact]
    public async Task ActualRgbMask_FeedsBlobAndCountAreaJudgments_AfterSourceContextRelease()
    {
        var colors = Enumerable.Range(0, 12 * 8).Select(_ => new byte[] { 5, 40, 200 }).ToArray();
        foreach (var (x, y) in new[] { (1, 2), (7, 3) })
        {
            colors[y * 12 + x] = [200, 30, 10];
            colors[y * 12 + x + 1] = [200, 30, 10];
            colors[(y + 1) * 12 + x] = [200, 30, 10];
            colors[(y + 1) * 12 + x + 1] = [200, 30, 10];
        }
        var frame = ColorFrame(12, 8, PixelFormat.Bgr24, colors, 3);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var segmenting = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(segmenting, new RgbColorSegmentOperation(frame, new(180, 220, 20, 40, 0, 20)), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        await engine.ReleaseContextAsync(segmenting, default);
        var inspecting = await engine.CreateContextAsync(default);
        var blobs = await engine.ExecuteAsync(inspecting, new BlobOperation(result.Value!.Image, 255, 255), default);
        Assert.Equal(VisionExecutionStatus.Success, blobs.Status);
        Assert.Equal(new long[] { 4, 4 }, blobs.Value!.Items.Select(item => item.Area));
        Assert.Equal(result.Value.Image.FrameId, blobs.Value.FrameId);
        Assert.Equal(frame.CoordinateSpaceId, blobs.Value.CoordinateSpaceId);
        await engine.ReleaseContextAsync(inspecting, default);
        var evaluating = await engine.CreateContextAsync(default);
        var count = await engine.ExecuteAsync(evaluating, BlobJudgmentOperation.ForCount(blobs.Value, 2, 2), default);
        var ng = await engine.ExecuteAsync(evaluating, BlobJudgmentOperation.ForCount(blobs.Value, 3, 3), default);
        var area = await engine.ExecuteAsync(evaluating, BlobJudgmentOperation.ForArea(blobs.Value, 1, 4, 4), default);
        Assert.True(count.Value!.Passed);
        Assert.Equal(VisionExecutionStatus.Success, ng.Status);
        Assert.False(ng.Value!.Passed);
        Assert.True(area.Value!.Passed);
    }

    [Theory]
    [InlineData(PixelFormat.Mono8, 1)]
    [InlineData(PixelFormat.Mono16, 2)]
    [InlineData(PixelFormat.Bgra32, 4)]
    [InlineData(PixelFormat.Float32, 4)]
    public async Task OtherFormats_AreUnsupported_NotImplicitGrayConversionOrAlphaDiscard(PixelFormat format, int bytes)
    {
        var frame = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 2, 2, format, new byte[4 * bytes]);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new RgbColorSegmentOperation(frame, new(0, 255, 0, 255, 0, 255)), default);
        Assert.Equal(VisionExecutionStatus.Unsupported, result.Status);
        Assert.Equal("UnsupportedPixelFormat", result.Issue!.Code);
        Assert.Null(result.Value);
    }

    [Fact]
    public void InvalidChannelOrderNullRangeOrEmptyRegion_AreRejectedBeforeExecution()
    {
        Assert.Throws<ArgumentException>(() => new RgbColorRange(100, 99, 0, 255, 0, 255));
        Assert.Throws<ArgumentException>(() => new RgbColorRange(0, 255, 100, 99, 0, 255));
        Assert.Throws<ArgumentException>(() => new RgbColorRange(0, 255, 0, 255, 100, 99));
        var frame = ColorFrame(2, 2, PixelFormat.Rgb24, Enumerable.Repeat(new byte[] { 0, 0, 0 }, 4).ToArray(), 0);
        Assert.Throws<ArgumentNullException>(() => new RgbColorSegmentOperation(frame, null!));
        var outside = new SearchRegion(Roi.Rectangle(100, 100, 2, 2));
        Assert.Throws<ArgumentException>(() => new RgbColorSegmentOperation(frame, new(0, 255, 0, 255, 0, 255), outside));
    }

    [Fact]
    public async Task SelectedBackendWithoutColorCapability_IsUnsupportedWithoutCallingOrFallingBack()
    {
        await using var engine = VisionEngineFactory.Create(new("test"), (_, _) => new NoColorSdk());
        Assert.DoesNotContain(engine.Descriptor.Operations, capability => capability.OperationId == RgbColorSegmentOperation.Id);
        var context = await engine.CreateContextAsync(default);
        var frame = ColorFrame(2, 2, PixelFormat.Rgb24, Enumerable.Repeat(new byte[] { 200, 30, 10 }, 4).ToArray(), 0);
        var result = await engine.ExecuteAsync(context, new RgbColorSegmentOperation(frame, new(180, 220, 20, 40, 0, 20)), default);
        Assert.Equal(VisionExecutionStatus.Unsupported, result.Status);
        Assert.Null(result.Value);
    }

    internal static VisionFrame ColorFrame(int width, int height, PixelFormat format, byte[][] rgb, int padding)
    {
        var stride = width * 3 + padding;
        var pixels = Enumerable.Repeat((byte)255, stride * height).ToArray();
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var color = rgb[y * width + x];
                var offset = y * stride + x * 3;
                pixels[offset] = format == PixelFormat.Rgb24 ? color[0] : color[2];
                pixels[offset + 1] = color[1];
                pixels[offset + 2] = format == PixelFormat.Rgb24 ? color[2] : color[0];
            }
        }
        return new(Guid.NewGuid(), Guid.NewGuid(), width, height, format, pixels, stride);
    }

    private sealed class NoColorSdk : IVisionBackend
    {
        public string Id => "test";
        public VisionExecutionDomain Domain => VisionExecutionDomain.Vision;
        public IReadOnlyList<VisionOperationCapability> Capabilities => [];
        public IReadOnlyList<VisionIssue> Validate(VisionOperation operation, IPreparedVisionModel? model)
            => throw new InvalidOperationException("Missing capability must stop before SDK validation");
        public ValueTask<IPreparedVisionModel> PrepareAsync(VisionModelDefinition definition, CancellationToken token)
            => throw new NotSupportedException();
        public ValueTask<BackendOutput> ExecuteAsync(IPreparedVisionModel? model, VisionOperation operation, CancellationToken token)
            => throw new InvalidOperationException("Missing capability must not fall back to another backend");
        public void Dispose()
        {
        }
    }
}
