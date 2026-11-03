using Vision.Core;
using Vision.Core.Capabilities;
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
public sealed class GeometryMeasurementEngineTests
{
    private readonly Guid _frame = Guid.NewGuid();
    private readonly Guid _space = Guid.NewGuid();

    [Theory]
    [InlineData(3, 4, 5)]
    [InlineData(0, 0, 0)]
    [InlineData(1e200, 0, 1e200)]
    public async Task PointDistance_IsFiniteEuclideanValueWithoutSdkOrFakeImage(double x, double y, double expected)
    {
        await using var engine = CommonEngine();
        var context = await engine.CreateContextAsync(default);
        var request = new PointDistanceOperation(P(0, 0), P(x, y));
        Assert.Null(request.Image);
        var result = await engine.ExecuteAsync(context, request, default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(expected, result.Value!.Value);
        Assert.Equal(GeometryMeasurementKind.PointDistance, result.Value.Kind);
        Assert.Equal(GeometryMeasurementUnit.Pixels, result.Value.Unit);
        Assert.Equal(_frame, result.Value.FrameId);
    }

    [Fact]
    public async Task PointLineDistance_UsesInfiniteLine_NotNearestSegmentEndpoint()
    {
        await using var engine = CommonEngine();
        var context = await engine.CreateContextAsync(default);
        foreach (var line in new[] { L(new(0, 0), new(4, 0)), L(new(4, 0), new(0, 0)) })
        {
            var result = await engine.ExecuteAsync(context, new PointLineDistanceOperation(P(10, 3), line), default);
            Assert.Equal(VisionExecutionStatus.Success, result.Status);
            Assert.Equal(3, result.Value!.Value, 12);
            Assert.Equal(GeometryMeasurementKind.PointLineDistance, result.Value.Kind);
        }
    }

    [Theory]
    [InlineData(0, 1, LineAngleMode.Directed, 90)]
    [InlineData(0, -1, LineAngleMode.Directed, -90)]
    [InlineData(-1, 0, LineAngleMode.Directed, 180)]
    [InlineData(-1, 1, LineAngleMode.UndirectedAcute, 45)]
    public async Task LineAngle_UsesExplicitDirectionAndScreenClockwiseSign(double x, double y, LineAngleMode mode, double expected)
    {
        await using var engine = CommonEngine();
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context,
            new LineAngleOperation(L(new(0, 0), new(1, 0)), L(new(0, 0), new(x, y)), mode), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(expected, result.Value!.Value, 10);
        Assert.Equal(GeometryMeasurementUnit.Degrees, result.Value.Unit);
    }

    [Fact]
    public async Task Intersection_CanBeOutsideBothSegments_AndItsPointFeedsAnotherOperation()
    {
        await using var engine = CommonEngine();
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new LineIntersectionOperation(
            L(new(0, 0), new(1, 0)), L(new(2, 1), new(2, 2))), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(new Point2D(2, 0), result.Value!.Point!.Position);
        var distance = await engine.ExecuteAsync(context,
            new PointDistanceOperation(P(0, 0), result.Value.Point), default);
        Assert.Equal(2, distance.Value!.Value, 12);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1e-8)]
    public async Task ParallelOrIllConditionedIntersection_ReturnsNoMatchWithNoArbitraryPoint(double slope)
    {
        await using var engine = CommonEngine();
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context,
            new LineIntersectionOperation(L(new(0, 0), new(1, 0)), L(new(0, 1), new(1, 1 + slope))), default);
        Assert.Equal(VisionExecutionStatus.NoMatch, result.Status);
        Assert.Null(result.Value!.Point);
        Assert.InRange(result.Value.SinAngle, 0, 1e-6);
    }

    [Fact]
    public void MixedSourcesAndDegenerateLines_AreRejectedBeforeAnyExecution()
    {
        var otherFrame = new PixelPoint(Guid.NewGuid(), _space, new(3, 4));
        var otherSpace = new PixelPoint(_frame, Guid.NewGuid(), new(3, 4));
        Assert.Throws<ArgumentException>(() => new PointDistanceOperation(P(0, 0), otherFrame));
        Assert.Throws<ArgumentException>(() => new PointDistanceOperation(P(0, 0), otherSpace));
        var line = L(new(0, 0), new(1, 0));
        var foreignLine = new PixelLine(Guid.NewGuid(), _space, new(0, 0), new(1, 1));
        Assert.Throws<ArgumentException>(() => new PointLineDistanceOperation(otherSpace, line));
        Assert.Throws<ArgumentException>(() => new LineAngleOperation(line, foreignLine, LineAngleMode.Directed));
        Assert.Throws<ArgumentException>(() => new LineIntersectionOperation(line, foreignLine));
        Assert.Throws<ArgumentException>(() => L(new(1, 1), new(1, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LineIntersectionOperation(line, line, 0));
        var absent = new CaliperLineOutput(_frame, _space, CaliperLineFitState.InsufficientEdges, null,
            [new(0, new(0, 0), null, 0, false, null), new(1, new(1, 0), null, 0, false, null)]);
        Assert.Throws<ArgumentException>(() => PixelLine.FromCaliper(absent));
    }

    [Fact]
    public async Task MeasurementRange_UsesInclusiveBoundsAndExplicitUnits_NgIsStillSuccess()
    {
        await using var engine = CommonEngine();
        var context = await engine.CreateContextAsync(default);
        var distance = new ScalarMeasurementOutput(_frame, _space, GeometryMeasurementKind.PointDistance, 5);
        var ok = await engine.ExecuteAsync(context, new MeasurementRangeOperation(distance, 5, 5, GeometryMeasurementUnit.Pixels), default);
        var ng = await engine.ExecuteAsync(context, new MeasurementRangeOperation(distance, 5.1, 6, GeometryMeasurementUnit.Pixels), default);
        Assert.Equal(VisionExecutionStatus.Success, ok.Status);
        Assert.True(ok.Value!.Passed);
        Assert.Same(distance, ok.Value.Measurement);
        Assert.Equal(VisionExecutionStatus.Success, ng.Status);
        Assert.False(ng.Value!.Passed);
        Assert.Throws<ArgumentException>(() => new MeasurementRangeOperation(distance, 4, 6, GeometryMeasurementUnit.Degrees));
        var angle = new ScalarMeasurementOutput(_frame, _space, GeometryMeasurementKind.DirectedLineAngle, -90);
        var negative = await engine.ExecuteAsync(context, new MeasurementRangeOperation(angle, -90, -90, GeometryMeasurementUnit.Degrees), default);
        Assert.True(negative.Value!.Passed);
    }

    [Fact]
    public async Task UnrepresentableArithmetic_FailsWithoutNaNOrOldValue()
    {
        await using var engine = CommonEngine();
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new PointDistanceOperation(P(1e308, 0), P(-1e308, 0)), default);
        Assert.Equal(VisionExecutionStatus.Failed, result.Status);
        Assert.Null(result.Value);
        Assert.NotNull(result.Issue);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(5.25, true)]
    [InlineData(10, true)]
    [InlineData(10.01, false)]
    public async Task PixelDistanceTolerance_PreservesZeroClosedBoundsAndValidNg(double distance, bool expected)
    {
        await using var engine = CommonEngine();
        var context = await engine.CreateContextAsync(default);
        var measured = await engine.ExecuteAsync(context, new PointDistanceOperation(P(0, 0), P(distance, 0)), default);
        Assert.Equal(VisionExecutionStatus.Success, measured.Status);
        var request = new MeasurementRangeOperation(measured.Value!, 0, 10, GeometryMeasurementUnit.Pixels);
        var result = await engine.ExecuteAsync(context, request, default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(expected, result.Value!.Passed);
        Assert.Equal(distance, result.Value.Measurement.Value);
        Assert.Equal(0, result.Value.Minimum);
        Assert.Equal(10, result.Value.Maximum);
        await engine.ReleaseContextAsync(context, default);
        Assert.Equal(expected, result.Value.Passed);
    }

    [Fact]
    public void MissingOrInvalidToleranceInput_DoesNotBecomeFalseVerdict()
    {
        var measurement = new ScalarMeasurementOutput(_frame, _space, GeometryMeasurementKind.PointDistance, 0);
        Assert.Throws<ArgumentNullException>(() => new MeasurementRangeOperation(null!, 0, 10, GeometryMeasurementUnit.Pixels));
        Assert.Throws<ArgumentException>(() => new MeasurementRangeOperation(measurement, 0, 10, GeometryMeasurementUnit.Degrees));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MeasurementRangeOperation(measurement, 10, 0, GeometryMeasurementUnit.Pixels));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MeasurementRangeOperation(measurement, double.NaN, 10, GeometryMeasurementUnit.Pixels));
        Assert.Throws<ArgumentException>(() => new ScalarMeasurementOutput(_frame, _space, GeometryMeasurementKind.PointDistance, double.PositiveInfinity));
    }

    [Fact]
    public async Task RealCaliperAndBlobCenter_FormPixelDistanceAndToleranceChain_AfterNativeContextCloses()
    {
        var pixels = new byte[64 * 48];
        pixels.AsSpan(24 * 64).Fill(200);
        for (var y = 28; y <= 32; y++)
        {
            pixels.AsSpan(y * 64 + 30, 5).Fill(255);
        }
        var frame = new VisionFrame(_frame, _space, 64, 48, PixelFormat.Mono8, pixels);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var caliper = await engine.ExecuteAsync(context, new CaliperLineOperation(frame,
            new(8, 24), new(56, 24), 7, 12, 1, minimumContrast: 100, gaussianSigmaPixels: 0), default);
        var blobs = await engine.ExecuteAsync(context, new BlobOperation(frame, 255, 255), default);
        Assert.Equal(VisionExecutionStatus.Success, caliper.Status);
        Assert.Equal(VisionExecutionStatus.Success, blobs.Status);
        var point = PixelPoint.FromBlobCenter(blobs.Value!, 1);
        var line = PixelLine.FromCaliper(caliper.Value!);
        Assert.Throws<ArgumentOutOfRangeException>(() => PixelPoint.FromBlobCenter(blobs.Value!, 0));
        await engine.ReleaseContextAsync(context, default);
        var evaluation = await engine.CreateContextAsync(default);
        var measured = await engine.ExecuteAsync(evaluation, new PointLineDistanceOperation(point, line), default);
        Assert.Equal(VisionExecutionStatus.Success, measured.Status);
        Assert.Equal(6.5, measured.Value!.Value, 5);
        var ok = await engine.ExecuteAsync(evaluation,
            new MeasurementRangeOperation(measured.Value, 6.4, 6.6, GeometryMeasurementUnit.Pixels), default);
        var ng = await engine.ExecuteAsync(evaluation,
            new MeasurementRangeOperation(measured.Value, 6.6, 7, GeometryMeasurementUnit.Pixels), default);
        Assert.True(ok.Value!.Passed);
        Assert.False(ng.Value!.Passed);
    }

    private PixelPoint P(double x, double y) => new(_frame, _space, new(x, y));
    private PixelLine L(Point2D start, Point2D end) => new(_frame, _space, start, end);
    private static IVisionEngine CommonEngine() => VisionEngineFactory.Create(new("test"), (_, _) => new NoGeometrySdk());

    private sealed class NoGeometrySdk : IVisionBackend
    {
        public string Id => "test";
        public VisionExecutionDomain Domain => VisionExecutionDomain.Vision;
        public IReadOnlyList<VisionOperationCapability> Capabilities => [];
        public IReadOnlyList<VisionIssue> Validate(VisionOperation operation, IPreparedVisionModel? model)
            => throw new InvalidOperationException("Common geometry must not use SDK validation");
        public ValueTask<IPreparedVisionModel> PrepareAsync(VisionModelDefinition definition, CancellationToken token)
            => throw new NotSupportedException();
        public ValueTask<BackendOutput> ExecuteAsync(IPreparedVisionModel? model, VisionOperation operation, CancellationToken token)
            => throw new InvalidOperationException("Common geometry must not execute through SDK");
        public void Dispose()
        {
        }
    }
}
