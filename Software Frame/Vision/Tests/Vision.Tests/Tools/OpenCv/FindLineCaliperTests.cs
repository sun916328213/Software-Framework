using System.Text.Json;
using System.Text.Json.Serialization;
using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Tools.OpenCv.Measurement;
using Xunit;

namespace Vision.Tests.Tools.OpenCv;

public sealed class FindLineCaliperTests
{
    [Fact]
    public void MissingMode_KeepsLegacyBand_WhileNewToolsUseCalipers_AndBothRoundTrip()
    {
        var legacy = JsonSerializer.Deserialize<FindLineParameters>("{\"LineStart\":{\"X\":20,\"Y\":70},\"LineEnd\":{\"X\":160,\"Y\":70},\"BandHalfWidth\":12,\"CannyLow\":33,\"CannyHigh\":99}")!;
        Assert.Equal(LineSearchKind.EdgeBand, legacy.SearchMode);
        Assert.Equal(12, legacy.BandHalfWidth); Assert.Equal(33, legacy.CannyLow); Assert.Equal(99, legacy.CannyHigh);
        using var tool = new FindLineTool();
        Assert.Equal(LineSearchKind.Calipers, tool.Parameters.SearchMode);
        var options = new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } };
        tool.Parameters.Transition = EdgeTransition.Negative;
        tool.Parameters.EdgeSelection = CaliperEdgeSelection.Last;
        tool.Parameters.Sigma = 1.4; tool.Parameters.IgnoreEndPoints = 1;
        foreach (var original in new[] { legacy, tool.Parameters })
        {
            var json = JsonSerializer.Serialize(original, options);
            var restored = JsonSerializer.Deserialize<FindLineParameters>(json, options)!;
            Assert.Equal(json, JsonSerializer.Serialize(restored, options));
            Assert.Equal(((ILineSearchRegion)original).SearchKind, ((ILineSearchRegion)restored).SearchKind);
            Assert.DoesNotContain("UsesCalipers", json); Assert.DoesNotContain("NormalHalfLength", json);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ProjectionAndSubpixelPeak_MeasureAnalyticBoundary_WithPathOrderAndPolarity(bool vertical, bool reverse)
    {
        const int size = 180;
        var pixels = new byte[size * size];
        for (var y = 0; y < size; y++) for (var x = 0; x < size; x++)
        {
            var distance = vertical ? x - 72.35 : 72.35 - y;
            pixels[y * size + x] = (byte)Math.Round(127.5 * (1 + Math.Tanh(distance / 1.2)));
        }
        var original = pixels.ToArray();
        using var tool = Tool();
        tool.Parameters.LineStart = vertical ? new(70, 20) : new(20, 70);
        tool.Parameters.LineEnd = vertical ? new(70, 160) : new(160, 70);
        if (reverse) (tool.Parameters.LineStart, tool.Parameters.LineEnd) = (tool.Parameters.LineEnd, tool.Parameters.LineStart);
        tool.Parameters.Transition = reverse ? EdgeTransition.Negative : EdgeTransition.Positive;
        using var result = tool.Run(VisionImage.FromBuffer(size, size, PixelFormat.Mono8, pixels));
        Assert.True(result.Success, result.Message);
        Assert.Equal(tool.Parameters.CaliperCount, result.EdgePointCount);
        foreach (var point in new[] { result.Start, result.End })
            Assert.InRange(Math.Abs((vertical ? point.X : point.Y) - 72.35), 0, 0.2);
        var start = vertical ? result.Start.Y : result.Start.X;
        var end = vertical ? result.End.Y : result.End.X;
        Assert.True(reverse ? start > end : start < end);
        Assert.Equal(original, pixels);
        tool.Parameters.Transition = reverse ? EdgeTransition.Positive : EdgeTransition.Negative;
        using var wrongPolarity = tool.Run(VisionImage.FromBuffer(size, size, PixelFormat.Mono8, pixels));
        Assert.False(wrongPolarity.Success);
    }

    [Theory]
    [InlineData(0.2)]
    [InlineData(-0.35)]
    public void RotatedCalipers_ProjectAcrossWidth_AndMeasureAlongTheDisplayedNormal(double slope)
    {
        using var tool = Tool();
        var p = tool.Parameters;
        p.LineStart = new(35, 100); p.LineEnd = new(145, 100 + slope * 110);
        p.Transition = EdgeTransition.Positive; p.CaliperHalfWidth = 4.5;
        var normal = LineSearchGeometry.Normal(p);
        var pixels = new byte[180 * 180];
        for (var y = 0; y < 180; y++) for (var x = 0; x < 180; x++)
        {
            var distance = (x - p.LineStart.X) * normal.X + (y - p.LineStart.Y) * normal.Y - 2.35;
            pixels[y * 180 + x] = (byte)Math.Round(127.5 * (1 + Math.Tanh(distance / 1.2)));
        }
        using var result = tool.Run(VisionImage.FromBuffer(180, 180, PixelFormat.Mono8, pixels));
        Assert.True(result.Success, result.Message);
        Assert.Equal(p.CaliperCount, result.EdgePointCount);
        foreach (var point in new[] { result.Start, result.End })
            Assert.InRange(Math.Abs((point.X - p.LineStart.X) * normal.X + (point.Y - p.LineStart.Y) * normal.Y - 2.35), 0, 0.25);
    }

    [Theory]
    [InlineData(EdgeTransition.All, CaliperEdgeSelection.First, 79.5)]
    [InlineData(EdgeTransition.All, CaliperEdgeSelection.Last, 59.5)]
    [InlineData(EdgeTransition.Positive, CaliperEdgeSelection.Last, 79.5)]
    [InlineData(EdgeTransition.Negative, CaliperEdgeSelection.First, 59.5)]
    public void EveryCaliper_SelectsFirstOrLastInMeasurementDirection(EdgeTransition transition, CaliperEdgeSelection selection, double expectedY)
    {
        var pixels = new byte[180 * 180];
        for (var y = 60; y < 80; y++) for (var x = 0; x < 180; x++) pixels[y * 180 + x] = 255;
        using var tool = Tool();
        tool.Parameters.Transition = transition; tool.Parameters.EdgeSelection = selection;
        using var result = tool.Run(VisionImage.FromBuffer(180, 180, PixelFormat.Mono8, pixels));
        Assert.True(result.Success, result.Message);
        Assert.Equal(tool.Parameters.CaliperCount, result.EdgePointCount);
        Assert.InRange(Math.Abs(result.Start.Y - expectedY), 0, 0.1);
        Assert.InRange(Math.Abs(result.End.Y - expectedY), 0, 0.1);
        tool.Parameters.EdgeSelection = CaliperEdgeSelection.All;
        tool.Parameters.Transition = EdgeTransition.All;
        using var all = tool.Run(VisionImage.FromBuffer(180, 180, PixelFormat.Mono8, pixels));
        Assert.True(all.Success, all.Message);
        Assert.Equal(2 * tool.Parameters.CaliperCount, all.EdgePointCount);
    }

    [Fact]
    public void EdgesBetweenCalipers_AreNotMeasuredAsContinuousBand()
    {
        var pixels = new byte[180 * 180];
        for (var y = 65; y < 75; y++) for (var x = 50; x < 60; x++) pixels[y * 180 + x] = 255;
        using var tool = Tool(); tool.Parameters.CaliperCount = 3;
        using var calipers = tool.Run(VisionImage.FromBuffer(180, 180, PixelFormat.Mono8, pixels));
        Assert.False(calipers.Success);
        tool.Parameters.SearchMode = LineSearchKind.EdgeBand; tool.Parameters.BandHalfWidth = 25; tool.Parameters.MinEdgePoints = 2;
        using var band = tool.Run(VisionImage.FromBuffer(180, 180, PixelFormat.Mono8, pixels));
        Assert.True(band.Success, band.Message);
    }

    [Fact]
    public void ClippingAndImageBounds_RequireTwoDistinctValidCalipers_AndDoNotCreateBorderEdges()
    {
        var pixels = new byte[180 * 180];
        for (var y = 0; y < 73; y++) for (var x = 0; x < 180; x++) pixels[y * 180 + x] = 255;
        var image = VisionImage.FromBuffer(180, 180, PixelFormat.Mono8, pixels);
        using var tool = Tool();
        tool.Parameters.IgnoreEndPoints = 1;
        using var clipped = tool.Run(image);
        Assert.True(clipped.Success, clipped.Message);
        Assert.Equal(tool.Parameters.CaliperCount - 2, clipped.EdgePointCount);
        Assert.True(clipped.Start.X > tool.Parameters.LineStart.X);
        Assert.True(clipped.End.X < tool.Parameters.LineEnd.X);
        tool.Parameters.IgnoreEndPoints = 0;
        tool.Parameters.LineStart = new(-20, 70); tool.Parameters.CaliperCount = 3;
        using var partial = tool.Run(image); Assert.True(partial.Success, partial.Message); Assert.Equal(2, partial.EdgePointCount);
        tool.Parameters.CaliperCount = 2;
        using var single = tool.Run(image); Assert.False(single.Success);
        using var blank = tool.Run(VisionImage.FromBuffer(180, 180, PixelFormat.Mono8, new byte[180 * 180]));
        Assert.False(blank.Success);
    }

    [Fact]
    public void InvalidCaliperSettings_AreRejectedBeforeNativeCalls()
    {
        using var tool = Tool();
        tool.Parameters.Sigma = double.NaN;
        Assert.Contains(tool.Validate().Issues, issue => issue.Code == "SIGMA");
        tool.Parameters.Sigma = 1; tool.Parameters.CaliperCount = 1;
        Assert.Contains(tool.Validate().Issues, issue => issue.Code == "CALIPER_COUNT");
        tool.Parameters.CaliperCount = 10000; tool.Parameters.CaliperHalfWidth = 10000;
        Assert.Contains(tool.Validate().Issues, issue => issue.Code == "CALIPER_WORK");
        tool.Parameters.CaliperHalfWidth = 2; tool.Parameters.CaliperCount = 9; tool.Parameters.Transition = (EdgeTransition)999;
        Assert.Contains(tool.Validate().Issues, issue => issue.Code == "TRANSITION");
    }

    private static FindLineTool Tool()
    {
        var tool = new FindLineTool();
        var p = tool.Parameters;
        p.LineStart = new(20, 70); p.LineEnd = new(160, 70);
        p.CaliperHalfLength = 25; p.CaliperHalfWidth = 2; p.CaliperCount = 11;
        p.Threshold = 15; p.EdgeSelection = CaliperEdgeSelection.First;
        return tool;
    }
}
