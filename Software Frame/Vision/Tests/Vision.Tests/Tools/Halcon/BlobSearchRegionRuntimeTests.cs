using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Roi;
using Vision.Tests.Tools;
using Vision.Tools.Halcon.Inspection;
using Xunit;

namespace Vision.Tests.Tools.Halcon;

public sealed class BlobSearchRegionRuntimeTests
{
    [Fact(Skip = "需要 HALCON Runtime/License；在授权离线 SDK 环境执行。")]
    [Trait("Category", "RequiresRuntime")]
    public void ReducedDomain_PrecedesConnection_PreservesInputCoordinates_AndSupportsZeroGray()
    {
        var pixels = new byte[20 * 20];
        for (var y = 0; y < 16; y++)
            foreach (var x in new[] { 2, 3, 10, 11 }) pixels[y * 20 + x] = 255;
        for (var y = 12; y < 14; y++) for (var x = 2; x < 12; x++) pixels[y * 20 + x] = 255;
        using var tool = new BlobTool { SearchRegion = new(Roi.Rectangle(10, 7, 20, 6)) };
        tool.Parameters.MinGray = 128; tool.Parameters.Features = [];
        using var result = tool.RunImage(VisionImage.FromBuffer(20, 20, PixelFormat.Mono8, pixels));
        Assert.True(result.Success, result.Message);
        Assert.Equal(2, result.Items.Count);
        Assert.True(result.TryGetNumericField("Count", out var count)); Assert.Equal(2, count!.Value); Assert.Equal(Vision.Core.Results.NumericUnit.Count, count.Unit);
        Assert.All(result.Items, item => Assert.Equal(12, item.Area));
        Assert.Equal(new Rect2D(2, 4, 2, 6), result.Items[0].Bounds);
        Assert.Equal(new Rect2D(10, 4, 2, 6), result.Items[1].Bounds);
        tool.SearchRegion = new(Roi.Circle(5, 5, 4));
        tool.Parameters.MinGray = 0; tool.Parameters.MaxGray = 0;
        var black = VisionImage.FromBuffer(20, 20, PixelFormat.Mono8, new byte[400]);
        using var zero = tool.RunImage(black);
        Assert.True(zero.Success, zero.Message); Assert.Equal(13, Assert.Single(zero.Items).Area);
        tool.SearchRegion = new(Roi.Rectangle(100, 100, 4, 4));
        using var empty = tool.RunImage(black);
        Assert.True(empty.Success, empty.Message); Assert.Empty(empty.Items);
        Assert.True(empty.TryGetNumericField("Count", out var zeroCount)); Assert.Equal(0, zeroCount!.Value);
    }
    [Fact(Skip = "需要 HALCON Runtime/License；在授权离线 SDK 环境验证最小面积预筛和快照复制。")]
    [Trait("Category", "RequiresRuntime")]
    public void MinArea_PrefiltersBeforeFeatureSelection_AndSurvivesPreparedRun()
    {
        var image = SyntheticImages.WhiteRectangle(20, 20, 5, 5, 4, 4); // 面积 16。
        using var tool = new BlobTool();
        tool.Parameters.MinGray = 128;
        tool.Parameters.MinArea = 16;
        using var kept = tool.RunImage(image);
        Assert.True(kept.Success, kept.Message);
        Assert.Equal(16, Assert.Single(kept.Items).Area);
        Assert.True(kept.TryGetPointField("SingleCenter", out var center)); Assert.Equal(new Point2D(6.5, 6.5), center!.Position);

        tool.Parameters.MinArea = 17;
        var execution = tool.PrepareRun(image);
        tool.Parameters.MinArea = 0;
        using var filtered = Assert.IsType<BlobResult>(execution());
        Assert.True(filtered.Success, filtered.Message);
        Assert.Empty(filtered.Items);
        Assert.True(filtered.TryGetNumericField("Count", out var count)); Assert.Equal(0, count!.Value);
        Assert.False(filtered.TryGetPointField("SingleCenter", out _));
    }

    [Fact(Skip = "需要 HALCON Runtime/License；在授权离线 SDK 环境验证后台线程和资源释放。")]
    [Trait("Category", "RequiresRuntime")]
    public async Task PreparedRun_DoesNotBorrowDisposedOriginalToolOrMutableInput()
    {
        var tool = new BlobTool { SearchRegion = new(Roi.Rectangle(5, 5, 4, 4)) };
        tool.Parameters.MaxGray = 0; tool.Parameters.Features = [];
        var image = VisionImage.FromBuffer(20, 20, PixelFormat.Mono8, new byte[400]);
        var execution = tool.PrepareRun(image);
        Array.Fill(image.Pixels, (byte)255); tool.Parameters.MinGray = 200; tool.SearchRegion = null;
        tool.Dispose();
        using var result = Assert.IsType<BlobResult>(await Task.Run(execution));
        Assert.True(result.Success, result.Message); Assert.Equal(16, Assert.Single(result.Items).Area);
    }
}
