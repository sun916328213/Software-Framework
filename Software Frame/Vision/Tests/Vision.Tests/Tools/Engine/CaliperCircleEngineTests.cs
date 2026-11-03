using System.Buffers.Binary;
using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Operations;
using Vision.Core.Results;
using Vision.Tools;
using Xunit;

namespace Vision.Tests.Tools.Engine;

[Trait("Category", "Contract")]
public sealed class CaliperCircleEngineTests
{
    [Theory]
    [InlineData(CircleScanDirection.Outward, CaliperEdgePolarity.LightToDark)]
    [InlineData(CircleScanDirection.Inward, CaliperEdgePolarity.DarkToLight)]
    public async Task BrightDisk_RadialSamplingFitsCircleAndDiameter_WithDirectionRelativePolarity(
        CircleScanDirection direction, CaliperEdgePolarity polarity)
    {
        var frame = Scene((radius, _) => radius <= 20 ? (byte)200 : (byte)0);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var request = new CaliperCircleOperation(frame, new(48, 48), 20, normalHalfLength: 6,
            scanDirection: direction, polarity: polarity, minimumContrast: 50, gaussianSigmaPixels: 0,
            minimumPoints: 24, maximumRadialResidualPixels: 0.9, minimumCoverageDegrees: 270);
        var result = await engine.ExecuteAsync(context, request, default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        var fit = result.Value!.Circle!;
        Assert.InRange(fit.Center.DistanceTo(new(48, 48)), 0, 0.4);
        Assert.InRange(fit.RadiusPixels, 19.3, 20.7);
        Assert.InRange(fit.RmsRadialResidualPixels, 0, 0.7);
        Assert.InRange(fit.MaximumRadialResidualPixels, 0, 0.9);
        Assert.True(fit.CoverageDegrees >= 270);
        Assert.Equal(32, result.Value.Samples.Count);
        Assert.NotEqual(result.Value.Samples[0].Center, result.Value.Samples[^1].Center);
        Assert.All(result.Value.Samples.Where(sample => sample.Edge != null), sample =>
            Assert.True(direction == CircleScanDirection.Outward ? sample.SignedContrast < 0 : sample.SignedContrast > 0));
        Assert.Equal(GeometryMeasurementKind.CaliperCircleDiameter, fit.Diameter.Kind);
        Assert.Equal(GeometryMeasurementUnit.Pixels, fit.Diameter.Unit);
        await engine.ReleaseContextAsync(context, default);
        var evaluating = await engine.CreateContextAsync(default);
        var decision = await engine.ExecuteAsync(evaluating,
            new MeasurementRangeOperation(fit.Diameter, 38, 42, GeometryMeasurementUnit.Pixels), default);
        Assert.True(decision.Value!.Passed);
        Assert.Equal(frame.FrameId, result.Value.FrameId);
        Assert.Equal(frame.CoordinateSpaceId, result.Value.CoordinateSpaceId);
    }

    [Theory]
    [InlineData(CircleScanDirection.Outward, CaliperEdgeSelection.First, 15)]
    [InlineData(CircleScanDirection.Outward, CaliperEdgeSelection.Last, 25)]
    [InlineData(CircleScanDirection.Inward, CaliperEdgeSelection.First, 25)]
    [InlineData(CircleScanDirection.Outward, CaliperEdgeSelection.Strongest, 15)]
    public async Task ConcentricEdges_HonorScanOrderAndSelection(CircleScanDirection direction, CaliperEdgeSelection selection, double radius)
    {
        var frame = Scene((r, _) => r < 15 ? (byte)20 : r <= 25 ? (byte)200 : (byte)40);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new CaliperCircleOperation(frame, new(48, 48), 20,
            normalHalfLength: 12, tangentHalfWidth: 0, scanDirection: direction, selection: selection,
            minimumContrast: 50, gaussianSigmaPixels: 0, minimumPoints: 20, minimumCoverageDegrees: 270), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.InRange(result.Value!.Circle!.RadiusPixels, radius - 0.8, radius + 0.8);
    }

    [Fact]
    public async Task FittedCenterAndRadius_AreMeasured_NotCopiedFromNominalSearchCircle()
    {
        var frame = Scene((radius, _) => radius <= 20 ? (byte)200 : (byte)0);
        var nominal = new Point2D(45, 50);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new CaliperCircleOperation(frame, nominal, 18,
            normalHalfLength: 8, tangentHalfWidth: 0, minimumContrast: 50, gaussianSigmaPixels: 0,
            minimumPoints: 24, minimumCoverageDegrees: 270), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.InRange(result.Value!.Circle!.Center.DistanceTo(new(48, 48)), 0, 0.6);
        Assert.InRange(result.Value.Circle.RadiusPixels, 19.2, 20.8);
        Assert.True(result.Value.Circle.Center.DistanceTo(nominal) > 2.5);
    }

    [Fact]
    public async Task LocalRadialOutlier_IsRejectedAndDiagnosed_AndMinimumPointsStillApplies()
    {
        var frame = Scene((radius, angle) => radius <= (Math.Abs(angle) < 0.03 ? 26 : 20) ? (byte)200 : (byte)0);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        CaliperCircleOperation Request(int minimum) => new(frame, new(48, 48), 20, normalHalfLength: 10,
            tangentHalfWidth: 0, minimumContrast: 50, gaussianSigmaPixels: 0, minimumPoints: minimum,
            maximumRadialResidualPixels: 1, minimumCoverageDegrees: 270);
        var result = await engine.ExecuteAsync(context, Request(24), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.InRange(result.Value!.Circle!.RadiusPixels, 19.3, 20.7);
        var rejected = Assert.Single(result.Value.Samples.Where(sample => sample.Edge != null && !sample.UsedInFit));
        Assert.Equal(0, rejected.Index);
        Assert.True(rejected.ResidualPixels > 5);
        var strict = await engine.ExecuteAsync(context, Request(32), default);
        Assert.Equal(VisionExecutionStatus.NoMatch, strict.Status);
        Assert.Null(strict.Value!.Circle);
        Assert.Equal(CaliperCircleFitState.InsufficientInliers, strict.Value.State);
    }

    [Fact]
    public async Task PartialArc_IncludesBothEnds_AndReportsMeasuredCoverage()
    {
        var frame = Scene((radius, _) => radius <= 20 ? (byte)200 : (byte)0);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var request = new CaliperCircleOperation(frame, new(48, 48), 20, 12, 6, 0,
            minimumContrast: 50, gaussianSigmaPixels: 0, minimumPoints: 8,
            angleStartDegrees: 0, angleExtentDegrees: 90, minimumCoverageDegrees: 75);
        var result = await engine.ExecuteAsync(context, request, default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(new Point2D(68, 48), result.Value!.Samples[0].Center);
        Assert.Equal(new Point2D(48, 68), result.Value.Samples[^1].Center);
        Assert.InRange(result.Value.Circle!.RadiusPixels, 18.5, 21.5);
        Assert.InRange(result.Value.Circle.CoverageDegrees, 75, 110);
    }

    [Fact]
    public async Task AlmostStraightArcSamples_DoNotPublishAnUnstableHugeCircle()
    {
        var pixels = new byte[96 * 96];
        pixels.AsSpan(56 * 96).Fill(200);
        var frame = Gray(pixels);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new CaliperCircleOperation(frame, new(48, 40), 20,
            12, 10, 0, minimumContrast: 50, gaussianSigmaPixels: 0, minimumPoints: 8,
            angleStartDegrees: 60, angleExtentDegrees: 60, minimumCoverageDegrees: 15), default);
        Assert.Equal(VisionExecutionStatus.NoMatch, result.Status);
        Assert.Null(result.Value!.Circle);
    }

    [Fact]
    public async Task Mono16Circle_UsesOriginalContrastAndIgnoresPadding()
    {
        var bytes = new byte[96 * 196];
        for (var y = 0; y < 96; y++)
        {
            for (var x = 0; x < 96; x++)
            {
                var value = (x - 48) * (x - 48) + (y - 48) * (y - 48) <= 400 ? (ushort)12000 : (ushort)0;
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(y * 196 + 2 * x, 2), value);
            }
            bytes.AsSpan(y * 196 + 192, 4).Fill(255);
        }
        var frame = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 96, 96, PixelFormat.Mono16, bytes, 196);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new CaliperCircleOperation(frame, new(48, 48), 20,
            24, 6, 0, polarity: CaliperEdgePolarity.LightToDark, minimumContrast: 5000,
            gaussianSigmaPixels: 0, minimumPoints: 16, minimumCoverageDegrees: 250), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.InRange(result.Value!.Circle!.RadiusPixels, 19.3, 20.7);
        Assert.All(result.Value.Samples.Where(sample => sample.Edge != null), sample => Assert.True(sample.SignedContrast <= -5000));
        Assert.Equal(bytes, frame.Pixels.ToArray());
    }

    [Fact]
    public async Task ThresholdAndSmoothing_CanLeaveNoEdges_WithoutOldCircle()
    {
        var frame = Scene((radius, _) => radius <= 20 ? (byte)200 : (byte)0);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var requests = new[]
        {
            new CaliperCircleOperation(frame, new(48, 48), 20, normalHalfLength: 10, minimumContrast: 201, gaussianSigmaPixels: 0),
            new CaliperCircleOperation(frame, new(48, 48), 20, normalHalfLength: 10, minimumContrast: 100, gaussianSigmaPixels: 3)
        };
        foreach (var request in requests)
        {
            var result = await engine.ExecuteAsync(context, request, default);
            Assert.Equal(VisionExecutionStatus.NoMatch, result.Status);
            Assert.Equal(CaliperCircleFitState.InsufficientEdges, result.Value!.State);
            Assert.Null(result.Value.Circle);
        }
    }

    [Fact]
    public void DegenerateSearchGeometryAndQualityBounds_AreRejectedBeforeSdk()
    {
        var frame = Gray(new byte[96 * 96]);
        Assert.Throws<ArgumentException>(() => new CaliperCircleOperation(frame, new(48, 48), 6));
        Assert.Throws<ArgumentException>(() => new CaliperCircleOperation(frame, new(8, 8), 20));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CaliperCircleOperation(frame, new(48, 48), 20, angleExtentDegrees: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CaliperCircleOperation(frame, new(48, 48), 20, minimumCoverageDegrees: 360));
    }

    private static VisionFrame Scene(Func<double, double, byte> intensity)
    {
        var pixels = new byte[96 * 96];
        for (var y = 0; y < 96; y++)
        {
            for (var x = 0; x < 96; x++)
            {
                var dx = x - 48;
                var dy = y - 48;
                pixels[y * 96 + x] = intensity(Math.Sqrt(dx * dx + dy * dy), Math.Atan2(dy, dx));
            }
        }
        return Gray(pixels);
    }

    private static VisionFrame Gray(byte[] pixels)
    {
        return new(Guid.NewGuid(), Guid.NewGuid(), 96, 96, PixelFormat.Mono8, pixels);
    }
}
