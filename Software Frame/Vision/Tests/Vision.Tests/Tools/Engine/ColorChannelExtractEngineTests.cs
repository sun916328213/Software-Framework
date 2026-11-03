using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Imaging;
using Vision.Core.Models;
using Vision.Core.Operations;
using Vision.Core.Results;
using Vision.Tools;
using Vision.Tools.Execution;
using Vision.Tools.Halcon.Engine;
using Xunit;

namespace Vision.Tests.Tools.Engine;

/// <summary>通道提取的真实黑图与不支持边界，及最末端fake注入的发布守卫；不执行HALCON原生。</summary>
[Trait("Category", "Contract")]
public sealed class ColorChannelExtractEngineTests
{
    [Theory]
    [InlineData(PixelFormat.Mono8, 1)]
    [InlineData(PixelFormat.Mono16, 2)]
    [InlineData(PixelFormat.Float32, 4)]
    public async Task GrayAndFloatingInputsAreNotGuessedAsRgb(PixelFormat format, int bytes)
    {
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var frame = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 1, 1, format, new byte[bytes]);
        var result = await engine.ExecuteAsync(context, new ColorChannelExtractOperation(frame, RgbChannel.Red), default);
        Assert.Equal(VisionExecutionStatus.Unsupported, result.Status);
        Assert.Null(result.Value);
    }

    [Theory]
    [InlineData(RgbChannel.Red)]
    [InlineData(RgbChannel.Green)]
    [InlineData(RgbChannel.Blue)]
    public async Task BlackChannelIsSuccessfulImage_NotNoMatch(RgbChannel channel)
    {
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var frame = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 1, 1, PixelFormat.Bgra32, [0, 0, 0, 255]);
        var result = await engine.ExecuteAsync(context, new ColorChannelExtractOperation(frame, channel), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(new byte[] { 0 }, result.Value!.Image.Pixels.ToArray());
    }

    [Fact]
    public async Task ExtractedChannelFeedsHistogramThresholdBlobAndAreaJudgment_AfterProducerContextCloses()
    {
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var producer = await engine.CreateContextAsync(default);
        var input = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 2, 2, PixelFormat.Rgb24,
            [10, 17, 40, 200, 17, 40, 0, 17, 40, 255, 17, 40]);
        var extracted = await engine.ExecuteAsync(producer, new ColorChannelExtractOperation(input, RgbChannel.Red), default);
        Assert.Equal(VisionExecutionStatus.Success, extracted.Status);
        var channel = extracted.Value!.Image;
        await engine.ReleaseContextAsync(producer, default);
        var consumer = await engine.CreateContextAsync(default);
        var histogram = await engine.ExecuteAsync(consumer, new GrayHistogramOperation(channel, 64), default);
        Assert.Equal(VisionExecutionStatus.Success, histogram.Status);
        Assert.Equal(105, histogram.Value!.Median);
        Assert.Equal(new long[] { 2, 0, 0, 2 }, histogram.Value.Bins);
        var binary = await engine.ExecuteAsync(consumer, new ThresholdRangeOperation(channel, 128, 255), default);
        Assert.Equal(VisionExecutionStatus.Success, binary.Status);
        Assert.Equal(new byte[] { 0, 255, 0, 255 }, binary.Value!.Image.Pixels.ToArray());
        var blobs = await engine.ExecuteAsync(consumer, new BlobOperation(binary.Value.Image, 255, 255), default);
        Assert.Equal(VisionExecutionStatus.Success, blobs.Status);
        Assert.Equal(2, Assert.Single(blobs.Value!.Items).Area);
        var verdict = await engine.ExecuteAsync(consumer, BlobJudgmentOperation.ForArea(blobs.Value, 1, 2, 2), default);
        Assert.Equal(VisionExecutionStatus.Success, verdict.Status);
        Assert.True(verdict.Value!.Passed);
        Assert.Equal(input.CoordinateSpaceId, verdict.Value.CoordinateSpaceId);
        Assert.Equal(binary.Value.Image.FrameId, verdict.Value.FrameId);
    }

    [Fact]
    public async Task HalconDoesNotClaimChannelExtractionOrCallSdkOrFallback()
    {
        var runtime = new ForbiddenRuntime();
        await using var engine = VisionEngineFactory.Create(new("halcon"), (_, _) => new HalconBackend(runtime));
        var context = await engine.CreateContextAsync(default);
        var frame = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 1, 1, PixelFormat.Rgb24, [100, 50, 10]);
        var result = await engine.ExecuteAsync(context, new ColorChannelExtractOperation(frame, RgbChannel.Red), default);
        Assert.Equal(VisionExecutionStatus.Unsupported, result.Status);
        Assert.Null(result.Value);
        Assert.Equal(0, runtime.Calls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task PublicationRejectsWrongSourceSpaceDimensionsFormatAndNoMatch(int corruption)
    {
        await using var engine = VisionEngineFactory.Create(new("opencv"), (_, _) => new BadOutputBackend(corruption));
        var context = await engine.CreateContextAsync(default);
        var frame = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 1, 1, PixelFormat.Bgr24, [10, 50, 100]);
        var result = await engine.ExecuteAsync(context, new ColorChannelExtractOperation(frame, RgbChannel.Red), default);
        Assert.Equal(VisionExecutionStatus.Failed, result.Status);
        Assert.Null(result.Value);
        Assert.Contains(corruption < 2 ? "其他帧" : corruption == 4 ? "未找到状态" : "图像变换输出", result.Issue!.Message);
    }

    private sealed class BadOutputBackend(int corruption) : IVisionBackend
    {
        public string Id => "opencv";
        public VisionExecutionDomain Domain => VisionExecutionDomain.Vision;
        public IReadOnlyList<VisionOperationCapability> Capabilities { get; } =
            [new(ColorChannelExtractOperation.Id, VisionExecutionDomain.Vision, VisionOperationFeatures.None)];
        public IReadOnlyList<VisionIssue> Validate(VisionOperation operation, IPreparedVisionModel? model) => [];
        public ValueTask<IPreparedVisionModel> PrepareAsync(VisionModelDefinition definition, CancellationToken ct) => throw new NotSupportedException();
        public ValueTask<BackendOutput> ExecuteAsync(IPreparedVisionModel? model, VisionOperation operation, CancellationToken ct)
        {
            var frame = operation.Image!;
            var image = new VisionFrame(Guid.NewGuid(), corruption == 1 ? Guid.NewGuid() : frame.CoordinateSpaceId,
                corruption == 2 ? 2 : 1, 1, corruption == 3 ? PixelFormat.Mono16 : PixelFormat.Mono8,
                corruption is 2 or 3 ? new byte[2] : [100]);
            return new(new BackendOutput(new ImageTransformOutput(corruption == 0 ? Guid.NewGuid() : frame.FrameId, image), corruption == 4));
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
