using Vision.Core;
using Vision.Core.Calibration;
using Vision.Core.Capabilities;
using Vision.Core.Coordinate;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Models;
using Vision.Core.Operations;
using Vision.Core.Results;
using Vision.Tools;
using Vision.Tools.Execution;
using Xunit;

namespace Vision.Tests.Tools.Engine;

[Trait("Category", "Contract")]
public sealed class AffineCalibrationEngineTests
{
    private static readonly Point2D[] Grid = [new(0, 0), new(100, 0), new(0, 100), new(100, 100), new(50, 50)];

    [Fact]
    public async Task AnisotropicShear_MapsBothDirections_AndMeasuresWorldDistanceWithoutSdk()
    {
        await using var engine = CommonEngine();
        var context = await engine.CreateContextAsync(default);
        var frame = Guid.NewGuid();
        var space = Guid.NewGuid();
        var world = Guid.NewGuid();
        var request = Request(frame, space, world, point => new(0.1 * point.X + 0.05 * point.Y + 10, -0.02 * point.X + 0.2 * point.Y - 5));
        Assert.Null(request.Image);
        var result = await engine.ExecuteAsync(context, request, default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        var calibration = Assert.IsType<AffinePixelCalibration>(result.Value!.Calibration);
        Assert.Equal(0.1, calibration.PixelToMillimeters.A, 10);
        Assert.Equal(0.05, calibration.PixelToMillimeters.B, 10);
        Assert.Equal(-0.02, calibration.PixelToMillimeters.C, 10);
        Assert.Equal(0.2, calibration.PixelToMillimeters.D, 10);
        Assert.Equal(10, calibration.PixelToMillimeters.Tx, 10);
        Assert.Equal(-5, calibration.PixelToMillimeters.Ty, 10);
        Assert.InRange(calibration.MaximumResidualMillimeters, 0, 1e-10);
        Assert.Equal(5, calibration.ResidualsMillimeters.Count);
        Assert.True(calibration.DesignConditionNumber >= 1);
        Assert.True(calibration.TransformConditionNumber > 1);
        Assert.NotEqual(Guid.Empty, calibration.CalibrationId);

        // 同一像素几何的后续帧允许复用；距离的两个观测本身仍须同帧。
        var later = Guid.NewGuid();
        var first = await Map(engine, context, calibration, new(later, space, new(0, 0)));
        var second = await Map(engine, context, calibration, new(later, space, new(3, 4)));
        Assert.Equal(10.5, second.Point.PositionMillimeters.X, 10);
        Assert.Equal(-4.26, second.Point.PositionMillimeters.Y, 10);
        var inverse = await engine.ExecuteAsync(context, new MillimeterPointToPixelOperation(second.Point, calibration, later), default);
        Assert.Equal(VisionExecutionStatus.Success, inverse.Status);
        Assert.Equal(later, inverse.Value!.Point.FrameId);
        Assert.Equal(3, inverse.Value.Point.Position.X, 9);
        Assert.Equal(4, inverse.Value.Point.Position.Y, 9);
        var distance = await engine.ExecuteAsync(context, new CalibratedPointDistanceOperation(first, second), default);
        Assert.Equal(VisionExecutionStatus.Success, distance.Status);
        Assert.Equal(double.Hypot(0.5, 0.74), distance.Value!.ValueMillimeters, 10);
        Assert.Same(first, distance.Value.First);
        Assert.Equal(calibration.CalibrationId, distance.Value.CalibrationId);
        var ok = await engine.ExecuteAsync(context, new MillimeterRangeOperation(distance.Value, 0.89, 0.90), default);
        var ng = await engine.ExecuteAsync(context, new MillimeterRangeOperation(distance.Value, 1, 2), default);
        Assert.Equal(VisionExecutionStatus.Success, ok.Status);
        Assert.True(ok.Value!.Passed);
        Assert.Equal(VisionExecutionStatus.Success, ng.Status);
        Assert.False(ng.Value!.Passed);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1e-9)]
    public async Task ReflectionAndSmallButWellConditionedUnits_AreNotMistakenForSingularity(double scale)
    {
        await using var engine = CommonEngine();
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, Request(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            point => new(scale * point.X, Math.Abs(scale) * point.Y)), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        var calibration = result.Value!.Calibration!;
        Assert.Equal(scale < 0, calibration.ReversesOrientation);
        var mapped = await Map(engine, context, calibration, new(Guid.NewGuid(), calibration.PixelSpaceId, new(30, 40)));
        var inverse = await engine.ExecuteAsync(context,
            new MillimeterPointToPixelOperation(mapped.Point, calibration, mapped.Source.FrameId), default);
        Assert.Equal(VisionExecutionStatus.Success, inverse.Status);
        Assert.Equal(30, inverse.Value!.Point.Position.X, 8);
        Assert.Equal(40, inverse.Value.Point.Position.Y, 8);
    }

    [Theory]
    [InlineData(0.5, true)]
    [InlineData(0.3, false)]
    public async Task OverdeterminedReferenceResiduals_AreMeasured_AndExcessiveErrorDoesNotPublishMap(double limit, bool accepted)
    {
        await using var engine = CommonEngine();
        var context = await engine.CreateContextAsync(default);
        var frame = Guid.NewGuid();
        var space = Guid.NewGuid();
        var world = Guid.NewGuid();
        var pairs = Grid.Select((p, index) => new CalibrationPointPair(new(frame, space, p),
            new(world, new(0.1 * p.X + (index == 4 ? 0.5 : 0), 0.2 * p.Y)))).ToArray();
        var result = await engine.ExecuteAsync(context, new AffineCalibrationOperation(pairs, limit), default);
        Assert.Equal(accepted ? VisionExecutionStatus.Success : VisionExecutionStatus.NoMatch, result.Status);
        Assert.Equal(5, result.Value!.ResidualsMillimeters.Count);
        Assert.Equal(0.4, result.Value.ResidualsMillimeters.Max(), 10);
        if (accepted)
        {
            Assert.Equal(0.2, result.Value.Calibration!.RmsResidualMillimeters, 10);
            Assert.Null(result.Value.Rejection);
        }
        else
        {
            Assert.Null(result.Value.Calibration);
            Assert.Equal("CalibrationResidualExceeded", result.Value.Rejection!.Code);
        }
    }

    [Theory]
    [InlineData(0, "DegeneratePixelGeometry")]
    [InlineData(1e-7, "IllConditionedPixelGeometry")]
    public async Task CollinearAndNearCollinearReferences_ReturnDiagnosticsWithoutOldMap(double height, string code)
    {
        await using var engine = CommonEngine();
        var context = await engine.CreateContextAsync(default);
        var frame = Guid.NewGuid();
        var space = Guid.NewGuid();
        var world = Guid.NewGuid();
        Point2D[] pixels = [new(0, 0), new(100, 0), new(50, height), new(100, height)];
        var pairs = pixels.Select(p => new CalibrationPointPair(new(frame, space, p), new(world, new(p.X * 0.1, p.Y * 0.1))));
        var result = await engine.ExecuteAsync(context, new AffineCalibrationOperation(pairs, 0.01), default);
        Assert.Equal(VisionExecutionStatus.NoMatch, result.Status);
        Assert.Null(result.Value!.Calibration);
        Assert.Equal(code, result.Value.Rejection!.Code);
        Assert.Empty(result.Value.ResidualsMillimeters);
    }

    [Theory]
    [InlineData(0, "DegenerateWorldGeometry")]
    [InlineData(1e-9, "IllConditionedCalibrationTransform")]
    public async Task CollapsedOrIllConditionedWorldMapping_IsRejected(double yScale, string code)
    {
        await using var engine = CommonEngine();
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, Request(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            point => yScale == 0 ? new(0, 0) : new(0.1 * point.X, yScale * point.Y)), default);
        Assert.Equal(VisionExecutionStatus.NoMatch, result.Status);
        Assert.Null(result.Value!.Calibration);
        Assert.Equal(code, result.Value.Rejection!.Code);
    }

    [Fact]
    public void InvalidReferences_AreRejectedBeforeExecution_AndInputCollectionIsFrozen()
    {
        var frame = Guid.NewGuid();
        var space = Guid.NewGuid();
        var world = Guid.NewGuid();
        var request = Request(frame, space, world, p => p);
        var pairs = request.Points.ToList();
        var frozen = new AffineCalibrationOperation(pairs, 0.1);
        pairs.Clear();
        Assert.Equal(5, frozen.Points.Count);
        Assert.Throws<ArgumentException>(() => new AffineCalibrationOperation(request.Points.Take(2), 0.1));
        Assert.Throws<ArgumentException>(() => new AffineCalibrationOperation(Enumerable.Repeat(request.Points[0], 4097), 0.1));
        Assert.Throws<ArgumentException>(() => new AffineCalibrationOperation(request.Points.Append(
            new(new(Guid.NewGuid(), space, new(1, 1)), new(world, new(1, 1)))), 0.1));
        Assert.Throws<ArgumentException>(() => new AffineCalibrationOperation(request.Points.Append(
            new(new(frame, Guid.NewGuid(), new(1, 1)), new(world, new(1, 1)))), 0.1));
        Assert.Throws<ArgumentException>(() => new AffineCalibrationOperation(request.Points.Append(
            new(new(frame, space, new(1, 1)), new(Guid.NewGuid(), new(1, 1)))), 0.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AffineCalibrationOperation(request.Points, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AffineCalibrationOperation(request.Points, 0.1, double.NaN));
        Assert.Throws<ArgumentException>(() => new MillimeterPoint(world, new(double.NaN, 0)));
        Assert.Throws<ArgumentException>(() => new MillimeterRangeOperation(new NakedMillimeters(), 0, 2));
    }

    [Fact]
    public async Task MappingAndDistance_RejectWrongSpacesMixedFramesAndDifferentCalibrations()
    {
        await using var engine = CommonEngine();
        var context = await engine.CreateContextAsync(default);
        var frame = Guid.NewGuid();
        var space = Guid.NewGuid();
        var world = Guid.NewGuid();
        var request = Request(frame, space, world, p => new(p.X * 0.1, p.Y * 0.2));
        var firstFit = await engine.ExecuteAsync(context, request, default);
        var secondFit = await engine.ExecuteAsync(context, request, default);
        Assert.Equal(VisionExecutionStatus.Success, firstFit.Status);
        Assert.Equal(VisionExecutionStatus.Success, secondFit.Status);
        var calibration = firstFit.Value!.Calibration!;
        Assert.Throws<ArgumentException>(() => new PixelToMillimeterPointOperation(
            new(frame, Guid.NewGuid(), new(0, 0)), calibration));
        Assert.Throws<ArgumentException>(() => new MillimeterPointToPixelOperation(
            new(Guid.NewGuid(), new(0, 0)), calibration, frame));
        Assert.Throws<ArgumentException>(() => new MillimeterPointToPixelOperation(
            new(world, new(0, 0)), calibration, Guid.Empty));
        var first = await Map(engine, context, calibration, new(frame, space, new(1, 2)));
        var otherFrame = await Map(engine, context, calibration, new(Guid.NewGuid(), space, new(3, 4)));
        var otherCalibration = await Map(engine, context, secondFit.Value!.Calibration!, new(frame, space, new(3, 4)));
        Assert.Throws<ArgumentException>(() => new CalibratedPointDistanceOperation(first, otherFrame));
        Assert.Throws<ArgumentException>(() => new CalibratedPointDistanceOperation(first, otherCalibration));
    }

    [Fact]
    public async Task NonRepresentablePointMapping_FailsWithoutNaNOrStalePoint()
    {
        await using var engine = CommonEngine();
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, Request(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            p => new(p.X * 10, p.Y * 20)), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        var calibration = result.Value!.Calibration!;
        var mapping = await engine.ExecuteAsync(context, new PixelToMillimeterPointOperation(
            new(Guid.NewGuid(), calibration.PixelSpaceId, new(1e308, 0)), calibration), default);
        Assert.Equal(VisionExecutionStatus.Failed, mapping.Status);
        Assert.Null(mapping.Value);
    }

    [Fact]
    public async Task ActualCaliperPair_MapsBothEdgesUnderShear_ThenEvaluatesMillimetersAfterSourceRelease()
    {
        var pixels = Enumerable.Repeat((byte)10, 128 * 128).ToArray();
        for (var y = 30; y < 40; y++)
        {
            pixels.AsSpan(y * 128 + 8, 49).Fill(200);
        }
        var frame = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 128, 128, PixelFormat.Mono8, pixels);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var width = await engine.ExecuteAsync(context, new CaliperEdgePairOperation(frame,
            new(12, 35), new(52, 35), 10, 10, caliperCount: 7, normalHalfLength: 12,
            tangentHalfWidth: 0, minimumContrast: 50, gaussianSigmaPixels: 0), default);
        Assert.Equal(VisionExecutionStatus.Success, width.Status);
        Assert.Equal(10, width.Value!.Statistics!.Mean.Value);
        // 配方显式假设同一平面为x_mm=0.1*x+0.05*y，y_mm=0.2*y，不是真实标定板验收。
        var calibrated = await engine.ExecuteAsync(context, Request(frame.FrameId, frame.CoordinateSpaceId,
            Guid.NewGuid(), p => new(0.1 * p.X + 0.05 * p.Y, 0.2 * p.Y)), default);
        Assert.Equal(VisionExecutionStatus.Success, calibrated.Status);
        var sample = width.Value.Samples[3];
        await engine.ReleaseContextAsync(context, default);
        var evaluating = await engine.CreateContextAsync(default);
        var calibration = calibrated.Value!.Calibration!;
        var first = await Map(engine, evaluating, calibration, new(frame.FrameId, frame.CoordinateSpaceId, sample.FirstEdge!.Value));
        var second = await Map(engine, evaluating, calibration, new(frame.FrameId, frame.CoordinateSpaceId, sample.SecondEdge!.Value));
        var distance = await engine.ExecuteAsync(evaluating, new CalibratedPointDistanceOperation(first, second), default);
        Assert.Equal(VisionExecutionStatus.Success, distance.Status);
        Assert.Equal(double.Hypot(0.5, 2), distance.Value!.ValueMillimeters, 9);
        var ok = await engine.ExecuteAsync(evaluating, new MillimeterRangeOperation(distance.Value, 2, 2.1), default);
        var ng = await engine.ExecuteAsync(evaluating, new MillimeterRangeOperation(distance.Value, 0.9, 1.1), default);
        Assert.True(ok.Value!.Passed);
        Assert.False(ng.Value!.Passed);
    }

    [Fact]
    public async Task ThreePointMinimum_AndLargeCoordinateOffset_KeepDirectionAndFiniteResiduals()
    {
        await using var engine = CommonEngine();
        var context = await engine.CreateContextAsync(default);
        var frame = Guid.NewGuid();
        var space = Guid.NewGuid();
        var world = Guid.NewGuid();
        Point2D[] points = [new(1e9, -1e9), new(1e9 + 100, -1e9), new(1e9, -1e9 + 100)];
        var pairs = points.Select(p => new CalibrationPointPair(new(frame, space, p),
            new(world, new((p.X - 1e9) * 0.1 + 5, (p.Y + 1e9) * 0.2 - 7))));
        var result = await engine.ExecuteAsync(context, new AffineCalibrationOperation(pairs, 1e-5), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(3, result.Value!.Calibration!.ResidualsMillimeters.Count);
        var mapped = await Map(engine, context, result.Value.Calibration, new(frame, space, new(1e9 + 30, -1e9 + 40)));
        Assert.InRange(mapped.Point.PositionMillimeters.X, 8 - 1e-5, 8 + 1e-5);
        Assert.InRange(mapped.Point.PositionMillimeters.Y, 1 - 1e-5, 1 + 1e-5);
        var calibration = result.Value.Calibration;
        Assert.Throws<ArgumentException>(() => new AffinePixelCalibration(Guid.NewGuid(),
            calibration.PixelToMillimeters, AffineTransform2D.Identity, calibration.DesignConditionNumber,
            calibration.TransformConditionNumber, calibration.RmsResidualMillimeters, calibration.MaximumResidualMillimeters,
            calibration.ReferencePairs, calibration.ResidualsMillimeters));
    }

    [Fact]
    public async Task CollinearWorldReferences_DoNotPublishUsableInverse()
    {
        await using var engine = CommonEngine();
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, Request(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            p => new(0.1 * p.X, 0.2 * p.X)), default);
        Assert.Equal(VisionExecutionStatus.NoMatch, result.Status);
        Assert.Null(result.Value!.Calibration);
        Assert.Contains(result.Value.Rejection!.Code, new[] { "SingularCalibrationTransform", "IllConditionedCalibrationTransform" });
    }

    [Theory]
    [InlineData(1e-9, 0, 1e-320)]
    [InlineData(10, -1e307, 1e307)]
    public async Task WorldDistance_DoesNotPublishFalseZeroOrNonFiniteValue(double scale, double firstX, double secondX)
    {
        await using var engine = CommonEngine();
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, Request(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            p => new(p.X * scale, p.Y * scale)), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        var calibration = result.Value!.Calibration!;
        var frame = Guid.NewGuid();
        var first = await Map(engine, context, calibration, new(frame, calibration.PixelSpaceId, new(firstX, 0)));
        var second = await Map(engine, context, calibration, new(frame, calibration.PixelSpaceId, new(secondX, 0)));
        var distance = await engine.ExecuteAsync(context, new CalibratedPointDistanceOperation(first, second), default);
        Assert.Equal(VisionExecutionStatus.Failed, distance.Status);
        Assert.Null(distance.Value);
    }

    private static AffineCalibrationOperation Request(Guid frame, Guid space, Guid world, Func<Point2D, Point2D> map)
        => new(Grid.Select(p => new CalibrationPointPair(new(frame, space, p), new(world, map(p)))), 0.01);

    private static async Task<CalibratedMillimeterPointOutput> Map(IVisionEngine engine, VisionContext context,
        AffinePixelCalibration calibration, PixelPoint source)
    {
        var result = await engine.ExecuteAsync(context, new PixelToMillimeterPointOperation(source, calibration), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        return result.Value!;
    }

    private static IVisionEngine CommonEngine() => VisionEngineFactory.Create(new("test"), (_, _) => new NoAffineSdk());

    private sealed class NakedMillimeters : IMillimeterLengthMeasurement
    {
        public Guid FrameId => Guid.NewGuid();
        public Guid CoordinateSpaceId => Guid.NewGuid();
        public Guid CalibrationId => Guid.NewGuid();
        public double ValueMillimeters => 1;
    }

    private sealed class NoAffineSdk : IVisionBackend
    {
        public string Id => "test";
        public VisionExecutionDomain Domain => VisionExecutionDomain.Vision;
        public IReadOnlyList<VisionOperationCapability> Capabilities => [];
        public IReadOnlyList<VisionIssue> Validate(VisionOperation operation, IPreparedVisionModel? model)
            => throw new InvalidOperationException("Affine calibration must not use SDK validation");
        public ValueTask<IPreparedVisionModel> PrepareAsync(VisionModelDefinition definition, CancellationToken token)
            => throw new NotSupportedException();
        public ValueTask<BackendOutput> ExecuteAsync(IPreparedVisionModel? model, VisionOperation operation, CancellationToken token)
            => throw new InvalidOperationException("Affine calibration must not use SDK execution");
        public void Dispose()
        {
        }
    }
}
