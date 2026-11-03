using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Operations;
using Vision.Core.Results;
using Vision.Tools;
using Xunit;

namespace Vision.Tests.Tools.OpenCv;

[Trait("Category", "Unit")]
public sealed class BlobContourTests
{
    [Fact]
    public async Task ConcaveRegion_AfterFiltering_HasActualBoundary_InInputCoordinates()
    {
        const int width = 50;
        var pixels = new byte[width * 35];
        pixels[width + 2] = 255;
        for (var y = 5; y < 25; y++)
        {
            for (var x = 10; x < (y < 15 ? 20 : 15); x++)
            {
                pixels[y * width + x] = 255;
            }
        }
        var frame = Frame(width, 35, pixels);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var request = new BlobOperation(frame, 128, 255, filters: [new(BlobFeature.Area, 20, 1000)]);
        var result = await engine.ExecuteAsync(context, request, default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        var item = Assert.Single(result.Value!.Items);
        Assert.Equal(1, item.Index);
        Assert.Equal(150, item.Area);
        Assert.Equal(new Rect2D(10, 5, 10, 20), item.Bounds);
        var boundary = Assert.Single(item.Contours);
        Assert.False(boundary.IsHole);
        Assert.True(boundary.Points.Count > 4);
        Assert.All(boundary.Points, point => Assert.Equal(255, pixels[(int)point.Y * width + (int)point.X]));
        Assert.Contains(boundary.Points, point => point.X == 14 && point.Y >= 15);
        Assert.DoesNotContain(new Point2D(19, 24), boundary.Points);
    }

    [Fact]
    public async Task RingAndIsland_PreserveHoleArea_AndSinglePixelComponent()
    {
        const int width = 30;
        var pixels = new byte[width * width];
        for (var y = 5; y < 25; y++)
        {
            for (var x = 5; x < 25; x++)
            {
                if (x < 10 || x >= 20 || y < 10 || y >= 20)
                {
                    pixels[y * width + x] = 255;
                }
            }
        }
        pixels[14 * width + 14] = 255;
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new BlobOperation(Frame(width, width, pixels), 128, 255), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(2, result.Value!.Items.Count);
        var ring = result.Value.Items[0];
        Assert.Equal(300, ring.Area);
        Assert.Single(ring.Contours.Where(contour => contour.IsHole));
        Assert.Single(ring.Contours.Where(contour => !contour.IsHole));
        var island = result.Value.Items[1];
        Assert.Equal(1, island.Area);
        Assert.Equal(new Point2D(14, 14), Assert.Single(Assert.Single(island.Contours).Points));
        await engine.ReleaseContextAsync(context, default);
        Assert.All(ring.Contours, contour => Assert.NotEmpty(contour.Points));
    }

    [Fact]
    public async Task NoForeground_IsNoMatchWithReadableEmptySnapshot()
    {
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new BlobOperation(Frame(5, 5, new byte[25]), 128, 255), default);
        Assert.Equal(VisionExecutionStatus.NoMatch, result.Status);
        Assert.Empty(result.Value!.Items);
    }

    private static VisionFrame Frame(int width, int height, byte[] pixels)
        => new(Guid.NewGuid(), Guid.NewGuid(), width, height, PixelFormat.Mono8, pixels);
}
