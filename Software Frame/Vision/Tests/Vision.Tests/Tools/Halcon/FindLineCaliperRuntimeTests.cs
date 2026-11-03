using Vision.Core.Imaging;
using Vision.Tools.Halcon.Measurement;
using Xunit;

namespace Vision.Tests.Tools.Halcon;

public sealed class FindLineCaliperRuntimeTests
{
    [Fact(Skip = "需要 HALCON Runtime/License；只在授权离线环境执行。")]
    [Trait("Category", "RequiresRuntime")]
    public void SelectionAndClipping_AffectActualMeasurement_NotOnlyTheEditor()
    {
        var pixels = new byte[120 * 100];
        for (var y = 40; y < 60; y++) for (var x = 0; x < 120; x++) pixels[y * 120 + x] = 255;
        var image = VisionImage.FromBuffer(120, 100, PixelFormat.Mono8, pixels);
        using var tool = new FindLineTool
        {
            Parameters = new() { LineStart = new(20, 50), LineEnd = new(100, 50), CaliperCount = 9,
                CaliperHalfLength = 30, CaliperHalfWidth = 2, Threshold = 20, Sigma = 1, EdgeSelection = CaliperEdgeSelection.First }
        };
        using var first = tool.RunImage(image);
        Assert.True(first.Success, first.Message); Assert.Equal(9, first.EdgePointCount);
        tool.Parameters.EdgeSelection = CaliperEdgeSelection.Last;
        using var last = tool.RunImage(image);
        Assert.True(last.Success, last.Message); Assert.Equal(9, last.EdgePointCount);
        Assert.InRange(first.Start.Y, 38, 61); Assert.InRange(last.Start.Y, 38, 61);
        Assert.True(Math.Abs(first.Start.Y - last.Start.Y) > 10);
        tool.Parameters.IgnoreEndPoints = 2;
        using var clipped = tool.RunImage(image);
        Assert.True(clipped.Success, clipped.Message); Assert.Equal(5, clipped.EdgePointCount);
        tool.Parameters.IgnoreEndPoints = int.MaxValue;
        using var insufficient = tool.RunImage(image);
        Assert.False(insufficient.Success); Assert.Contains("不足 2 个", insufficient.Message);
    }
}
