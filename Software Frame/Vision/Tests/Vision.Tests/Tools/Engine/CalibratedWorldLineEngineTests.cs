using Vision.Core;
using Vision.Core.Calibration;
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
public sealed class CalibratedWorldLineEngineTests
{
    [Fact]
    public async Task Shear_UsesWorldNormalDistance_NotPixelScaleOrAlongScanEndpointDistance()
    {
        await using var engine = CommonEngine();
        var context = await engine.CreateContextAsync(default);
        var calibration = await Fit(engine, context, p => new(0.1 * p.X + 0.05 * p.Y + 10, 0.2 * p.Y - 5));
        var frame = Guid.NewGuid();
        var line = await MapLine(engine, context, calibration, new(frame, calibration.PixelSpaceId, new(0, 0), new(100, 0)));
        var point = await MapPoint(engine, context, calibration, new(frame, calibration.PixelSpaceId, new(0, 10)));
        var start = await MapPoint(engine, context, calibration, new(frame, calibration.PixelSpaceId, new(0, 0)));
        var request = new CalibratedPointLineDistanceOperation(point, line);
        Assert.Null(request.Image);
        var distance = await engine.ExecuteAsync(context, request, default);
        var scanDistance = await engine.ExecuteAsync(context, new CalibratedPointDistanceOperation(start, point), default);
        Assert.Equal(VisionExecutionStatus.Success, distance.Status);
        Assert.Equal(2, distance.Value!.ValueMillimeters, 10);
        Assert.Equal(double.Hypot(0.5, 2), scanDistance.Value!.ValueMillimeters, 10);
        Assert.Same(point, distance.Value.Point);
        Assert.Same(line, distance.Value.Line);
        Assert.Equal(calibration.CalibrationId, distance.Value.CalibrationId);
        var ok = await engine.ExecuteAsync(context, new MillimeterRangeOperation(distance.Value, 1.99, 2.01), default);
        var ng = await engine.ExecuteAsync(context, new MillimeterRangeOperation(distance.Value, 2.05, 2.1), default);
        Assert.Equal(VisionExecutionStatus.Success, ok.Status);
        Assert.True(ok.Value!.Passed);
        Assert.Equal(VisionExecutionStatus.Success, ng.Status);
        Assert.False(ng.Value!.Passed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InfiniteLine_DoesNotClampToSegment_AndReversalDoesNotChangeUnsignedDistance(bool reverse)
    {
        await using var engine = CommonEngine();
        var context = await engine.CreateContextAsync(default);
        var calibration = await Fit(engine, context, p => new(-0.1 * p.X + 0.05 * p.Y, 0.2 * p.Y));
        var frame = Guid.NewGuid();
        var first = new Point2D(0, 0);
        var second = new Point2D(10, 0);
        var line = await MapLine(engine, context, calibration,
            new(frame, calibration.PixelSpaceId, reverse ? second : first, reverse ? first : second));
        var point = await MapPoint(engine, context, calibration, new(frame, calibration.PixelSpaceId, new(1000, 10)));
        var result = await engine.ExecuteAsync(context, new CalibratedPointLineDistanceOperation(point, line), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(2, result.Value!.ValueMillimeters, 9);
    }

    [Fact]
    public async Task TiltedWorldLine_RecomputesNormalFromBothMappedComponents()
    {
        await using var engine = CommonEngine();
        var context = await engine.CreateContextAsync(default);
        var calibration = await Fit(engine, context, p => new(0.1 * p.X + 0.05 * p.Y, -0.02 * p.X + 0.2 * p.Y));
        var frame = Guid.NewGuid();
        var line = await MapLine(engine, context, calibration,
            new(frame, calibration.PixelSpaceId, new(0, 0), new(100, 100)));
        var point = await MapPoint(engine, context, calibration, new(frame, calibration.PixelSpaceId, new(0, 10)));
        var result = await engine.ExecuteAsync(context, new CalibratedPointLineDistanceOperation(point, line), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        // 世界直线方向(15,18)，点相对起点(0.5,2)，垂距=|0.5*18-2*15|/hypot(15,18)。
        Assert.Equal(21 / double.Hypot(15, 18), result.Value!.ValueMillimeters, 10);
    }

    [Fact]
    public async Task PointAtLineStart_HasValidZeroDistance_WithoutInventingAnEdgePair()
    {
        await using var engine = CommonEngine();
        var context = await engine.CreateContextAsync(default);
        var calibration = await Fit(engine, context, p => new(1e-12 * p.X, 1e-12 * p.Y));
        var frame = Guid.NewGuid();
        var source = new PixelLine(frame, calibration.PixelSpaceId, new(0, 0), new(100, 0));
        var line = await MapLine(engine, context, calibration, source);
        var point = await MapPoint(engine, context, calibration, new(frame, calibration.PixelSpaceId, source.Start));
        var result = await engine.ExecuteAsync(context, new CalibratedPointLineDistanceOperation(point, line), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(0, result.Value!.ValueMillimeters);
        var decision = await engine.ExecuteAsync(context, new MillimeterRangeOperation(result.Value, 0, 0), default);
        Assert.True(decision.Value!.Passed);
    }

    [Fact]
    public async Task WrongSpaceFrameOrCalibration_IsRejectedBeforeDistanceExecution()
    {
        await using var engine = CommonEngine();
        var context = await engine.CreateContextAsync(default);
        var calibration = await Fit(engine, context, p => new(p.X * 0.1, p.Y * 0.2));
        var frame = Guid.NewGuid();
        var source = new PixelLine(frame, calibration.PixelSpaceId, new(0, 0), new(100, 0));
        Assert.Throws<ArgumentException>(() => new PixelLineToMillimetersOperation(
            new(frame, Guid.NewGuid(), source.Start, source.End), calibration));
        var line = await MapLine(engine, context, calibration, source);
        var wrongFrame = await MapPoint(engine, context, calibration, new(Guid.NewGuid(), calibration.PixelSpaceId, new(0, 10)));
        Assert.Throws<ArgumentException>(() => new CalibratedPointLineDistanceOperation(wrongFrame, line));
        var other = await Fit(engine, context, p => new(p.X * 0.1, p.Y * 0.2), calibration.PixelSpaceId);
        var wrongCalibration = await MapPoint(engine, context, other, new(frame, calibration.PixelSpaceId, new(0, 10)));
        Assert.Throws<ArgumentException>(() => new CalibratedPointLineDistanceOperation(wrongCalibration, line));
        Assert.Throws<ArgumentException>(() => new CalibratedMillimeterLineOutput(source, calibration,
            new(Guid.NewGuid(), line.Start.PositionMillimeters), line.End));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CollapsedOrOverflowingWorldLine_FailsWithoutStaleLine(bool overflow)
    {
        await using var engine = CommonEngine();
        var context = await engine.CreateContextAsync(default);
        var calibration = await Fit(engine, context, p => overflow ? new(p.X * 10, p.Y * 10)
            : new(p.X * 1e-9 + 1e6, p.Y * 1e-9 - 1e6));
        var source = new PixelLine(Guid.NewGuid(), calibration.PixelSpaceId,
            overflow ? new(1e307, 0) : new(0, 0), overflow ? new(2e307, 0) : new(1e-8, 0));
        var result = await engine.ExecuteAsync(context, new PixelLineToMillimetersOperation(source, calibration), default);
        Assert.Equal(VisionExecutionStatus.Failed, result.Status);
        Assert.Null(result.Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NonCollinearFalseZeroOrOverflowingNormalDistance_FailsWithoutValue(bool overflow)
    {
        await using var engine = CommonEngine();
        var context = await engine.CreateContextAsync(default);
        var calibration = await Fit(engine, context, p => overflow ? new(p.X * 10, p.Y * 10)
            : new(p.X * 1e-9 + 1e6, p.Y * 1e-9 - 1e6));
        var frame = Guid.NewGuid();
        var y = overflow ? -1e307 : 0;
        // 极端远坐标下用可表示的长定义方向，确保本例检验的是垂距溢出，而非直线先被舍入压塌。
        var line = await MapLine(engine, context, calibration,
            new(frame, calibration.PixelSpaceId, new(0, y), new(overflow ? 1e306 : 100, y)));
        var point = await MapPoint(engine, context, calibration,
            new(frame, calibration.PixelSpaceId, overflow ? new(0, 1e307) : new(10, 1e-8)));
        var result = await engine.ExecuteAsync(context, new CalibratedPointLineDistanceOperation(point, line), default);
        Assert.Equal(VisionExecutionStatus.Failed, result.Status);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task ActualCaliperLineAndPair_InShearedWorld_HaveDistinctNormalAndScanDistances_AfterSourceRelease()
    {
        var pixels = Enumerable.Repeat((byte)10, 128 * 128).ToArray();
        for (var y = 30; y < 40; y++)
        {
            pixels.AsSpan(y * 128 + 8, 49).Fill(200);
        }
        var frame = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 128, 128, PixelFormat.Mono8, pixels);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var fitted = await engine.ExecuteAsync(context, new CaliperLineOperation(frame, new(12, 35), new(52, 35),
            caliperCount: 7, normalHalfLength: 12, tangentHalfWidth: 0, polarity: CaliperEdgePolarity.LightToDark,
            minimumContrast: 50, gaussianSigmaPixels: 0, maximumResidualPixels: 0.01), default);
        var pair = await engine.ExecuteAsync(context, new CaliperEdgePairOperation(frame, new(12, 35), new(52, 35),
            10, 10, caliperCount: 7, normalHalfLength: 12, tangentHalfWidth: 0, minimumContrast: 50, gaussianSigmaPixels: 0), default);
        Assert.Equal(VisionExecutionStatus.Success, fitted.Status);
        Assert.Equal(VisionExecutionStatus.Success, pair.Status);
        Assert.Equal(29.5, fitted.Value!.Line!.Start.Y, 5);
        var pixelLine = PixelLine.FromCaliper(fitted.Value);
        var sample = pair.Value!.Samples[3];
        Assert.Equal(39.5, sample.FirstEdge!.Value.Y, 5);
        // 已知平面配方：x_mm=0.1*x+0.05*y、y_mm=0.2*y；不是实际标定板或工件验收。
        var calibration = await Fit(engine, context, p => new(0.1 * p.X + 0.05 * p.Y, 0.2 * p.Y),
            frame.CoordinateSpaceId, frame.FrameId);
        await engine.ReleaseContextAsync(context, default);
        var evaluating = await engine.CreateContextAsync(default);
        var line = await MapLine(engine, evaluating, calibration, pixelLine);
        var point = await MapPoint(engine, evaluating, calibration, new(frame.FrameId, frame.CoordinateSpaceId, sample.FirstEdge.Value));
        var second = await MapPoint(engine, evaluating, calibration, new(frame.FrameId, frame.CoordinateSpaceId, sample.SecondEdge!.Value));
        var normal = await engine.ExecuteAsync(evaluating, new CalibratedPointLineDistanceOperation(point, line), default);
        var scan = await engine.ExecuteAsync(evaluating, new CalibratedPointDistanceOperation(point, second), default);
        Assert.Equal(VisionExecutionStatus.Success, normal.Status);
        Assert.Equal(VisionExecutionStatus.Success, scan.Status);
        Assert.Equal(2, normal.Value!.ValueMillimeters, 8);
        Assert.Equal(double.Hypot(0.5, 2), scan.Value!.ValueMillimeters, 8);
        var ok = await engine.ExecuteAsync(evaluating, new MillimeterRangeOperation(normal.Value, 1.99, 2.01), default);
        var ng = await engine.ExecuteAsync(evaluating, new MillimeterRangeOperation(normal.Value, 2.05, 2.1), default);
        Assert.True(ok.Value!.Passed);
        Assert.False(ng.Value!.Passed);
    }

    private static async Task<AffinePixelCalibration> Fit(IVisionEngine engine, VisionContext context, Func<Point2D, Point2D> map,
        Guid? spaceId = null, Guid? frameId = null)
    {
        var space = spaceId ?? Guid.NewGuid();
        var frame = frameId ?? Guid.NewGuid();
        var world = Guid.NewGuid();
        Point2D[] pixels = [new(0, 0), new(100, 0), new(0, 100), new(100, 100), new(50, 50)];
        var request = new AffineCalibrationOperation(pixels.Select(p => new CalibrationPointPair(new(frame, space, p), new(world, map(p)))), 0.01);
        var result = await engine.ExecuteAsync(context, request, default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        return result.Value!.Calibration!;
    }

    private static async Task<CalibratedMillimeterLineOutput> MapLine(IVisionEngine engine, VisionContext context, AffinePixelCalibration calibration, PixelLine source)
    {
        var result = await engine.ExecuteAsync(context, new PixelLineToMillimetersOperation(source, calibration), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        return result.Value!;
    }

    private static async Task<CalibratedMillimeterPointOutput> MapPoint(IVisionEngine engine, VisionContext context, AffinePixelCalibration calibration, PixelPoint source)
    {
        var result = await engine.ExecuteAsync(context, new PixelToMillimeterPointOperation(source, calibration), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        return result.Value!;
    }

    private static IVisionEngine CommonEngine() => VisionEngineFactory.Create(new("test"), (_, _) => new NoWorldLineSdk());

    private sealed class NoWorldLineSdk : IVisionBackend
    {
        public string Id => "test";
        public VisionExecutionDomain Domain => VisionExecutionDomain.Vision;
        public IReadOnlyList<VisionOperationCapability> Capabilities => [];
        public IReadOnlyList<VisionIssue> Validate(VisionOperation operation, IPreparedVisionModel? model)
            => throw new InvalidOperationException("World geometry must not use SDK validation");
        public ValueTask<IPreparedVisionModel> PrepareAsync(VisionModelDefinition definition, CancellationToken token)
            => throw new NotSupportedException();
        public ValueTask<BackendOutput> ExecuteAsync(IPreparedVisionModel? model, VisionOperation operation, CancellationToken token)
            => throw new InvalidOperationException("World geometry must not use SDK execution");
        public void Dispose()
        {
        }
    }
}
