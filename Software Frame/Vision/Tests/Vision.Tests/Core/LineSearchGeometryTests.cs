using System.Text.Json;
using Vision.Core.Geometry;
using Xunit;

namespace Vision.Tests.Core;

public sealed class LineSearchGeometryTests
{
    [Fact]
    public void NewOpenCvTool_UsesRealCaliperGeometryMatchingHalconDefaults()
    {
        using var tool = new Vision.Tools.OpenCv.Measurement.FindLineTool();
        ILineSearchRegion actual = tool.Parameters;
        ILineSearchRegion expected = new Vision.Tools.Halcon.Measurement.FindLineParameters();
        Assert.Equal(LineSearchKind.Calipers, actual.SearchKind);
        Assert.Equal(expected.SampleCount, actual.SampleCount);
        Assert.Equal(expected.NormalHalfLength, actual.NormalHalfLength);
        Assert.Equal(expected.TangentHalfWidth, actual.TangentHalfWidth);
        Assert.Equal(LineSearchGeometry.EditingFrame(expected).Size, LineSearchGeometry.EditingFrame(actual).Size);
        Assert.Equal(expected.SampleCount, LineSearchGeometry.Rectangles(actual).Count);
    }
    [Fact]
    public void Calipers_UseMeasurementNormal_AndIncludeBothEndpoints()
    {
        var parameters = new Vision.Tools.Halcon.Measurement.FindLineParameters
        { LineStart = new(10, 20), LineEnd = new(30, 20), CaliperCount = 3, CaliperHalfLength = 8, CaliperHalfWidth = 2 };
        ILineSearchRegion region = parameters;
        Assert.Equal(new Point2D(0, -1), LineSearchGeometry.Normal(region));
        var rectangles = LineSearchGeometry.Rectangles(region);
        Assert.Equal(3, rectangles.Count);
        Assert.Equal(new[] { 10.0, 20.0, 30.0 }, rectangles.Select(r => r.Pose.X));
        Assert.All(rectangles, r => { Assert.Equal(20, r.Pose.Y); Assert.Equal(16, r.Size.Width); Assert.Equal(4, r.Size.Height); Assert.Equal(-90, r.Pose.AngleDeg, 8); });
    }
    [Fact]
    public void CannyBand_IsOneRectangle_NotFakeCalipers_AndDoesNotChangeJson()
    {
        var parameters = new Vision.Tools.OpenCv.Measurement.FindLineParameters
        { LineStart = new(10, 20), LineEnd = new(30, 20), BandHalfWidth = 4 };
        ILineSearchRegion region = parameters;
        Assert.Equal(LineSearchKind.EdgeBand, region.SearchKind);
        var rectangle = Assert.Single(LineSearchGeometry.Rectangles(region));
        Assert.Equal(20, rectangle.Pose.X); Assert.Equal(20, rectangle.Pose.Y);
        Assert.Equal(20, rectangle.Size.Width); Assert.Equal(8, rectangle.Size.Height);
        Assert.Equal(0, rectangle.Pose.AngleDeg);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(parameters));
        Assert.DoesNotContain(json.RootElement.EnumerateObject(), p => p.Name is "SearchKind" or "NormalHalfLength" or "TangentHalfWidth" or "SampleCount");
        Assert.Equal(4, json.RootElement.GetProperty("BandHalfWidth").GetDouble());
    }
    [Fact]
    public void SharedEditingFrame_PreservesRealCaliperExtent_AndDoesNotReplaceSamplingRegions()
    {
        var parameters = new Vision.Tools.Halcon.Measurement.FindLineParameters
        { LineStart = new(10, 20), LineEnd = new(30, 20), CaliperCount = 3, CaliperHalfLength = 8, CaliperHalfWidth = 2 };
        var frame = LineSearchGeometry.EditingFrame(parameters);
        Assert.Equal(20, frame.Pose.X); Assert.Equal(20, frame.Pose.Y);
        Assert.Equal(24, frame.Size.Width); Assert.Equal(16, frame.Size.Height); Assert.Equal(0, frame.Pose.AngleDeg);
        Assert.Equal(3, LineSearchGeometry.Rectangles(parameters).Count);
        // 纵向路径：半宽沿列方向，端点采样宽度沿行方向，不能混为轴对齐框。
        parameters.LineEnd = new(10, 40);
        frame = LineSearchGeometry.EditingFrame(parameters);
        Assert.Equal(10, frame.Pose.X); Assert.Equal(30, frame.Pose.Y);
        Assert.Equal(24, frame.Size.Width); Assert.Equal(16, frame.Size.Height); Assert.Equal(90, frame.Pose.AngleDeg);
    }
    [Fact]
    public void SharedEditingFrame_ForCannyIsTheExistingBand_WithoutInventingCalipers()
    {
        var parameters = new Vision.Tools.OpenCv.Measurement.FindLineParameters
        { LineStart = new(10, 20), LineEnd = new(30, 20), BandHalfWidth = 4 };
        var frame = LineSearchGeometry.EditingFrame(parameters);
        var actual = Assert.Single(LineSearchGeometry.Rectangles(parameters));
        Assert.Equal(actual.Pose, frame.Pose); Assert.Equal(actual.Size, frame.Size);
        Assert.Equal(20, frame.Size.Width); Assert.Equal(8, frame.Size.Height);
    }
    [Fact]
    public void InvalidGeometry_IsRejectedBeforeDrawingOrAllocatingCalipers()
    {
        var parameters = new Vision.Tools.Halcon.Measurement.FindLineParameters { LineStart = new(0, 0), LineEnd = new(0, 0) };
        Assert.ThrowsAny<ArgumentException>(() => LineSearchGeometry.Rectangles(parameters));
        parameters.LineEnd = new(10, 0); parameters.CaliperCount = 10001;
        Assert.ThrowsAny<ArgumentException>(() => LineSearchGeometry.Rectangles(parameters));
        parameters.CaliperCount = 3; parameters.CaliperHalfWidth = double.NaN;
        Assert.ThrowsAny<ArgumentException>(() => LineSearchGeometry.Rectangles(parameters));
        parameters.CaliperHalfWidth = 2; parameters.LineStart = new(double.PositiveInfinity, 0);
        Assert.ThrowsAny<ArgumentException>(() => LineSearchGeometry.Rectangles(parameters));
    }
}
