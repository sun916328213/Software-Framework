using System.Text.Json;
using Vision.Core.Geometry;
using Xunit;

namespace Vision.Tests.Core;

[Trait("Category", "Contract")]
public sealed class RegionContourTests
{
    [Fact]
    public void OlderContourJson_WithoutClosureFlag_RemainsClosed()
    {
        var contour = JsonSerializer.Deserialize<RegionContour>("{\"Points\":[{\"X\":1,\"Y\":2}],\"IsHole\":false}")!;
        Assert.True(contour.IsClosed);
        Assert.Equal(new Point2D(1, 2), Assert.Single(contour.Points));
    }

    [Fact]
    public void ContourOwnsCoordinateSnapshot_AndRoundTripsWithoutSdkTypes()
    {
        Point2D[] points = [new(1, 2), new(3, 4), new(5, 6)];
        var contour = new RegionContour(points, isHole: true, isClosed: false);
        points[0] = new(99, 99);
        Assert.Equal(new Point2D(1, 2), contour.Points[0]);
        Assert.Throws<NotSupportedException>(() => { ((IList<Point2D>)contour.Points)[0] = new(88, 88); });
        var copy = JsonSerializer.Deserialize<RegionContour>(JsonSerializer.Serialize(contour))!;
        Assert.True(copy.IsHole);
        Assert.False(copy.IsClosed);
        Assert.Equal(contour.Points.ToArray(), copy.Points.ToArray());
    }
}
