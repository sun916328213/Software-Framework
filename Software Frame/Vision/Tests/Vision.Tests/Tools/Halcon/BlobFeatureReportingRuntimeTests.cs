using Vision.Core.Imaging;
using Vision.Tools.Halcon.Inspection;
using Xunit;

namespace Vision.Tests.Tools.Halcon;

public sealed class BlobFeatureReportingRuntimeTests
{
    [Fact(Skip = "需要 HALCON Runtime 和 License；仅在授权离线 SDK 环境手动执行。")]
    [Trait("Category", "RequiresRuntime")]
    public void SelectedFeaturesReportRegionFeaturesOfTheFinalRegion()
    {
        var pixels = new byte[1600];
        for (var y = 10; y < 20; y++) for (var x = 10; x < 20; x++) pixels[y * 40 + x] = 255;
        using var tool = new BlobTool();
        tool.Parameters.MinGray = 200;
        tool.Parameters.Features = new[] { "area", "width", "height", "row", "column" }
            .Select(name => new BlobFeatureRange { Feature = name, Min = 0, Max = 99999 }).ToList();
        using var result = tool.RunImage(VisionImage.FromBuffer(40, 40, PixelFormat.Mono8, pixels));
        Assert.True(result.Success, result.Message);
        var item = Assert.Single(result.Items);
        Assert.Equal(100, item.FeatureValues["area"]); Assert.Equal(10, item.FeatureValues["width"]); Assert.Equal(10, item.FeatureValues["height"]);
        Assert.Equal(14.5, item.FeatureValues["row"]); Assert.Equal(14.5, item.FeatureValues["column"]);
    }
}
