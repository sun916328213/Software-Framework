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
using Xunit;

namespace Vision.Tests.Tools.Engine;

[Trait("Category", "Contract")]
public sealed class PixelScaleCalibrationEngineTests
{
    [Theory]
    [InlineData(GeometryMeasurementKind.PointDistance)]
    [InlineData(GeometryMeasurementKind.PointLineDistance)]
    [InlineData(GeometryMeasurementKind.CaliperPairMeanWidth)]
    [InlineData(GeometryMeasurementKind.CaliperCircleDiameter)]
    public async Task KnownLength_CalibratesExplicitScale_AndConvertsLengthKindsWithoutSdk(GeometryMeasurementKind kind)
    {
        var frame = Guid.NewGuid();
        var space = Guid.NewGuid();
        await using var engine = CommonEngine();
        var context = await engine.CreateContextAsync(default);
        var request = new PixelScaleCalibrationOperation(new(frame, space, new(0, 0)), new(frame, space, new(3, 4)), 10);
        Assert.Null(request.Image);
        var calibrated = await engine.ExecuteAsync(context, request, default);
        Assert.Equal(VisionExecutionStatus.Success, calibrated.Status);
        Assert.Equal(5, calibrated.Value!.ReferencePixelLength);
        Assert.Equal(2, calibrated.Value.MillimetersPerPixel);
        Assert.NotEqual(Guid.Empty, calibrated.Value.CalibrationId);
        // 同一像素空间的后续帧允许复用比例，不凭原标定FrameId限定一次曝光。
        var source = new ScalarMeasurementOutput(Guid.NewGuid(), space, kind, 7.5);
        var converted = await engine.ExecuteAsync(context, new LengthToMillimetersOperation(source, calibrated.Value), default);
        Assert.Equal(VisionExecutionStatus.Success, converted.Status);
        Assert.Equal(15, converted.Value!.ValueMillimeters);
        Assert.Equal(source.FrameId, converted.Value.FrameId);
        Assert.Same(source, converted.Value.SourcePixels);
        Assert.Same(calibrated.Value, converted.Value.Calibration);
        Assert.Equal(7.5, source.Value);
        Assert.Equal(GeometryMeasurementUnit.Pixels, source.Unit);
    }

    [Fact]
    public async Task MillimeterTolerance_IsInclusive_NgIsSuccess_AndCalibrationIdentityRemainsTraceable()
    {
        var frame = Guid.NewGuid();
        var space = Guid.NewGuid();
        await using var engine = CommonEngine();
        var context = await engine.CreateContextAsync(default);
        var calibration = await engine.ExecuteAsync(context,
            new PixelScaleCalibrationOperation(new(frame, space, new(0, 0)), new(frame, space, new(10, 0)), 1), default);
        Assert.Equal(VisionExecutionStatus.Success, calibration.Status);
        var length = await engine.ExecuteAsync(context,
            new LengthToMillimetersOperation(new(frame, space, GeometryMeasurementKind.PointDistance, 20), calibration.Value!), default);
        Assert.Equal(VisionExecutionStatus.Success, length.Status);
        var ok = await engine.ExecuteAsync(context, new MillimeterRangeOperation(length.Value!, 2, 2), default);
        var ng = await engine.ExecuteAsync(context, new MillimeterRangeOperation(length.Value!, 2.1, 3), default);
        Assert.Equal(VisionExecutionStatus.Success, ok.Status);
        Assert.True(ok.Value!.Passed);
        Assert.Equal(VisionExecutionStatus.Success, ng.Status);
        Assert.False(ng.Value!.Passed);
        Assert.Equal(calibration.Value!.CalibrationId, ok.Value.Measurement.CalibrationId);
        var zero = await engine.ExecuteAsync(context, new LengthToMillimetersOperation(
            new(frame, space, GeometryMeasurementKind.PointDistance, 0), calibration.Value), default);
        Assert.Equal(VisionExecutionStatus.Success, zero.Status);
        Assert.Equal(0, zero.Value!.ValueMillimeters);
    }

    [Fact]
    public async Task SpaceAndUnitMismatch_AreRejected_InsteadOfRelabelingPixelsOrAngles()
    {
        var frame = Guid.NewGuid();
        var space = Guid.NewGuid();
        await using var engine = CommonEngine();
        var context = await engine.CreateContextAsync(default);
        var calibrated = await engine.ExecuteAsync(context,
            new PixelScaleCalibrationOperation(new(frame, space, new(0, 0)), new(frame, space, new(10, 0)), 1), default);
        Assert.Equal(VisionExecutionStatus.Success, calibrated.Status);
        Assert.Throws<ArgumentException>(() => new LengthToMillimetersOperation(
            new(frame, space, GeometryMeasurementKind.DirectedLineAngle, 45), calibrated.Value!));
        Assert.Throws<ArgumentException>(() => new LengthToMillimetersOperation(
            new(frame, Guid.NewGuid(), GeometryMeasurementKind.PointDistance, 10), calibrated.Value!));
        Assert.Throws<ArgumentException>(() => new MillimeterLengthOutput(
            new(frame, space, GeometryMeasurementKind.PointDistance, 10), calibrated.Value!, 10));
    }

    [Theory]
    [InlineData(1e308, 2)]
    [InlineData(1e-308, 1e-308)]
    public async Task OverflowAndPositiveLengthUnderflow_FailWithoutNonFiniteOrFalseZero(double knownMillimeters, double pixels)
    {
        var frame = Guid.NewGuid();
        var space = Guid.NewGuid();
        await using var engine = CommonEngine();
        var context = await engine.CreateContextAsync(default);
        var calibrated = await engine.ExecuteAsync(context,
            new PixelScaleCalibrationOperation(new(frame, space, new(0, 0)), new(frame, space, new(1, 0)), knownMillimeters), default);
        Assert.Equal(VisionExecutionStatus.Success, calibrated.Status);
        var converted = await engine.ExecuteAsync(context, new LengthToMillimetersOperation(
            new(frame, space, GeometryMeasurementKind.PointDistance, pixels), calibrated.Value!), default);
        Assert.Equal(VisionExecutionStatus.Failed, converted.Status);
        Assert.Null(converted.Value);
    }

    [Fact]
    public void DegenerateOrUnlabeledReferences_AreRejectedBeforeCalibration()
    {
        var frame = Guid.NewGuid();
        var space = Guid.NewGuid();
        var first = new PixelPoint(frame, space, new(0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PixelScaleCalibrationOperation(first, first, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PixelScaleCalibrationOperation(first, new(frame, space, new(0.5, 0)), 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PixelScaleCalibrationOperation(first, new(frame, space, new(10, 0)), double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PixelScaleCalibrationOperation(first, new(frame, space, new(10, 0)), 0));
        Assert.Throws<ArgumentException>(() => new PixelScaleCalibrationOperation(first, new(Guid.NewGuid(), space, new(10, 0)), 1));
    }

    [Fact]
    public async Task ActualTemplateFollowWidth_ToExplicitUniformCalibration_ToMillimeterOkNg()
    {
        var template = EngineSyntheticFrames.Pattern(8, 8);
        var pixels = Enumerable.Repeat((byte)10, 128 * 96).ToArray();
        for (var y = 0; y < 8; y++)
        {
            template.Pixels.Slice(y * 8, 8).CopyTo(pixels.AsSpan((y + 10) * 128 + 8, 8));
        }
        for (var y = 30; y < 40; y++)
        {
            pixels.AsSpan(y * 128 + 18, 61).Fill(200);
        }
        // 独立参考条：端点相距100px，配方明确假设该合成参考长度为10mm，不是实际工件标定验收。
        pixels.AsSpan(8 * 128 + 8, 101).Fill(100);
        var frame = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 128, 96, PixelFormat.Mono8, pixels);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var locating = await engine.CreateContextAsync(default);
        var measuring = await engine.CreateContextAsync(default);
        var model = await engine.PrepareModelAsync(locating, new TemplateModelDefinition(template, new(2, 3), new()), 1, default);
        var located = await engine.ExecuteAsync(locating, new TemplateMatchOperation(frame, model, new(), 0.99), default);
        Assert.Equal(VisionExecutionStatus.Success, located.Status);
        var width = await engine.ExecuteAsync(measuring, new CaliperEdgePairOperation(frame,
            new(10, 25), new(70, 25), 10, 10, caliperCount: 7, normalHalfLength: 12,
            tangentHalfWidth: 0, minimumContrast: 50, gaussianSigmaPixels: 0,
            locationBinding: new(located.Value!, model, 0)), default);
        Assert.Equal(VisionExecutionStatus.Success, width.Status);
        Assert.Equal(10, width.Value!.Statistics!.Mean.Value);
        var calibration = await engine.ExecuteAsync(measuring, new PixelScaleCalibrationOperation(
            new(frame.FrameId, frame.CoordinateSpaceId, new(8, 8)),
            new(frame.FrameId, frame.CoordinateSpaceId, new(108, 8)), 10), default);
        Assert.Equal(VisionExecutionStatus.Success, calibration.Status);
        Assert.Equal(0.1, calibration.Value!.MillimetersPerPixel);
        await engine.ReleaseContextAsync(locating, default);
        await engine.ReleaseContextAsync(measuring, default);
        var evaluating = await engine.CreateContextAsync(default);
        var converted = await engine.ExecuteAsync(evaluating,
            new LengthToMillimetersOperation(width.Value.Statistics.Mean, calibration.Value), default);
        Assert.Equal(VisionExecutionStatus.Success, converted.Status);
        Assert.Equal(1, converted.Value!.ValueMillimeters);
        var ok = await engine.ExecuteAsync(evaluating, new MillimeterRangeOperation(converted.Value, 0.95, 1.05), default);
        var ng = await engine.ExecuteAsync(evaluating, new MillimeterRangeOperation(converted.Value, 1.1, 1.2), default);
        Assert.True(ok.Value!.Passed);
        Assert.False(ng.Value!.Passed);
    }

    private static IVisionEngine CommonEngine() => VisionEngineFactory.Create(new("test"), (_, _) => new NoCalibrationSdk());

    private sealed class NoCalibrationSdk : IVisionBackend
    {
        public string Id => "test";
        public VisionExecutionDomain Domain => VisionExecutionDomain.Vision;
        public IReadOnlyList<VisionOperationCapability> Capabilities => [];
        public IReadOnlyList<VisionIssue> Validate(VisionOperation operation, IPreparedVisionModel? model)
            => throw new InvalidOperationException("Calibration must not use SDK validation");
        public ValueTask<IPreparedVisionModel> PrepareAsync(VisionModelDefinition definition, CancellationToken token)
            => throw new NotSupportedException();
        public ValueTask<BackendOutput> ExecuteAsync(IPreparedVisionModel? model, VisionOperation operation, CancellationToken token)
            => throw new InvalidOperationException("Calibration must not use SDK execution");
        public void Dispose()
        {
        }
    }
}
