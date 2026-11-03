using System.Buffers.Binary;
using Vision.Core;
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
using Vision.Tools.Halcon.Engine;
using Xunit;

namespace Vision.Tests.Tools.Engine;

[Trait("Category", "Contract")]
public sealed class GrayHistogramEngineTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RawCountsAndMedianPreserveBitDepth_IgnorePadding_AndAgreeWithExistingStatistics(bool mono16)
    {
        ushort[] values = mono16 ? [0, 20000, 40000, 60000] : [0, 2, 4, 6];
        var frame = Frame(values, mono16, 2, 3);
        var original = frame.Pixels.ToArray();
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new GrayHistogramOperation(frame), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        var histogram = result.Value!;
        Assert.Equal(mono16 ? 65536 : 256, histogram.Bins.Count);
        Assert.Equal(4, histogram.PixelCount);
        Assert.Equal(4, histogram.Bins.Sum());
        Assert.All(values, value => Assert.Equal(1, histogram.Bins[value]));
        Assert.Equal(mono16 ? 30000 : 3, histogram.Median);
        Assert.Equal(frame.FrameId, histogram.FrameId);
        Assert.Equal(frame.CoordinateSpaceId, histogram.CoordinateSpaceId);
        Assert.Equal(frame.Format, histogram.Format);
        var statistics = await engine.ExecuteAsync(context, new GrayStatisticsOperation(frame), default);
        Assert.Equal(VisionExecutionStatus.Success, statistics.Status);
        Assert.Equal(mono16 ? 30000 : 3, statistics.Value!.Mean);
        Assert.Equal(Math.Sqrt(5) * (mono16 ? 10000 : 1), statistics.Value.PopulationStandardDeviation, 7);
        Assert.Equal(original, frame.Pixels.ToArray());
        await engine.ReleaseContextAsync(context, default);
        Assert.Equal(4, histogram.Bins.Sum());
    }

    [Fact]
    public async Task HistogramDoesNotPrepareFakeModelOrInvalidateCurrentTemplate()
    {
        var frame = Frame([0, 200, 0, 100, 50, 255, 5, 0, 0, 60, 120, 0, 10, 0, 0, 250], width: 4);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var model = await engine.PrepareModelAsync(context, new TemplateModelDefinition(frame, new(0, 0), new()), 1, default);
        var request = new GrayHistogramOperation(frame, 256);
        Assert.Null(request.Model);
        var histogram = await engine.ExecuteAsync(context, request, default);
        Assert.Equal(VisionExecutionStatus.Success, histogram.Status);
        var match = await engine.ExecuteAsync(context, new TemplateMatchOperation(frame, model, new(), 0.99), default);
        Assert.Equal(VisionExecutionStatus.Success, match.Status);
        Assert.Equal(new Point2D(0, 0), Assert.Single(match.Value!.Matches).Position);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(256)]
    public async Task GroupingNeverQuantizesMedian_AndLastBinMayBePartial(int width)
    {
        var frame = Frame([0, 1, 2, 3]);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new GrayHistogramOperation(frame, width), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(1.5, result.Value!.Median);
        Assert.Equal((256 + width - 1) / width, result.Value.Bins.Count);
        var expected = new long[result.Value.Bins.Count];
        foreach (var gray in new[] { 0, 1, 2, 3 })
        {
            expected[gray / width]++;
        }

        Assert.Equal(expected, result.Value.Bins);
    }

    [Fact]
    public async Task Upper16BitLevelsAndOddMedianRemainExact_WhenAllPixelsShareOneBin()
    {
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var even = await engine.ExecuteAsync(context, new GrayHistogramOperation(Frame([65534, 65535], true), 65536), default);
        Assert.Equal(VisionExecutionStatus.Success, even.Status);
        Assert.Equal(65534.5, even.Value!.Median);
        Assert.Equal(2, Assert.Single(even.Value.Bins));
        var odd = await engine.ExecuteAsync(context, new GrayHistogramOperation(Frame([0, 2, 9]), 256), default);
        Assert.Equal(VisionExecutionStatus.Success, odd.Status);
        Assert.Equal(2, odd.Value!.Median);
        Assert.Equal(3, Assert.Single(odd.Value.Bins));
    }

    [Fact]
    public async Task Highest8BitCodeIsIncludedInPartialLastBin_WithoutMovingEvenMedian()
    {
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new GrayHistogramOperation(Frame([0, 255]), 17), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(16, result.Value!.Bins.Count);
        Assert.Equal(1, result.Value.Bins[0]);
        Assert.Equal(1, result.Value.Bins[^1]);
        Assert.Equal(127.5, result.Value.Median);
    }

    [Fact]
    public async Task LargeSingleBinCountRemainsExactBeyondFloatIntegerPrecision()
    {
        const int count = 16_777_217;
        var frame = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), count, 1, PixelFormat.Mono8, new byte[count]);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new GrayHistogramOperation(frame, 256), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(count, Assert.Single(result.Value!.Bins));
        Assert.Equal(count, result.Value.PixelCount);
        Assert.Equal(0, result.Value.Median);
    }

    [Fact]
    public async Task FrozenCircleCountsOnlyItsFiveBlackPixels_NotBoundingBoxOrPadding()
    {
        var frame = Frame([99, 0, 99, 0, 0, 0, 99, 0, 99], width: 3, padding: 2);
        var roi = new Roi { Kind = RoiKind.Circle, Pose = new(1, 1, 0), Size = new(2, 2) };
        var region = new SearchRegion(roi);
        var request = new GrayHistogramOperation(frame, 1, region);
        roi.Pose = new(100, 100, 0);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, request, default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(5, result.Value!.PixelCount);
        Assert.Equal(5, result.Value.Bins[0]);
        Assert.Equal(0, result.Value.Bins[99]);
        Assert.Equal(0, result.Value.Median);
    }

    [Fact]
    public async Task EmptyRegionAndIllegalBinWidthsAreRejected_NotConvertedToEmptySuccess()
    {
        var frame = Frame([1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => new GrayHistogramOperation(frame, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GrayHistogramOperation(frame, 65537));
        Assert.Throws<ArgumentException>(() => new GrayHistogramOperation(frame, region:
            new SearchRegion(new Roi { Kind = RoiKind.Rectangle, Pose = new(50, 50, 0), Size = new(2, 2) })));
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var invalid = await engine.ExecuteAsync(context, new GrayHistogramOperation(frame, 257), default);
        Assert.Equal(VisionExecutionStatus.Failed, invalid.Status);
        Assert.Equal("HistogramBinWidthMismatch", invalid.Issue!.Code);
        Assert.Null(invalid.Value);
    }

    [Theory]
    [InlineData(PixelFormat.Bgr24, 3)]
    [InlineData(PixelFormat.Rgb24, 3)]
    [InlineData(PixelFormat.Bgra32, 4)]
    [InlineData(PixelFormat.Float32, 4)]
    public async Task UnsupportedStorageIsNotSilentlyConverted(PixelFormat format, int bpp)
    {
        var frame = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 1, 1, format, new byte[bpp]);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new GrayHistogramOperation(frame), default);
        Assert.Equal(VisionExecutionStatus.Unsupported, result.Status);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task PixelBudgetIsValidatedBeforeAllocatingHistogramWork()
    {
        var frame = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 20_000_001, 1, PixelFormat.Mono8, new byte[20_000_001]);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var issues = await engine.ValidateAsync(context, new GrayHistogramOperation(frame), default);
        Assert.Contains(issues, issue => issue.Code == "ImageBudgetExceeded");
    }

    [Fact]
    public async Task HalconSelectionRemainsUnsupported_WithoutCallingSdkOrOpenCvFallback()
    {
        var runtime = new ForbiddenRuntime();
        await using var engine = VisionEngineFactory.Create(new("halcon"), (_, _) => new HalconBackend(runtime));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new GrayHistogramOperation(Frame([1, 2])), default);
        Assert.Equal(VisionExecutionStatus.Unsupported, result.Status);
        Assert.Null(result.Value);
        Assert.Equal(0, runtime.Calls);
    }

    [Fact]
    public void ManagedSnapshotOwnsBinsAndRejectsContradictoryOrEmptyMedian()
    {
        var frame = Frame([0, 0]);
        var bins = new long[256];
        bins[0] = 2;
        var result = new GrayHistogramOutput(frame.FrameId, frame.CoordinateSpaceId, PixelFormat.Mono8, 1, bins, 0);
        bins[0] = 0;
        Assert.Equal(2, result.Bins[0]);
        Assert.Throws<NotSupportedException>(() => ((IList<long>)result.Bins)[0] = 4);
        Assert.Throws<ArgumentException>(() => new GrayHistogramOutput(frame.FrameId, frame.CoordinateSpaceId, PixelFormat.Mono8, 1, bins, 0));
        bins[0] = 1;
        Assert.Throws<ArgumentException>(() => new GrayHistogramOutput(frame.FrameId, frame.CoordinateSpaceId, PixelFormat.Mono8, 1, bins, 0.5));
        Assert.Throws<ArgumentException>(() => new GrayHistogramOutput(frame.FrameId, frame.CoordinateSpaceId, PixelFormat.Mono8, 1, bins, 1));
        bins[0] = -1;
        Assert.Throws<ArgumentException>(() => new GrayHistogramOutput(frame.FrameId, frame.CoordinateSpaceId, PixelFormat.Mono8, 1, bins, 0));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task PublicationRejectsWrongSourceBinWidthCountFormatAndNoMatch(int corruption)
    {
        var backend = new CorruptBackend(corruption);
        await using var engine = VisionEngineFactory.Create(new("opencv"), (_, _) => backend);
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new GrayHistogramOperation(Frame([0, 1, 2, 3]), 16), default);
        Assert.Equal(VisionExecutionStatus.Failed, result.Status);
        Assert.Null(result.Value);
        Assert.Contains(corruption < 2 ? "其他帧" : corruption == 5 ? "未找到状态" : "直方图", result.Issue!.Message);
    }

    private static VisionFrame Frame(ushort[] values, bool mono16 = false, int width = 0, int padding = 0)
    {
        width = width == 0 ? values.Length : width;
        var height = values.Length / width;
        var bpp = mono16 ? 2 : 1;
        var stride = width * bpp + padding;
        var pixels = Enumerable.Repeat((byte)99, stride * height).ToArray();
        for (var index = 0; index < values.Length; index++)
        {
            var offset = index / width * stride + index % width * bpp;
            if (mono16)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(pixels.AsSpan(offset, 2), values[index]);
            }
            else
            {
                pixels[offset] = checked((byte)values[index]);
            }
        }

        return new(Guid.NewGuid(), Guid.NewGuid(), width, height, mono16 ? PixelFormat.Mono16 : PixelFormat.Mono8, pixels, stride);
    }

    private sealed class CorruptBackend(int corruption) : IVisionBackend
    {
        public string Id => "opencv";
        public VisionExecutionDomain Domain => VisionExecutionDomain.Vision;
        public IReadOnlyList<VisionOperationCapability> Capabilities { get; } =
            [new(GrayHistogramOperation.Id, VisionExecutionDomain.Vision, VisionOperationFeatures.ProcessingRegion)];
        public IReadOnlyList<VisionIssue> Validate(VisionOperation operation, IPreparedVisionModel? model) => [];
        public ValueTask<IPreparedVisionModel> PrepareAsync(VisionModelDefinition definition, CancellationToken ct) => throw new NotSupportedException();
        public ValueTask<BackendOutput> ExecuteAsync(IPreparedVisionModel? model, VisionOperation operation, CancellationToken ct)
        {
            var request = (GrayHistogramOperation)operation;
            var format = corruption == 4 ? PixelFormat.Mono16 : PixelFormat.Mono8;
            var width = corruption == 2 ? 17 : 16;
            var bins = new long[((format == PixelFormat.Mono8 ? 256 : 65536) + width - 1) / width];
            bins[0] = corruption == 3 ? 3 : 4;
            return new(new BackendOutput(new GrayHistogramOutput(
                corruption == 0 ? Guid.NewGuid() : request.Image.FrameId,
                corruption == 1 ? Guid.NewGuid() : request.Image.CoordinateSpaceId,
                format, width, bins, 0), corruption == 5));
        }
        public void Dispose() { }
    }

    private sealed class ForbiddenRuntime : IHalconVisionRuntime
    {
        internal int Calls;
        public Exception? CleanupFailure => null;
        public IHalconShapeModel Prepare(TemplateModelDefinition definition) { Calls++; throw new InvalidOperationException(); }
        public BinaryImageOutput Threshold(ThresholdRangeOperation operation) { Calls++; throw new InvalidOperationException(); }
        public GrayStatisticsOutput Statistics(GrayStatisticsOperation operation) { Calls++; throw new InvalidOperationException(); }
        public ImageCropOutput Crop(ImageCropOperation operation) { Calls++; throw new InvalidOperationException(); }
        public void Dispose() { }
    }
}
