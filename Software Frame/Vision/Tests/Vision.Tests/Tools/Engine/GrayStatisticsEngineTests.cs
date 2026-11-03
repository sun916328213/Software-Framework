using System.Buffers.Binary;
using Vision.Core.Imaging;
using Vision.Core.Models;
using Vision.Tools.Halcon.Engine;
using Vision.Core.Operations;
using Vision.Core.Results;
using Vision.Tools;
using Xunit;

namespace Vision.Tests.Tools.Engine;

[Trait("Category", "Contract")]
public sealed class GrayStatisticsEngineTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OpenCv_ReportsPopulationStatisticsWithoutLosing16BitOrReadingPadding(bool mono16)
    {
        var frame = Frame(mono16);
        var before = frame.Pixels.ToArray();
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var operation = new GrayStatisticsOperation(frame);
        Assert.Null(operation.Model);
        var result = await engine.ExecuteAsync(context, operation, default);
        Assert.True(result.Status == VisionExecutionStatus.Success, result.Issue?.Message ?? result.Status.ToString());
        AssertStatistics(frame, result.Value!, mono16);
        Assert.Equal(before, frame.Pixels.ToArray());
    }

    [Fact]
    public async Task SinglePixelIsZeroDeviation_AndColourIsNotSilentlyConverted()
    {
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var single = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 1, 1, PixelFormat.Mono16, [255, 255]);
        var result = await engine.ExecuteAsync(context, new GrayStatisticsOperation(single), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(65535, result.Value!.Mean); Assert.Equal(0, result.Value.PopulationStandardDeviation);
        var colour = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 1, 1, PixelFormat.Bgr24, new byte[3]);
        var rejected = await engine.ExecuteAsync(context, new GrayStatisticsOperation(colour), default);
        Assert.Equal(VisionExecutionStatus.Unsupported, rejected.Status); Assert.Null(rejected.Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StatisticsRange_PreservesBitDepthAndSnapshot_AndNgIsSuccess(bool mono16)
    {
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new GrayStatisticsOperation(Frame(mono16)), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        var expected = mono16 ? 2000d : 1d;
        Assert.Equal(mono16 ? PixelFormat.Mono16 : PixelFormat.Mono8, result.Value!.Format);
        await engine.ReleaseContextAsync(context, default);
        var evaluation = await engine.CreateContextAsync(default);
        var ok = await engine.ExecuteAsync(evaluation, new GrayStatisticsRangeOperation(result.Value, GrayStatisticsMetric.Mean, expected, expected), default);
        var ng = await engine.ExecuteAsync(evaluation, new GrayStatisticsRangeOperation(result.Value, GrayStatisticsMetric.Mean, expected + 1, expected + 2), default);
        Assert.Equal(VisionExecutionStatus.Success, ok.Status);
        Assert.Equal(VisionExecutionStatus.Success, ng.Status);
        Assert.True(ok.Value!.Passed);
        Assert.False(ng.Value!.Passed);
        Assert.Same(result.Value, ok.Value.Source);
        Assert.Equal(expected, ok.Value.Value);
    }

    [Theory]
    [InlineData(GrayStatisticsMetric.Minimum, 0)]
    [InlineData(GrayStatisticsMetric.Maximum, 4)]
    [InlineData(GrayStatisticsMetric.Mean, 1.5)]
    [InlineData(GrayStatisticsMetric.PopulationStandardDeviation, 1.6583123951777)]
    public async Task StatisticsRange_SelectsTheDeclaredMetricWithoutRounding(GrayStatisticsMetric metric, double expected)
    {
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var input = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 4, 1, PixelFormat.Mono8, [0, 0, 2, 4]);
        var measured = await engine.ExecuteAsync(context, new GrayStatisticsOperation(input), default);
        Assert.Equal(VisionExecutionStatus.Success, measured.Status);
        var result = await engine.ExecuteAsync(context, new GrayStatisticsRangeOperation(measured.Value!, metric, expected - 1e-9, expected + 1e-9), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(metric, result.Value!.Metric);
        Assert.Equal(expected, result.Value.Value, 9);
        Assert.True(result.Value.Passed);
    }

    [Fact]
    public async Task WrongBackendBitDepth_IsRejectedBeforePublication()
    {
        await using var engine = VisionEngineFactory.CreateConfigured(new("halcon"), (_, _, _) => new HalconBackend(new WrongFormatRuntime()));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new GrayStatisticsOperation(Frame(false)), default);
        Assert.Equal(VisionExecutionStatus.Failed, result.Status);
        Assert.Null(result.Value);
    }

    [Fact]
    public void StatisticsAndRange_RejectMissingFormatInvalidValuesAndBadLimits()
    {
        var frame = Guid.NewGuid();
        var space = Guid.NewGuid();
        Assert.Throws<ArgumentOutOfRangeException>(() => new GrayStatisticsOutput(frame, space, PixelFormat.Bgr24, 1, 0, 0, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GrayStatisticsOutput(frame, space, PixelFormat.Mono8, 1, 0, 256, 0, 0));
        var source = new GrayStatisticsOutput(frame, space, PixelFormat.Mono8, 1, 0, 0, 0, 0);
        Assert.Throws<ArgumentNullException>(() => new GrayStatisticsRangeOperation(null!, GrayStatisticsMetric.Mean, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GrayStatisticsRangeOperation(source, (GrayStatisticsMetric)99, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GrayStatisticsRangeOperation(source, GrayStatisticsMetric.Mean, 1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GrayStatisticsRangeOperation(source, GrayStatisticsMetric.Mean, double.NaN, 1));
    }

    private sealed class WrongFormatRuntime : IHalconVisionRuntime
    {
        public Exception? CleanupFailure => null;
        public IHalconShapeModel Prepare(TemplateModelDefinition definition) => throw new NotSupportedException();
        public BinaryImageOutput Threshold(ThresholdRangeOperation operation) => throw new NotSupportedException();
        public ImageCropOutput Crop(ImageCropOperation operation) => throw new NotSupportedException();
        public GrayStatisticsOutput Statistics(GrayStatisticsOperation operation)
            => new(operation.Image.FrameId, operation.Image.CoordinateSpaceId, PixelFormat.Mono16, 4, 0, 2, 1, 1);
        public void Dispose()
        {
        }
    }

    internal static void AssertStatistics(VisionFrame frame, GrayStatisticsOutput result, bool mono16)
    {
        Assert.Equal(frame.FrameId, result.FrameId); Assert.Equal(frame.CoordinateSpaceId, result.CoordinateSpaceId);
        Assert.Equal(frame.Format, result.Format);
        Assert.Equal(4, result.PixelCount);
        Assert.Equal(mono16 ? 1000 : 0, result.Minimum, 9);
        Assert.Equal(mono16 ? 3000 : 2, result.Maximum, 9);
        Assert.Equal(mono16 ? 2000 : 1, result.Mean, 9);
        Assert.Equal(mono16 ? 1000 : 1, result.PopulationStandardDeviation, 9);
    }

    internal static VisionFrame Frame(bool mono16)
    {
        var values = mono16 ? new ushort[] { 1000, 1000, 3000, 3000 } : [0, 0, 2, 2];
        var stride = mono16 ? 8 : 5;
        var bytes = Enumerable.Repeat((byte)73, stride * 2).ToArray();
        for (var row = 0; row < 2; row++)
            for (var col = 0; col < 2; col++)
                if (mono16) BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(row * stride + col * 2), values[row * 2 + col]);
                else bytes[row * stride + col] = (byte)values[row * 2 + col];
        return new(Guid.NewGuid(), Guid.NewGuid(), 2, 2, mono16 ? PixelFormat.Mono16 : PixelFormat.Mono8, bytes, stride);
    }
}
