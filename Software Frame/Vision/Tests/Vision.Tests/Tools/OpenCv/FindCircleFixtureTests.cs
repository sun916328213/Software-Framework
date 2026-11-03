using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Presentation;
using Vision.Core.Roi;
using Vision.Tools.OpenCv.Measurement;
using Xunit;
using Roi = Vision.Core.Roi.Roi;

namespace Vision.Tests.Tools.OpenCv;
[Trait("Category", "Regression")]
public sealed class FindCircleFixtureTests
{
    [Fact]
    public void NoRegion_PreservesOriginalNearestExpectedCenterAndReportsAppliedPoint()
    {
        using var tool = Tool(new(60, 70)); using var result = tool.Run(TwoCircles()); Assert.True(result.Success, result.Message);
        Assert.InRange(result.Center.X, 58, 62); Assert.InRange(result.Center.Y, 68, 72); Assert.Equal(new Point2D(60, 70), result.AppliedExpectedCenter);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RegionFiltersCandidateCenters_NotPixelsOrNearestOutsideCircle(bool empty)
    {
        using var tool = Tool(new(60, 70)); var capability = Assert.IsAssignableFrom<ISearchRegionFixtureTool>(tool);
        capability.SearchRegion = new SearchRegion(empty ? Roi.Rectangle(220, 160, 12, 12) : Roi.Rectangle(160, 100, 60, 60));
        var image = TwoCircles(); var before = image.Pixels.ToArray(); using var result = tool.Run(image);
        if (empty) { Assert.False(result.Success); Assert.False(result.TryGetCircleField("Circle", out _)); }
        else { Assert.True(result.Success, result.Message); Assert.InRange(result.Center.X, 158, 162); Assert.InRange(result.Center.Y, 98, 102); Assert.InRange(result.Radius, 23, 27); }
        Assert.Equal(before, image.Pixels); Assert.Equal(new Point2D(60, 70), tool.Parameters.ExpectedCenter);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    public void RigidPreparationMovesExpectedCenterWithRegion_AndDoesNotSelectDistractor(double angle)
    {
        using var tool = Tool(angle == 0 ? new(160, 100) : new(140, 100)); var capability = Assert.IsAssignableFrom<ISearchRegionFixtureTool>(tool);
        var reference = angle == 0 ? Pose2D.Identity : new Pose2D(100, 100, 0); var current = angle == 0 ? new Pose2D(-70, -10, 0) : new Pose2D(100, 100, 90);
        var roi = angle == 0 ? Roi.Rectangle(185, 100, 110, 60) : Roi.Rectangle(120, 100, 100, 100);
        var expected = angle == 0 ? new Point2D(90, 90) : new Point2D(100, 140);
        var image = angle == 0 ? Scene((90, 90, 20), (140, 90, 15)) : Scene((100, 140, 20), (140, 100, 20));
        var region = SearchRegionPreparation.Capture(roi, reference, current);
        using var result = Assert.IsType<FindCircleResult>(capability.PrepareSearchRegionRun(image, region, reference, current)());
        Assert.True(result.Success, result.Message); Assert.Equal(expected, result.AppliedExpectedCenter);
        Assert.InRange(result.Center.X, expected.X - 2, expected.X + 2); Assert.InRange(result.Center.Y, expected.Y - 2, expected.Y + 2);
        Assert.Equal(angle == 0 ? new Point2D(160, 100) : new Point2D(140, 100), tool.Parameters.ExpectedCenter);
    }

    [Theory]
    [InlineData("nan")]
    [InlineData("overflow")]
    [InlineData("missing-region")]
    public void InvalidPoseOrMissingFollowRegion_CannotUseOriginalCenter(string invalid)
    {
        using var tool = Tool(new(60, 70)); var capability = Assert.IsAssignableFrom<ISearchRegionFixtureTool>(tool);
        var reference = invalid == "overflow" ? new Pose2D(-double.MaxValue, 0, 0) : Pose2D.Identity;
        var current = invalid == "nan" ? new Pose2D(0, 0, double.NaN) : new Pose2D(double.MaxValue, 0, 0);
        var region = invalid == "missing-region" ? null : new SearchRegion(Roi.Rectangle(60, 70, 60, 60));
        if (invalid == "missing-region") Assert.Throws<InvalidOperationException>(() => capability.PrepareSearchRegionRun(TwoCircles(), region, reference, current));
        else Assert.Throws<ArgumentException>(() => capability.PrepareSearchRegionRun(TwoCircles(), region, reference, current));
    }

    [Fact]
    public void SnapshotOwnsPixelsExpectedCenterAndImmutableDomain_AfterRecipeRoiMutationAndDispose()
    {
        var tool = Tool(new(60, 70)); var capability = Assert.IsAssignableFrom<ISearchRegionFixtureTool>(tool); var roi = Roi.Rectangle(160, 100, 60, 60);
        capability.SearchRegion = new SearchRegion(roi); var image = TwoCircles(); var run = Assert.IsAssignableFrom<IBackgroundImageTool>(tool).PrepareRun(image);
        tool.Parameters.ExpectedCenter = new(0, 0); capability.SearchRegion = null; roi.Pose = new(0, 0, 0); image.Pixels.AsSpan().Clear(); tool.Dispose();
        using var result = Assert.IsType<FindCircleResult>(run()); Assert.True(result.Success, result.Message); Assert.InRange(result.Center.X, 158, 162); Assert.Equal(new Point2D(60, 70), result.AppliedExpectedCenter);
    }
    private static FindCircleTool Tool(Point2D expected) => new() { Parameters = new() { ExpectedCenter = expected, RadiusMin = 12, RadiusMax = 28, CannyHigh = 100, AccumThreshold = 18 } };
    private static VisionImage TwoCircles() => Scene((60, 70, 20), (160, 100, 25));
    private static VisionImage Scene(params (int X, int Y, int R)[] circles)
    {
        var pixels = new byte[240 * 220]; foreach (var c in circles) for (var y = 0; y < 220; y++) for (var x = 0; x < 240; x++) if ((x - c.X) * (x - c.X) + (y - c.Y) * (y - c.Y) <= c.R * c.R) pixels[y * 240 + x] = 255;
        return VisionImage.FromBuffer(240, 220, PixelFormat.Mono8, pixels);
    }
}
