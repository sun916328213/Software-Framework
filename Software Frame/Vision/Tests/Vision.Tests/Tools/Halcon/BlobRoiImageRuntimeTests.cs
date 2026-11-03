using Vision.Core.Imaging;
using Vision.Core.Roi;
using Vision.Tools.Halcon.Inspection;
using Xunit;

namespace Vision.Tests.Tools.Halcon;

public sealed class BlobRoiImageRuntimeTests
{
    [Fact(Skip = "需要 HALCON Runtime/License；仅在授权离线环境执行。")]
    [Trait("Category", "RequiresRuntime")]
    public void IntermediateImage_CapturesThresholdBeforeFillUp_AndSurvivesSdkCleanup()
    {
        var pixels = Enumerable.Repeat((byte)255, 100).ToArray();
        for (var y = 3; y < 7; y++) for (var x = 3; x < 7; x++) pixels[y * 10 + x] = 0;
        var input = VisionImage.FromBuffer(10, 10, PixelFormat.Mono8, pixels); input.FrameId = 99;
        using var tool = new BlobTool { SearchRegion = new(Roi.Rectangle(5, 5, 8, 8)) };
        tool.Parameters.MinGray = 128; tool.Parameters.Features = [];
        using var result = tool.RunImage(input);
        Assert.True(result.Success, result.Message); Assert.Equal(64, Assert.Single(result.Items).Area);
        var stage = Assert.IsType<VisionImage>(result.RoiImage);
        Assert.Equal(99, stage.FrameId); Assert.Equal(input.Timestamp, stage.Timestamp);
        Assert.Equal(48, stage.Pixels.Count(pixel => pixel == 255));
        Assert.Equal((byte)0, stage.Pixels[4 * 10 + 4]); Assert.Equal((byte)0, stage.Pixels[0]);
        Assert.Equal((byte)255, stage.Pixels[1 * 10 + 1]);
        tool.Dispose(); result.Dispose(); Assert.Null(result.RoiImage);
        Assert.Equal(48, stage.Pixels.Count(pixel => pixel == 255));
    }
    [Fact(Skip = "需要 HALCON Runtime/License；仅在授权离线环境执行。")]
    [Trait("Category", "RequiresRuntime")]
    public void ZeroThresholdAndEmptyDomain_KeepOriginalCoordinatesAndFullImageSize()
    {
        using var tool = new BlobTool { SearchRegion = new(Roi.Rectangle(4, 4, 4, 4)) };
        tool.Parameters.MinGray = 0; tool.Parameters.MaxGray = 0; tool.Parameters.Features = [];
        var input = VisionImage.FromBuffer(8, 8, PixelFormat.Mono8, new byte[64]);
        using var zero = tool.RunImage(input);
        Assert.True(zero.Success, zero.Message); Assert.Equal(16, zero.RoiImage!.Pixels.Count(pixel => pixel == 255));
        Assert.Equal((byte)0, zero.RoiImage.Pixels[0]);
        tool.SearchRegion = new(Roi.Circle(100, 100, 4));
        using var empty = tool.RunImage(input);
        Assert.True(empty.Success, empty.Message); Assert.Empty(empty.Items);
        var stage = Assert.IsType<VisionImage>(empty.RoiImage);
        Assert.Equal(8, stage.Width); Assert.Equal(8, stage.Height);
        Assert.All(stage.Pixels, pixel => Assert.Equal((byte)0, pixel));
    }
}
