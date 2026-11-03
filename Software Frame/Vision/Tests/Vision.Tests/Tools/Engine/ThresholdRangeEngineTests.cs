using System.Buffers.Binary;
using Vision.Core.Engine;
using Vision.Core.Imaging;
using Vision.Core.Models;
using Vision.Core.Operations;
using Vision.Core.Results;
using Vision.Tools;
using Vision.Tools.Halcon.Engine;
using Xunit;

namespace Vision.Tests.Tools.Engine;

[Trait("Category", "Contract")]
public sealed class ThresholdRangeEngineTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OpenCv_ModelFreeRange_IsInclusive_PreservesInputAndCoordinates_AndPacksStride(bool mono16)
    {
        var frame = Frame(mono16);
        var before = frame.Pixels.ToArray();
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var minimum = (ushort)(mono16 ? 1000 : 10);
        var maximum = (ushort)(mono16 ? 2000 : 20);
        var operation = new ThresholdRangeOperation(frame, minimum, maximum);
        Assert.Null(operation.Model);
        var outcome = await engine.ExecuteAsync(context, operation, default);
        Assert.Equal(VisionExecutionStatus.Success, outcome.Status);
        var output = outcome.Value!;
        Assert.Equal(new byte[] { 0, 255, 255, 255, 255, 0 }, output.Image.Pixels.ToArray());
        Assert.Equal(frame.FrameId, output.FrameId);
        Assert.NotEqual(frame.FrameId, output.Image.FrameId);
        Assert.Equal(frame.CoordinateSpaceId, output.Image.CoordinateSpaceId);
        Assert.Equal(frame.Width, output.Image.Width); Assert.Equal(frame.Height, output.Image.Height);
        Assert.Equal(before, frame.Pixels.ToArray());
        var empty = await engine.ExecuteAsync(context, new ThresholdRangeOperation(frame, 0, 0), default);
        Assert.Equal(VisionExecutionStatus.Success, empty.Status);
        Assert.All(empty.Value!.Image.Pixels.ToArray(), pixel => Assert.Equal(0, pixel));
    }

    [Fact]
    public async Task ModelFreeProcessing_DoesNotRequireOrRetireCurrentTemplateModel()
    {
        var frame = Frame(false);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var model = await engine.PrepareModelAsync(context, new TemplateModelDefinition(frame, new(), new()), 1, default);
        var threshold = await engine.ExecuteAsync(context, new ThresholdRangeOperation(frame, 10, 20), default);
        Assert.Equal(VisionExecutionStatus.Success, threshold.Status);
        var match = await engine.ExecuteAsync(context, new TemplateMatchOperation(frame, model, new(), 0.9), default);
        Assert.Equal(VisionExecutionStatus.Success, match.Status);
    }

    [Fact]
    public async Task Halcon_ModelFreePath_UsesSelectedRuntime_AndRejectsWrongSourceResult()
    {
        var runtime = new ProbeHalconRuntime();
        await using var engine = VisionEngineFactory.CreateConfigured(new("halcon"), (_, id, _) =>
        { Assert.Equal("halcon", id); return new HalconBackend(runtime); });
        var context = await engine.CreateContextAsync(default);
        var frame = Frame(true);
        var operation = new ThresholdRangeOperation(frame, 1000, 2000);
        var output = await engine.ExecuteAsync(context, operation, default);
        Assert.Equal(VisionExecutionStatus.Success, output.Status);
        Assert.Equal(new byte[] { 0, 255, 255, 255, 255, 0 }, output.Value!.Image.Pixels.ToArray());
        Assert.Equal(1, runtime.Calls);
        runtime.WrongSource = true;
        var rejected = await engine.ExecuteAsync(context, operation, default);
        Assert.Equal(VisionExecutionStatus.Failed, rejected.Status); Assert.Null(rejected.Value);
    }

    [Fact]
    public async Task InvalidBoundsAndUnsupportedColour_AreNotClampedOrConverted()
    {
        var frame = Frame(false);
        Assert.Throws<ArgumentException>(() => new ThresholdRangeOperation(frame, 20, 10));
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var invalid = new ThresholdRangeOperation(frame, 0, 256);
        Assert.Contains(await engine.ValidateAsync(context, invalid, default), item => item.Code == "ThresholdRangeMismatch");
        Assert.Equal(VisionExecutionStatus.Failed, (await engine.ExecuteAsync(context, invalid, default)).Status);
        var colour = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 1, 1, PixelFormat.Bgr24, new byte[3]);
        var unsupported = await engine.ExecuteAsync(context, new ThresholdRangeOperation(colour, 0, 255), default);
        Assert.Equal(VisionExecutionStatus.Unsupported, unsupported.Status); Assert.Null(unsupported.Value);
    }

    private sealed class ProbeHalconRuntime : IHalconVisionRuntime
    {
        public Exception? CleanupFailure => null;
        public ImageCropOutput Crop(ImageCropOperation operation) => throw new NotSupportedException();
        public GrayStatisticsOutput Statistics(GrayStatisticsOperation operation) => throw new NotSupportedException();
        internal bool WrongSource;
        internal int Calls;
        public IHalconShapeModel Prepare(TemplateModelDefinition definition) => throw new InvalidOperationException("Model-free processing must not prepare a model");
        public BinaryImageOutput Threshold(ThresholdRangeOperation operation)
        {
            Calls++;
            Assert.Null(operation.Model);
            Assert.Equal(1000, operation.Minimum); Assert.Equal(2000, operation.Maximum);
            var source = operation.Image;
            return new(WrongSource ? Guid.NewGuid() : source.FrameId,
                new VisionFrame(Guid.NewGuid(), source.CoordinateSpaceId, 3, 2, PixelFormat.Mono8, [0, 255, 255, 255, 255, 0]));
        }
        public void Dispose() { }
    }

    internal static VisionFrame Frame(bool mono16)
    {
        var values = mono16 ? new ushort[] { 999, 1000, 1001, 1999, 2000, 2001 } : [9, 10, 11, 19, 20, 21];
        var stride = mono16 ? 8 : 5;
        var pixels = Enumerable.Repeat((byte)73, stride * 2).ToArray();
        for (var row = 0; row < 2; row++)
            for (var col = 0; col < 3; col++)
                if (mono16) BinaryPrimitives.WriteUInt16LittleEndian(pixels.AsSpan(row * stride + col * 2), values[row * 3 + col]);
                else pixels[row * stride + col] = (byte)values[row * 3 + col];
        return new(Guid.NewGuid(), Guid.NewGuid(), 3, 2, mono16 ? PixelFormat.Mono16 : PixelFormat.Mono8, pixels, stride);
    }
}
