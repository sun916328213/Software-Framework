using Vision.Core.Geometry;
using Vision.Core.Roi;
using Xunit;

namespace Vision.Tests.Core;

[Trait("Category", "Unit")]
public sealed class SearchRegionTests
{
    [Theory]
    [InlineData(90)]
    [InlineData(-90)]
    [InlineData(180)]
    [InlineData(270)]
    public void QuarterTurnRectangle_PreservesExactHalfOpenPixelCount(double angle)
    {
        var region = new SearchRegion(Roi.RotatedRectangle(10, 20, 4, 2, angle));
        var contained = Enumerable.Range(0, 40).Sum(y => Enumerable.Range(0, 40).Count(x => region.Contains(x, y)));
        Assert.Equal(8, contained); // 不经过Runs/包围框，独立检查像素中心半开规则。
    }

    [Fact]
    public void Rectangle_UsesPixelCenters_HalfOpenBounds_AndIndependentSnapshot()
    {
        var roi = Roi.Rectangle(5, 6, 4, 6);
        var scope = new SearchRegion(roi);
        roi.Pose = new(100, 100, 0); roi.Size = new(1, 1);
        var runs = scope.Runs(20, 20).ToArray();
        Assert.Equal(24, runs.Sum(r => r.End - r.Start + 1));
        Assert.Equal((3, 3, 6), runs[0]);
        Assert.Equal((8, 3, 6), runs[^1]);
        Assert.False(scope.Contains(7, 6));
        Assert.False(scope.Contains(5, 9));
    }
    [Fact]
    public void CircleEllipseRotationAndPolygon_UseActualShape_NotTheirBoundingBox()
    {
        var circle = new SearchRegion(Roi.Circle(5, 5, 4));
        Assert.Equal(13, circle.Runs(20, 20).Sum(r => r.End - r.Start + 1));
        Assert.False(circle.Contains(3, 3));
        var ellipse = new SearchRegion(Roi.Ellipse(10, 10, 8, 2, 90));
        Assert.True(ellipse.Contains(10, 13)); Assert.False(ellipse.Contains(13, 10));
        var rotated = new SearchRegion(Roi.RotatedRectangle(10, 10, 8, 2, 45));
        Assert.True(rotated.Contains(12, 12)); Assert.False(rotated.Contains(12, 8));
        var polygon = Roi.Polygon([new(1, 1), new(8, 1), new(1, 8)]);
        var snapshot = new SearchRegion(polygon);
        polygon.OutlinePoints.Clear();
        Assert.True(snapshot.Contains(2, 2)); Assert.False(snapshot.Contains(7, 7));
        Assert.True(snapshot.Contains(1, 1));
    }
    [Fact]
    public void ClippingPreservesOrigin_AndDisjointRegionIsEmpty_NotFullImage()
    {
        var scope = new SearchRegion(Roi.Rectangle(0, 0, 4, 4));
        Assert.Equal(new[] { (0, 0, 1), (1, 0, 1) }, scope.Runs(10, 10));
        Assert.Empty(new SearchRegion(Roi.Circle(100, 100, 2)).Runs(10, 10));
    }
    [Fact]
    public void InvalidOrUnsupportedRoi_IsRejected_NotSilentlyTreatedAsFullImage()
    {
        Assert.Throws<ArgumentException>(() => new SearchRegion(Roi.Rectangle(5, 5, 0, 10)));
        Assert.Throws<ArgumentException>(() => new SearchRegion(Roi.Circle(double.NaN, 5, 2)));
        Assert.Throws<ArgumentException>(() => new SearchRegion(new Roi { Kind = RoiKind.Mask, Pose = new(5, 5, 0), Size = new(10, 10) }));
        Assert.Throws<ArgumentException>(() => new SearchRegion(Roi.Polygon([new(0, 0), new(1, 1), new(double.NaN, 2)])));
    }
}
