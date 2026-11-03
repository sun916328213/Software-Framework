using Vision.Core.Geometry;
using Vision.Core.Operations;
using Vision.Core.Results;
using Vision.Tools;
using Xunit;

namespace Vision.Tests.Tools.OpenCv;

[Trait("Category", "Regression")]
public sealed class LinePairTests
{
    private readonly Guid _frame = Guid.NewGuid();
    private readonly Guid _space = Guid.NewGuid();

    [Theory]
    [InlineData(0, 0)]
    [InlineData(10, 20)]
    [InlineData(100, -30)]
    public async Task SupportingLines_ProducePixelIntersection_IncludingOriginAndOutsideSegments(double x, double y)
    {
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new LineIntersectionOperation(
            Line(new(0, y), new(10, y)), Line(new(x, 40), new(x, 50))), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(new Point2D(x, y), result.Value!.Point!.Position);
        Assert.Equal(_frame, result.Value.Point.FrameId);
        Assert.Equal(_space, result.Value.Point.CoordinateSpaceId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public async Task ParallelOrCoincidentLines_ReturnNoMatchWithoutInventingOrigin(double y)
    {
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new LineIntersectionOperation(
            Line(new(0, 0), new(10, 0)), Line(new(0, y), new(10, y))), default);
        Assert.Equal(VisionExecutionStatus.NoMatch, result.Status);
        Assert.Null(result.Value!.Point);
    }

    [Fact]
    public void WrongSourcesMissingAndInvalidLines_AreRejectedWithoutFallback()
    {
        var valid = Line(new(0, 0), new(10, 0));
        Assert.Throws<ArgumentNullException>(() => new LineIntersectionOperation(valid, null!));
        Assert.Throws<ArgumentException>(() => new LineIntersectionOperation(valid, new PixelLine(Guid.NewGuid(), _space, new(0, 0), new(0, 1))));
        Assert.Throws<ArgumentException>(() => new LineIntersectionOperation(valid, new PixelLine(_frame, Guid.NewGuid(), new(0, 0), new(0, 1))));
        Assert.Throws<ArgumentException>(() => Line(new(0, 0), new(0, 0)));
        Assert.Throws<ArgumentException>(() => Line(new(0, 0), new(double.NaN, 1)));
        Assert.Throws<ArgumentException>(() => Line(new(-double.MaxValue, 0), new(double.MaxValue, 1)));
        Assert.Throws<ArgumentException>(() => new PixelLine(Guid.Empty, _space, new(0, 0), new(0, 1)));
    }

    [Fact]
    public async Task SnapshotOutlivesContext_AndIntersectionRemainsTypedPixelPoint()
    {
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var first = Line(new(0, 20), new(10, 20));
        var second = Line(new(10, 40), new(10, 50));
        var request = new LineIntersectionOperation(first, second);
        Assert.Null(request.Image);
        var result = await engine.ExecuteAsync(context, request, default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        await engine.ReleaseContextAsync(context, default);
        Assert.Equal(new Point2D(10, 20), result.Value!.Point!.Position);
        Assert.Equal(_space, result.Value.Point.CoordinateSpaceId);
    }

    private PixelLine Line(Point2D start, Point2D end) => new(_frame, _space, start, end);
}
