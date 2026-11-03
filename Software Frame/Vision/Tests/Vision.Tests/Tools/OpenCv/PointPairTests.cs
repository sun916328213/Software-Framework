using Vision.Core.Geometry;
using Vision.Core.Operations;
using Vision.Core.Results;
using Vision.Tools;
using Xunit;

namespace Vision.Tests.Tools.OpenCv;

[Trait("Category", "Regression")]
public sealed class PointPairTests
{
    private readonly Guid _frame = Guid.NewGuid();
    private readonly Guid _space = Guid.NewGuid();

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(3, 4, 5)]
    [InlineData(30, 40, 50)]
    public async Task ExplicitPoints_ProducePixelDistanceIncludingValidZero(double x, double y, double expected)
    {
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var first = Point(0, 0);
        var second = Point(x, y);
        var request = new PointDistanceOperation(first, second);
        Assert.Null(request.Image);
        var result = await engine.ExecuteAsync(context, request, default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(expected, result.Value!.Value);
        Assert.Equal(GeometryMeasurementUnit.Pixels, result.Value.Unit);
        Assert.Equal(GeometryMeasurementKind.PointDistance, result.Value.Kind);
        Assert.Equal(_frame, result.Value.FrameId);
        Assert.Equal(_space, result.Value.CoordinateSpaceId);
        Assert.Equal(Point2D.Origin, first.Position);
        Assert.Equal(new Point2D(x, y), second.Position);
    }

    [Fact]
    public void InvalidOrDifferentSourcesAndMissingPoints_AreRejectedWithoutFallback()
    {
        var point = Point(0, 0);
        Assert.Throws<ArgumentNullException>(() => new PointDistanceOperation(point, null!));
        Assert.Throws<ArgumentException>(() => new PointDistanceOperation(point, new PixelPoint(Guid.NewGuid(), _space, new(3, 4))));
        Assert.Throws<ArgumentException>(() => new PointDistanceOperation(point, new PixelPoint(_frame, Guid.NewGuid(), new(3, 4))));
        Assert.Throws<ArgumentException>(() => new PixelPoint(Guid.Empty, _space, new(0, 0)));
        Assert.Throws<ArgumentException>(() => new PixelPoint(_frame, Guid.Empty, new(0, 0)));
        Assert.Throws<ArgumentException>(() => Point(double.NaN, 0));
        Assert.Throws<ArgumentException>(() => Point(0, double.PositiveInfinity));
    }

    [Fact]
    public async Task FinitePairWithOverflowingDistance_FailsWithoutPublishingInfinity()
    {
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new PointDistanceOperation(Point(-double.MaxValue, 0), Point(double.MaxValue, 0)), default);
        Assert.Equal(VisionExecutionStatus.Failed, result.Status);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task MeasurementSnapshot_SurvivesContextRelease()
    {
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var request = new PointDistanceOperation(Point(0, 0), Point(3, 4));
        var result = await engine.ExecuteAsync(context, request, default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        await engine.ReleaseContextAsync(context, default);
        Assert.Equal(5, result.Value!.Value);
        Assert.Equal(_space, result.Value.CoordinateSpaceId);
    }

    private PixelPoint Point(double x, double y) => new(_frame, _space, new(x, y));
}
