using Xunit;
using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Results;
using Vision.Core.Tools;
using Vision.Tools.Halcon.Measurement;
using CvCircle = Vision.Tools.OpenCv.Measurement.FindCircleTool;
using CvLine = Vision.Tools.OpenCv.Measurement.FindLineTool;
using CvCircleResult = Vision.Tools.OpenCv.Measurement.FindCircleResult;
using CvLineResult = Vision.Tools.OpenCv.Measurement.FindLineResult;

namespace Vision.Tests.Tools.OpenCv;

[Trait("Category", "Regression")]
public sealed class PointParameterTests
{
    [Fact]
    public void FinitePointsWhoseDistanceOverflows_DoNotReturnSuccessfulInfinity()
    {
        using var tool = new GeometryTool();
        tool.Parameters.FirstStart = new(-double.MaxValue, 0); tool.Parameters.FirstEnd = new(double.MaxValue, 0);
        using var result = tool.RunImage(null);
        Assert.False(result.Success); Assert.False(result.Validation!.IsValid);
    }

    [Fact]
    public void PointSnapshot_PreservesRecipeReferenceAndFirstStart_AfterOriginalToolAndFrameChange()
    {
        var image = VisionImage.FromBuffer(1, 1, PixelFormat.Mono8, [0]);
        var tool = new GeometryTool(); tool.Parameters.FirstStart = new(99, 99); tool.Parameters.FirstEnd = new(0, 0);
        var input = new PointParameterValue("FirstStart", new(new(3, 4), GeometryUnit.Pixel,
            VisionToolConfiguration.ImageIdentity(image), "TaskInput"), "TaskInput");
        var run = tool.PreparePointRun(image, input);
        Assert.Equal(new Point2D(99, 99), tool.Parameters.FirstStart); Assert.Equal(Point2D.Origin, tool.Parameters.FirstEnd);
        tool.Parameters.FirstEnd = new(1000, 1000); tool.Parameters.Operation = GeometryOperation.CircleDiameter;
        Assert.Throws<InvalidOperationException>(() => tool.PreparePointRun(image, input));
        tool.Dispose(); image.Pixels[0] = 255;
        using var result = Assert.IsType<GeometryResult>(run());
        Assert.True(result.Success, result.Message); Assert.Equal(5, result.Distance);
        Assert.Equal(new Point2D(3, 4), result.AppliedStart); Assert.Equal(Point2D.Origin, result.AppliedEnd);
        Assert.Equal(input.Point, result.AppliedPoint); Assert.Equal(GeometryOperation.Distance, result.Operation);
        Assert.Throws<ObjectDisposedException>(() => tool.PreparePointRun(image, input));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(31.5, -7.25)]
    public void OriginAndEqualPoints_AreValidZeroDistance_NotMissing(double x, double y)
    {
        var image = VisionImage.FromBuffer(1, 1, PixelFormat.Mono8, [0]);
        using var tool = new GeometryTool(); tool.Parameters.FirstEnd = new(x, y);
        var run = tool.PreparePointRun(image, new("FirstStart",
            new(new(x, y), GeometryUnit.Pixel, VisionToolConfiguration.ImageIdentity(image), "TaskInput"), "TaskInput"));
        using var result = Assert.IsType<GeometryResult>(run());
        Assert.True(result.Success, result.Message); Assert.Equal(0, result.Distance); Assert.NotNull(result.AppliedPoint);
        Assert.Equal(new Point2D(x, y), result.AppliedStart); Assert.Equal(new Point2D(x, y), result.AppliedEnd);
    }

    [Theory]
    [InlineData("unit")]
    [InlineData("frame")]
    [InlineData("coordinate")]
    [InlineData("missing-coordinate")]
    [InlineData("nan")]
    [InlineData("infinity")]
    [InlineData("parameter")]
    public void InvalidPoint_CannotBecomeOriginOrRecipeStart(string invalid)
    {
        var image = VisionImage.FromBuffer(1, 1, PixelFormat.Mono8, [0]);
        var point = new PointValue(new(0, 0), GeometryUnit.Pixel, VisionToolConfiguration.ImageIdentity(image), "TaskInput");
        point = invalid switch
        {
            "unit" => point with { Unit = GeometryUnit.Unknown },
            "frame" => point with { ImageIdentity = "old" },
            "coordinate" => point with { CoordinateIdentity = "Output:other-crop" },
            "missing-coordinate" => point with { CoordinateIdentity = null },
            "nan" => point with { Position = new(double.NaN, 0) },
            "infinity" => point with { Position = new(0, double.PositiveInfinity) },
            _ => point
        };
        using var tool = new GeometryTool(); tool.Parameters.FirstStart = new(99, 99);
        Assert.Throws<InvalidOperationException>(() => tool.PreparePointRun(image,
            new(invalid == "parameter" ? "Unknown" : "FirstStart", point, "TaskInput")));
        Assert.Equal(new Point2D(99, 99), tool.Parameters.FirstStart);
    }

    [Fact]
    public void InvalidRecipeReference_FailsInsteadOfChangingItOrPublishingInfinity()
    {
        var image = VisionImage.FromBuffer(1, 1, PixelFormat.Mono8, [0]);
        using var tool = new GeometryTool(); tool.Parameters.FirstEnd = new(double.NaN, 0);
        var run = tool.PreparePointRun(image, new("FirstStart", new(new(3, 4), GeometryUnit.Pixel,
            VisionToolConfiguration.ImageIdentity(image), "TaskInput"), "TaskInput"));
        using var result = run();
        Assert.False(result.Success); Assert.True(double.IsNaN(tool.Parameters.FirstEnd.X));
    }

    [Theory]
    [InlineData("Center")]
    [InlineData("Start")]
    [InlineData("End")]
    public void ActualAlgorithms_ExposeOnlySelectedPixelPoint(string field)
    {
        var pixels = new byte[220 * 160];
        if (field == "Center")
        {
            for (var y = 0; y < 160; y++)
                for (var x = 0; x < 220; x++)
                    if ((x - 80) * (x - 80) + (y - 70) * (y - 70) <= 30 * 30) pixels[y * 220 + x] = 255;
            using var tool = new CvCircle(); tool.Parameters.ExpectedCenter = new(80, 70);
            tool.Parameters.RadiusMin = 25; tool.Parameters.RadiusMax = 35;
            using var result = tool.Run(VisionImage.FromBuffer(220, 160, PixelFormat.Mono8, pixels));
            Assert.True(result.Success, result.Message); Assert.True(result.TryGetPointField(field, out var point));
            Assert.InRange(point!.Position.X, 78, 82); Assert.InRange(point.Position.Y, 68, 72);
            Assert.Equal(result.Center, point.Position); Assert.False(result.TryGetPointField("Radius", out _));
        }
        else
        {
            for (var y = 80; y <= 120; y++) for (var x = 0; x < 220; x++) pixels[y * 220 + x] = 255;
            using var tool = new CvLine(); var p = tool.Parameters;
            p.LineStart = new(20, 80); p.LineEnd = new(180, 80); p.CaliperHalfLength = 12; p.CaliperHalfWidth = 2;
            p.Transition = Vision.Tools.OpenCv.Measurement.EdgeTransition.Negative;
            p.EdgeSelection = Vision.Tools.OpenCv.Measurement.CaliperEdgeSelection.First;
            using var result = tool.Run(VisionImage.FromBuffer(220, 160, PixelFormat.Mono8, pixels));
            Assert.True(result.Success, result.Message); Assert.True(result.TryGetPointField(field, out var point));
            Assert.InRange(point!.Position.X, field == "Start" ? 19.9 : 179.9, field == "Start" ? 20.1 : 180.1);
            Assert.InRange(point.Position.Y, 79.25, 79.75);
            Assert.Equal(field == "Start" ? result.Start : result.End, point.Position);
            Assert.False(result.TryGetPointField("Center", out _));
        }
    }

    [Fact]
    public void DefaultFailedNonPixelAndMalformedResults_DoNotAdvertisePoint_OriginCanBeReal()
    {
        using var circle = new CvCircleResult();
        Assert.False(circle.TryGetPointField("Center", out _));
        circle.Radius = 3; Assert.True(circle.TryGetPointField("Center", out var zero)); Assert.Equal(Point2D.Origin, zero!.Position);
        circle.CoordinateSpace = Vision.Core.Coordinate.CoordinateSpace.Calibration;
        Assert.False(circle.TryGetPointField("Center", out _));
        using var line = new CvLineResult { EdgePointCount = 2, Start = new(0, 0), End = new(3, 4) };
        Assert.True(line.TryGetPointField("Start", out var start)); Assert.Equal(Point2D.Origin, start!.Position);
        line.EdgePointCount = 0; Assert.False(line.TryGetPointField("Start", out _));
        line.EdgePointCount = 2; line.Start = new(double.NaN, 0); Assert.False(line.TryGetPointField("Start", out _));
        line.Start = new(0, 0); line.Success = false; Assert.False(line.TryGetPointField("Start", out _));
    }
}
