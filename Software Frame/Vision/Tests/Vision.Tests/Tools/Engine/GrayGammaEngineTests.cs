using System.Buffers.Binary;
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

[Trait("Category", "Contract")]
public sealed class GrayGammaEngineTests
{
    [Theory]
    [InlineData(false, 2, 16, 64)]
    [InlineData(false, 0.5, 128, 181)]
    [InlineData(true, 2, 4096, 16384)]
    [InlineData(true, 0.5, 32768, 46341)]
    public async Task ExplicitExponentPreservesEndpointsBitDepthAndPadding(bool mono16, double gamma, ushort first, ushort second)
    {
        ushort[] values = mono16 ? [0, 16384, 32768, 65535] : [0, 64, 128, 255];
        var frame = Frame(values, mono16);
        var original = frame.Pixels.ToArray();
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var request = new GrayGammaOperation(frame, gamma);
        Assert.Null(request.Model);
        var result = await engine.ExecuteAsync(context, request, default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(new ushort[] { 0, first, second, mono16 ? ushort.MaxValue : (ushort)255 }, Values(result.Value!.Image));
        Assert.Equal(frame.FrameId, result.Value.FrameId);
        Assert.NotEqual(frame.FrameId, result.Value.Image.FrameId);
        Assert.Equal(frame.CoordinateSpaceId, result.Value.Image.CoordinateSpaceId);
        Assert.Equal(frame.Format, result.Value.Image.Format);
        Assert.Equal(original, frame.Pixels.ToArray());
        await engine.ReleaseContextAsync(context, default);
        Assert.Equal(first, Values(result.Value.Image)[1]);
    }

    [Fact]
    public async Task IdentityGammaPreservesEvery16BitEncoding_WithoutConvertingTo8Bit()
    {
        var values = Enumerable.Range(0, 65536).Select(value => (ushort)value).ToArray();
        var frame = Frame(values, true);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new GrayGammaOperation(frame, 1), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(values, Values(result.Value!.Image));
    }

    [Theory]
    [InlineData(double.Epsilon, 255)]
    [InlineData(double.MaxValue, 0)]
    public async Task ExtremeFinitePositiveExponentsRemainBounded_WithUnchangedEndpoints(double gamma, ushort middle)
    {
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new GrayGammaOperation(Frame([0, 128, 255]), gamma), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(new ushort[] { 0, middle, 255 }, Values(result.Value!.Image));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void InvalidExponentIsRejectedBeforeExecution(double gamma)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GrayGammaOperation(Frame([1]), gamma));
    }

    [Theory]
    [InlineData(PixelFormat.Rgb24, 3)]
    [InlineData(PixelFormat.Bgr24, 3)]
    [InlineData(PixelFormat.Bgra32, 4)]
    [InlineData(PixelFormat.Float32, 4)]
    public async Task UnsupportedFormatsAreNotImplicitlyConverted(PixelFormat format, int bytes)
    {
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var frame = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 1, 1, format, new byte[bytes]);
        var result = await engine.ExecuteAsync(context, new GrayGammaOperation(frame, 2), default);
        Assert.Equal(VisionExecutionStatus.Unsupported, result.Status);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task ParametersAreFrozen_AndGammaIsNotPerFrameNormalization()
    {
        byte[] input = [64, 128];
        var frame = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 2, 1, PixelFormat.Mono8, input);
        var gamma = 2d;
        var request = new GrayGammaOperation(frame, gamma);
        gamma = 0.5;
        Array.Clear(input);
        Assert.NotEqual(gamma, request.Gamma);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, request, default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(new ushort[] { 16, 64 }, Values(result.Value!.Image));
    }

    [Fact]
    public async Task ImageBudgetIsValidatedBeforeLookupAndOutputAllocation()
    {
        var frame = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 20_000_001, 1, PixelFormat.Mono8, new byte[20_000_001]);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var issues = await engine.ValidateAsync(context, new GrayGammaOperation(frame, 2), default);
        Assert.Contains(issues, issue => issue.Code == "ImageBudgetExceeded");
    }

    [Fact]
    public async Task CorrectedImageFeedsThresholdBlobAndCount_WithoutAlteringSource()
    {
        var frame = Frame([0, 64, 128, 255]);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var corrected = await engine.ExecuteAsync(context, new GrayGammaOperation(frame, 2), default);
        Assert.Equal(VisionExecutionStatus.Success, corrected.Status);
        var binary = await engine.ExecuteAsync(context, new ThresholdRangeOperation(corrected.Value!.Image, 64, 255), default);
        Assert.Equal(VisionExecutionStatus.Success, binary.Status);
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, binary.Value!.Image.Pixels.ToArray());
        var blobs = await engine.ExecuteAsync(context, new BlobOperation(binary.Value.Image, 255, 255), default);
        Assert.Equal(VisionExecutionStatus.Success, blobs.Status);
        Assert.Equal(2, Assert.Single(blobs.Value!.Items).Area);
        var count = await engine.ExecuteAsync(context, BlobJudgmentOperation.ForCount(blobs.Value, 1, 1), default);
        Assert.Equal(VisionExecutionStatus.Success, count.Status);
        Assert.True(count.Value!.Passed);
        Assert.Equal(new ushort[] { 0, 64, 128, 255 }, Values(frame));
    }

    [Fact]
    public async Task HalconRemainsUnsupported_WithoutSdkOrOpenCvFallback()
    {
        var runtime = new ForbiddenRuntime();
        await using var engine = VisionEngineFactory.Create(new("halcon"), (_, _) => new HalconBackend(runtime));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new GrayGammaOperation(Frame([0, 255]), 2), default);
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
        await using var engine = VisionEngineFactory.Create(new("opencv"), (_, _) => new BadBackend(corruption));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new GrayGammaOperation(Frame([10]), 2), default);
        Assert.Equal(VisionExecutionStatus.Failed, result.Status);
        Assert.Null(result.Value);
        Assert.Contains(corruption < 2 ? "其他帧" : corruption == 4 ? "未找到状态" : "图像变换输出", result.Issue!.Message);
    }

    private static VisionFrame Frame(ushort[] values, bool mono16 = false)
    {
        var bpp = mono16 ? 2 : 1;
        var pixels = Enumerable.Repeat((byte)99, values.Length * bpp + 3).ToArray();
        for (var index = 0; index < values.Length; index++)
        {
            if (mono16)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(pixels.AsSpan(index * 2, 2), values[index]);
            }
            else
            {
                pixels[index] = checked((byte)values[index]);
            }
        }

        return new(Guid.NewGuid(), Guid.NewGuid(), values.Length, 1, mono16 ? PixelFormat.Mono16 : PixelFormat.Mono8, pixels, pixels.Length);
    }

    private static ushort[] Values(VisionFrame frame)
    {
        var values = new ushort[frame.Width * frame.Height];
        for (var index = 0; index < values.Length; index++)
        {
            var offset = index / frame.Width * frame.Stride + index % frame.Width * (frame.Format == PixelFormat.Mono16 ? 2 : 1);
            values[index] = frame.Format == PixelFormat.Mono16
                ? BinaryPrimitives.ReadUInt16LittleEndian(frame.Pixels.Slice(offset, 2)) : frame.Pixels[offset];
        }

        return values;
    }

    private sealed class BadBackend(int corruption) : IVisionBackend
    {
        public string Id => "opencv";
        public VisionExecutionDomain Domain => VisionExecutionDomain.Vision;
        public IReadOnlyList<VisionOperationCapability> Capabilities { get; } =
            [new(GrayGammaOperation.Id, VisionExecutionDomain.Vision, VisionOperationFeatures.None)];
        public IReadOnlyList<VisionIssue> Validate(VisionOperation operation, IPreparedVisionModel? model) => [];
        public ValueTask<IPreparedVisionModel> PrepareAsync(VisionModelDefinition definition, CancellationToken ct) => throw new NotSupportedException();
        public ValueTask<BackendOutput> ExecuteAsync(IPreparedVisionModel? model, VisionOperation operation, CancellationToken ct)
        {
            var source = operation.Image!;
            var output = new VisionFrame(Guid.NewGuid(), corruption == 1 ? Guid.NewGuid() : source.CoordinateSpaceId,
                corruption == 2 ? 2 : 1, 1, corruption == 3 ? PixelFormat.Mono16 : PixelFormat.Mono8,
                corruption is 2 or 3 ? new byte[2] : [1]);
            return new(new BackendOutput(new ImageTransformOutput(corruption == 0 ? Guid.NewGuid() : source.FrameId, output), corruption == 4));
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
