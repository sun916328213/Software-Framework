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
using Vision.Tools.Halcon.Engine;
using Xunit;

namespace Vision.Tests.Tools.Engine;

[Trait("Category", "Contract")]
public sealed class ImageProcessingDomainTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OpenCv_ThresholdAndStatistics_UseSameFrozenRegion_AndIgnorePadding(bool mono16)
    {
        var frame = Frame(mono16);
        var roi = Rectangle(2, 1, 2, 2);
        var region = new SearchRegion(roi);
        var minimum = (ushort)(mono16 ? 1500 : 15);
        var maximum = (ushort)(mono16 ? 2000 : 20);
        var threshold = new ThresholdRangeOperation(frame, minimum, maximum, region);
        var statistics = new GrayStatisticsOperation(frame, region);
        roi.Pose = new Pose2D(100, 100, 0);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);

        var binary = await engine.ExecuteAsync(context, threshold, default);
        Assert.Equal(VisionExecutionStatus.Success, binary.Status);
        Assert.Equal(new byte[] { 0, 0, 255, 0, 0, 255, 255, 0, 0, 0, 0, 0 }, binary.Value!.Image.Pixels.ToArray());
        var black = await engine.ExecuteAsync(context, new ThresholdRangeOperation(frame, 0, 0, region), default);
        Assert.Equal(VisionExecutionStatus.Success, black.Status);
        Assert.All(black.Value!.Image.Pixels.ToArray(), pixel => Assert.Equal(0, pixel));
        var measured = await engine.ExecuteAsync(context, statistics, default);
        Assert.Equal(VisionExecutionStatus.Success, measured.Status);
        Assert.Equal(4, measured.Value!.PixelCount);
        Assert.Equal(mono16 ? 1000 : 10, measured.Value.Minimum);
        Assert.Equal(mono16 ? 2000 : 20, measured.Value.Maximum);
        Assert.Equal(mono16 ? 1625 : 16.25, measured.Value.Mean, 9);
        Assert.Equal(Math.Sqrt(mono16 ? 171875 : 17.1875), measured.Value.PopulationStandardDeviation, 9);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OpenCv_CropCreatesIndependentSpace_MapsPixelCenters_AndFeedsNextOperation(bool mono16)
    {
        var frame = Frame(mono16);
        var before = frame.Pixels.ToArray();
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new ImageCropOperation(frame, 1, 0, 2, 2), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        var crop = result.Value!;
        Assert.NotEqual(frame.FrameId, crop.Image.FrameId);
        Assert.NotEqual(frame.CoordinateSpaceId, crop.Image.CoordinateSpaceId);
        Assert.Equal(frame.FrameId, crop.FrameId);
        Assert.Equal(frame.CoordinateSpaceId, crop.SourceCoordinateSpaceId);
        Assert.Equal(frame.Format, crop.Image.Format);
        var values = new ushort[4];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = mono16
                ? BinaryPrimitives.ReadUInt16LittleEndian(crop.Image.Pixels.Slice(i * 2, 2))
                : crop.Image.Pixels[i];
        }
        Assert.Equal(mono16 ? new ushort[] { 1000, 2000, 1500, 2000 } : [10, 20, 15, 20], values);
        Assert.Equal(new Point2D(1, 0), crop.ImageToSource.Transform(new Point2D(0, 0)));
        Assert.Equal(new Point2D(2, 1), crop.ImageToSource.Transform(new Point2D(1, 1)));
        Assert.Equal(before, frame.Pixels.ToArray());
        var minimum = (ushort)(mono16 ? 1500 : 15);
        var maximum = (ushort)(mono16 ? 2000 : 20);
        var binary = await engine.ExecuteAsync(context, new ThresholdRangeOperation(crop.Image, minimum, maximum), default);
        Assert.Equal(VisionExecutionStatus.Success, binary.Status);
        Assert.Equal(new byte[] { 0, 255, 255, 255 }, binary.Value!.Image.Pixels.ToArray());
        Assert.Equal(crop.Image.FrameId, binary.Value.FrameId);
        Assert.Equal(crop.Image.CoordinateSpaceId, binary.Value.Image.CoordinateSpaceId);
        var nested = await engine.ExecuteAsync(context, new ImageCropOperation(crop.Image, 1, 1, 1, 1), default);
        Assert.Equal(VisionExecutionStatus.Success, nested.Status);
        Assert.Equal(crop.Image.FrameId, nested.Value!.FrameId);
        Assert.Equal(crop.Image.CoordinateSpaceId, nested.Value.SourceCoordinateSpaceId);
        Assert.Equal(new Point2D(2, 1), crop.ImageToSource.Transform(
            nested.Value.ImageToSource.Transform(new Point2D(0, 0))));
    }

    [Theory]
    [InlineData(PixelFormat.Rgb24, 3)]
    [InlineData(PixelFormat.Bgr24, 3)]
    [InlineData(PixelFormat.Bgra32, 4)]
    [InlineData(PixelFormat.Float32, 4)]
    public async Task OpenCv_CropCopiesWholeMultibytePixels_AndIgnoresPadding(PixelFormat format, int bytesPerPixel)
    {
        var stride = 3 * bytesPerPixel + 2;
        var pixels = Enumerable.Repeat((byte)99, stride * 2).ToArray();
        for (var y = 0; y < 2; y++)
        {
            for (var x = 0; x < 3; x++)
            {
                Array.Fill(pixels, (byte)(10 * y + x + 1), y * stride + x * bytesPerPixel, bytesPerPixel);
            }
        }
        var frame = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 3, 2, format, pixels, stride);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new ImageCropOperation(frame, 1, 0, 2, 2), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(new byte[] { 2, 3, 12, 13 }.SelectMany(value => Enumerable.Repeat(value, bytesPerPixel)), result.Value!.Image.Pixels.ToArray());
        Assert.Equal(format, result.Value.Image.Format);
        Assert.Equal(frame.FrameId, result.Value.FrameId);
        Assert.Equal(frame.CoordinateSpaceId, result.Value.SourceCoordinateSpaceId);
        Assert.NotEqual(frame.FrameId, result.Value.Image.FrameId);
        Assert.NotEqual(frame.CoordinateSpaceId, result.Value.Image.CoordinateSpaceId);
        Assert.Equal(new Point2D(1, 0), result.Value.ImageToSource.Transform(new Point2D(0, 0)));
        await engine.ReleaseContextAsync(context, default);
        Array.Clear(pixels);
        Assert.Equal((byte)2, result.Value.Image.Pixels[0]);
    }

    [Fact]
    public void EmptyRegionAndOutOfBoundsCrop_AreRejected_WithoutInventingPixels()
    {
        var frame = Frame(false);
        var outside = new SearchRegion(Rectangle(100, 100, 2, 2));
        Assert.Throws<ArgumentException>(() => new ThresholdRangeOperation(frame, 0, 255, outside));
        Assert.Throws<ArgumentException>(() => new GrayStatisticsOperation(frame, outside));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ImageCropOperation(frame, 3, 0, 2, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ImageCropOperation(frame, int.MaxValue, 0, int.MaxValue, 1));
    }

    [Fact]
    public void NumericRequest_DoesNotRequireAFakeFrame_ButImageRequestRejectsNull()
    {
        var request = new NumericProbe();
        Assert.Null(request.Image);
        Assert.Null(request.Model);
        Assert.Throws<ArgumentNullException>(() => new ImageCropOperation(null!, 0, 0, 1, 1));
    }

    [Fact]
    public async Task HalconCrop_UsesSelectedSdkBoundary_AndRejectsIncorrectMapping()
    {
        var runtime = new CropRuntime();
        await using var engine = VisionEngineFactory.CreateConfigured(new("halcon"), (_, _, _) => new HalconBackend(runtime));
        var context = await engine.CreateContextAsync(default);
        var operation = new ImageCropOperation(Frame(true), 1, 0, 2, 2);
        var result = await engine.ExecuteAsync(context, operation, default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(1, runtime.Crops);
        runtime.WrongOrigin = true;
        var rejected = await engine.ExecuteAsync(context, operation, default);
        Assert.Equal(VisionExecutionStatus.Failed, rejected.Status);
        Assert.Null(rejected.Value);
    }

    private sealed record NumericOutput(double Value);
    private sealed class NumericProbe() : VisionOperation<NumericOutput>
    {
        public override string OperationId => "test.numeric";
        public override VisionExecutionDomain Domain => VisionExecutionDomain.Vision;
        public override VisionOperationFeatures RequiredFeatures => VisionOperationFeatures.None;
        protected override void ValidateParameters(List<VisionIssue> issues) { }
    }

    private sealed class CropRuntime : IHalconVisionRuntime
    {
        public Exception? CleanupFailure => null;
        internal int Crops;
        internal bool WrongOrigin;
        public IHalconShapeModel Prepare(TemplateModelDefinition definition) => throw new NotSupportedException();
        public BinaryImageOutput Threshold(ThresholdRangeOperation operation) => throw new NotSupportedException();
        public GrayStatisticsOutput Statistics(GrayStatisticsOperation operation) => throw new NotSupportedException();
        public ImageCropOutput Crop(ImageCropOperation operation)
        {
            Crops++;
            return new ImageCropOutput(operation.Image.FrameId, operation.Image.CoordinateSpaceId,
                new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 2, 2, PixelFormat.Mono16, new byte[8]),
                WrongOrigin ? operation.X + 1 : operation.X, operation.Y);
        }
        public void Dispose() { }
    }

    private static Roi Rectangle(double x, double y, double width, double height) => new()
    {
        Kind = RoiKind.Rectangle,
        Pose = new Pose2D(x, y, 0),
        Size = new Size2D(width, height)
    };

    private static VisionFrame Frame(bool mono16)
    {
        ushort[] values = mono16
            ? [999, 1000, 2000, 65535, 0, 1500, 2000, 0, 2500, 999, 2001, 42]
            : [99, 10, 20, 255, 0, 15, 20, 0, 25, 9, 21, 42];
        var stride = mono16 ? 10 : 6;
        var pixels = Enumerable.Repeat((byte)221, stride * 3).ToArray();
        for (var y = 0; y < 3; y++)
        {
            for (var x = 0; x < 4; x++)
            {
                if (mono16)
                {
                    BinaryPrimitives.WriteUInt16LittleEndian(pixels.AsSpan(y * stride + x * 2), values[y * 4 + x]);
                }
                else
                {
                    pixels[y * stride + x] = (byte)values[y * 4 + x];
                }
            }
        }
        return new(Guid.NewGuid(), Guid.NewGuid(), 4, 3, mono16 ? PixelFormat.Mono16 : PixelFormat.Mono8, pixels, stride);
    }
}
