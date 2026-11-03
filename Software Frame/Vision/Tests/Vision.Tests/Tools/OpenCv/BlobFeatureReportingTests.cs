using Vision.Core.Imaging;
using Vision.Tools.OpenCv.Inspection;
using Xunit;

namespace Vision.Tests.Tools.OpenCv;

public sealed class BlobFeatureReportingTests
{
    [Fact]
    public void SelectedFeaturesReportTheExactMetricsUsedForFiltering_NotBoundsApproximations()
    {
        var pixels = new byte[1600];
        for (var y = 10; y < 20; y++) for (var x = 10; x < 20; x++) pixels[y * 40 + x] = 255;
        using var tool = new BlobTool();
        tool.Parameters.MinGray = 200; tool.Parameters.MinArea = 1;
        tool.Parameters.Features = BlobTool.SupportedFeatures.Select(name => new BlobFeatureRange { Feature = name, Min = -10000, Max = 10000 }).ToList();
        using var result = tool.Run(VisionImage.FromBuffer(40, 40, PixelFormat.Mono8, pixels));
        Assert.True(result.Success, result.Message);
        var item = Assert.Single(result.Items);
        var values = Assert.IsAssignableFrom<IReadOnlyDictionary<string, double>>(item.GetType().GetProperty("FeatureValues")?.GetValue(item));
        Assert.Equal(10, values.Count);
        Assert.Equal(100, values["area"]); Assert.Equal(14.5, values["row"]); Assert.Equal(14.5, values["column"]);
        Assert.Equal(10, values["width"]); Assert.Equal(10, values["height"]);
        Assert.Equal(0, values["phi"], 6); Assert.Equal(Math.PI / 4, values["circularity"], 6);
        Assert.Equal(1, values["rectangularity"], 6); Assert.Equal(Math.Sqrt(50), values["outer_radius"], 3); Assert.Equal(5, values["inner_radius"], 6);
    }
}
