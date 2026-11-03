using Vision.Core.Geometry;
using Vision.Core.Operations;
using Vision.Core.Results;
using Vision.Tools;
using Xunit;

namespace Vision.Tests.Tools.OpenCv;

[Trait("Category", "Regression")]
public sealed class LineAngleTests
{
    private readonly Guid _frame = Guid.NewGuid();
    private readonly Guid _space = Guid.NewGuid();

    [Theory]
    [InlineData(1, 0, 0)]
    [InlineData(-1, 0, 0)]
    [InlineData(0, 1, 90)]
    [InlineData(1, 1, 45)]
    [InlineData(-1, 1, 45)]
    [InlineData(2, -1, 26.56505117707799)]
    public async Task UndirectedAngle_IsAcuteDegrees_AndInvariantToReversedEndpoints(double x, double y, double expected)
    {
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var first = Line(new(0, 0), new(1, 0));
        var second = Line(new(0, 0), new(x, y));
        var result = await engine.ExecuteAsync(context, new LineAngleOperation(first, second, LineAngleMode.UndirectedAcute), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(expected, result.Value!.Value, 10);
        Assert.Equal(GeometryMeasurementUnit.Degrees, result.Value.Unit);
        Assert.Equal(_frame, result.Value.FrameId);
        Assert.Equal(_space, result.Value.CoordinateSpaceId);
        var reverse = await engine.ExecuteAsync(context, new LineAngleOperation(
            Line(first.End, first.Start), Line(second.End, second.Start), LineAngleMode.UndirectedAcute), default);
        Assert.Equal(VisionExecutionStatus.Success, reverse.Status);
        Assert.Equal(expected, reverse.Value!.Value, 10);
    }

    [Theory]
    [InlineData(1e200)]
    [InlineData(1e-8)]
    public async Task DirectionNormalization_WorksForLargeAndSmallValidLines(double scale)
    {
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new LineAngleOperation(
            Line(new(0, 0), new(scale, 0)), Line(new(0, 0), new(scale, scale)), LineAngleMode.UndirectedAcute), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(45, result.Value!.Value, 10);
    }

    [Fact]
    public void InvalidOrDifferentSourceLines_DoNotFallback()
    {
        var first = Line(new(0, 0), new(1, 0));
        Assert.Throws<ArgumentNullException>(() => new LineAngleOperation(first, null!, LineAngleMode.UndirectedAcute));
        Assert.Throws<ArgumentException>(() => new LineAngleOperation(first, new PixelLine(Guid.NewGuid(), _space, new(0, 0), new(0, 1)), LineAngleMode.UndirectedAcute));
        Assert.Throws<ArgumentException>(() => new LineAngleOperation(first, new PixelLine(_frame, Guid.NewGuid(), new(0, 0), new(0, 1)), LineAngleMode.UndirectedAcute));
        Assert.Throws<ArgumentException>(() => Line(new(0, 0), new(0, 0)));
        Assert.Throws<ArgumentException>(() => Line(new(0, 0), new(1e-200, 0)));
        Assert.Throws<ArgumentException>(() => Line(new(0, 0), new(double.NaN, 1)));
        Assert.Throws<ArgumentException>(() => Line(new(-double.MaxValue, 0), new(double.MaxValue, 0)));
    }

    [Theory]
    [InlineData(1, 0, true)]
    [InlineData(1, 1, true)]
    [InlineData(2, 1, true)]
    [InlineData(0, 1, false)]
    public async Task DegreeTolerance_IncludesEndpoints_AndNgIsSuccessful(double x, double y, bool expected)
    {
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var angle = await engine.ExecuteAsync(context, new LineAngleOperation(
            Line(new(0, 0), new(1, 0)), Line(new(0, 0), new(x, y)), LineAngleMode.UndirectedAcute), default);
        Assert.Equal(VisionExecutionStatus.Success, angle.Status);
        Assert.Throws<ArgumentException>(() => new MeasurementRangeOperation(angle.Value!, 0, 45, GeometryMeasurementUnit.Pixels));
        var verdict = await engine.ExecuteAsync(context, new MeasurementRangeOperation(angle.Value!, 0, 45, GeometryMeasurementUnit.Degrees), default);
        Assert.Equal(VisionExecutionStatus.Success, verdict.Status);
        Assert.Equal(expected, verdict.Value!.Passed);
        await engine.ReleaseContextAsync(context, default);
        Assert.Equal(expected, verdict.Value.Passed);
    }

    private PixelLine Line(Point2D start, Point2D end) => new(_frame, _space, start, end);
}
