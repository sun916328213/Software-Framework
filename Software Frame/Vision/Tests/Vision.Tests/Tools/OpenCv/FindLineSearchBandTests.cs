using Vision.Core.Imaging;
using Vision.Tools.OpenCv.Measurement;
using Xunit;

namespace Vision.Tests.Tools.OpenCv;

public sealed class FindLineSearchBandTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DetectedLine_UsesFittedOffset_AndKeepsPathEndpointOrder(bool reverse)
    {
        var pixels = new byte[100 * 100];
        Array.Fill(pixels, (byte)255, 45 * 100, 55 * 100);
        var image = new VisionImage { Width = 100, Height = 100, Format = PixelFormat.Mono8, Pixels = pixels };
        using var tool = new FindLineTool
        {
            Parameters = new() { LineStart = new(reverse ? 90 : 10, 50), LineEnd = new(reverse ? 10 : 90, 50), BandHalfWidth = 10 }
        };
        using var result = tool.Run(image);
        Assert.True(result.Success, result.Message);
        Assert.InRange(result.Start.Y, 43, 46); Assert.InRange(result.End.Y, 43, 46);
        Assert.True(reverse ? result.Start.X > result.End.X : result.Start.X < result.End.X);
    }

    [Theory]
    [InlineData(0.0, false, false)]
    [InlineData(0.0, true, false)]
    [InlineData(0.2, false, false)]
    [InlineData(0.0, false, true)]
    [InlineData(0.0, true, true)]
    [InlineData(0.2, false, true)]
    public void ShortReflectionsInsideBand_DoNotPullTheLongObjectBoundaryAway(double slope, bool reverse, bool vertical)
    {
        const int width = 200, height = 140;
        var pixels = new byte[width * height];
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var offset = y - (60 + slope * (x - 100));
                pixels[y * width + x] = offset >= 0 ? (byte)128 : (byte)0;
                // 单一长边界旁的局部反光，不是两条同等长度的候选目标。
                if (x >= 30 && x < 65 && offset >= 12 && offset < 18) pixels[y * width + x] = 255;
            }
        if (vertical)
        {
            var transposed = new byte[pixels.Length];
            for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
                transposed[x * height + y] = pixels[y * width + x];
            pixels = transposed;
        }
        var original = pixels.ToArray();
        Vision.Core.Geometry.Point2D PathPoint(double x)
        {
            var y = 60 + slope * (x - 100);
            return vertical ? new(y, x) : new(x, y);
        }
        using var tool = new FindLineTool
        {
            Parameters = new()
            {
                LineStart = PathPoint(reverse ? 190 : 10),
                LineEnd = PathPoint(reverse ? 10 : 190),
                BandHalfWidth = 25
            }
        };
        using var result = tool.Run(VisionImage.FromBuffer(vertical ? height : width, vertical ? width : height, PixelFormat.Mono8, pixels));
        Assert.True(result.Success, result.Message);
        foreach (var point in new[] { result.Start, result.End })
        {
            var x = vertical ? point.Y : point.X; var y = vertical ? point.X : point.Y;
            var error = Math.Abs(y - (60 + slope * (x - 100))) / Math.Sqrt(1 + slope * slope);
            Assert.InRange(error, 0, 1.5); // 解析边界独立给定；允许Canny整数定位误差，不宣称亚像素。
        }
        var alongStart = vertical ? result.Start.Y : result.Start.X;
        var alongEnd = vertical ? result.End.Y : result.End.X;
        Assert.True(reverse ? alongStart > alongEnd : alongStart < alongEnd);
        Assert.True(result.EdgePointCount >= 100);
        Assert.Equal(original, pixels);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void NonFiniteCannyThresholds_AreRejectedBeforeCallingOpenCv(double value)
    {
        using var tool = new FindLineTool();
        tool.Parameters.SearchMode = Vision.Core.Geometry.LineSearchKind.EdgeBand;
        tool.Parameters.CannyLow = value;
        Assert.Contains(tool.Validate().Issues, issue => issue.Code == "CANNY_LOW");
        tool.Parameters.CannyLow = 50; tool.Parameters.CannyHigh = value;
        Assert.Contains(tool.Validate().Issues, issue => issue.Code == "CANNY_HIGH");
    }
    [Fact]
    public void EdgesBeyondPathEndpoints_AreNotInsideTheDisplayedSearchBand()
    {
        var pixels = new byte[100 * 100];
        for (var y = 51; y < 90; y++) for (var x = 60; x < 90; x++) pixels[y * 100 + x] = 255;
        var image = new VisionImage { Width = 100, Height = 100, Format = PixelFormat.Mono8, Pixels = pixels };
        using var tool = new FindLineTool
        {
            Parameters = new() { LineStart = new(10, 50), LineEnd = new(30, 50), BandHalfWidth = 3, MinEdgePoints = 2 }
        };
        using var outside = tool.Run(image);
        Assert.False(outside.Success);
        tool.Parameters.LineStart = new(55, 50); tool.Parameters.LineEnd = new(95, 50);
        using var inside = tool.Run(image);
        Assert.True(inside.Success, inside.Message);
        Assert.True(inside.EdgePointCount >= 2);
    }
}
