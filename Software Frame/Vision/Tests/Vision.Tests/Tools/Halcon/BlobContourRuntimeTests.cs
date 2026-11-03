using Vision.Core.Imaging;
using Vision.Tools.Halcon.Inspection;
using Xunit;

namespace Vision.Tests.Tools.Halcon;

public sealed class BlobContourRuntimeTests
{
    [Fact(Skip = "需要 HALCON Runtime 和 License；仅在授权离线 SDK 环境手动执行。")]
    [Trait("Category", "RequiresRuntime")]
    public void FinalFilledRegion_ExportsManagedBoundary_WithoutChangingFillUpSemantics()
    {
        const int width = 30;
        var pixels = new byte[width * width];
        for (var y = 5; y < 25; y++)
            for (var x = 5; x < 25; x++)
                if (x < 10 || x >= 20 || y < 10 || y >= 20) pixels[y * width + x] = 255;
        using var tool = new BlobTool();
        tool.Parameters.MinGray = 128;
        tool.Parameters.Features = [];
        using var result = tool.RunImage(VisionImage.FromBuffer(width, width, PixelFormat.Mono8, pixels));
        Assert.True(result.Success, result.Message);
        var item = Assert.Single(result.Items);
        Assert.Equal(400, item.Area); // 保留既有 HALCON FillUp，不假称与 OpenCV 的 300 相同。
        Assert.Equal(20, item.Bounds.Width);
        Assert.NotEmpty(item.Contours);
        Assert.All(item.Contours, c => { Assert.False(c.IsHole); Assert.NotEmpty(c.Points); });
        tool.Dispose();
        Assert.All(item.Contours.SelectMany(c => c.Points), p => Assert.True(double.IsFinite(p.X) && double.IsFinite(p.Y)));
    }

    [Fact(Skip = "需要 HALCON Runtime 和 License；仅在授权离线 SDK 环境手动执行。")]
    [Trait("Category", "RequiresRuntime")]
    public void ConcaveRegion_BoundaryIsNotBoundingBox()
    {
        const int width = 40;
        var pixels = new byte[width * width];
        for (var y = 5; y < 25; y++)
            for (var x = 10; x < (y < 15 ? 20 : 15); x++) pixels[y * width + x] = 255;
        using var tool = new BlobTool();
        tool.Parameters.MinGray = 128;
        tool.Parameters.Features = [];
        using var result = tool.RunImage(VisionImage.FromBuffer(width, width, PixelFormat.Mono8, pixels));
        Assert.True(result.Success, result.Message);
        var item = Assert.Single(result.Items);
        Assert.Equal(150, item.Area);
        var points = Assert.Single(item.Contours).Points;
        Assert.Contains(points, p => p.X == 14 && p.Y >= 15);
        Assert.DoesNotContain(points, p => p.X == 19 && p.Y == 24);
    }
}
