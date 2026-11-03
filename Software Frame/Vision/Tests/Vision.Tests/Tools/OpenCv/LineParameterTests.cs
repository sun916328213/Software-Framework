using Xunit;
using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Results;
using Vision.Core.Tools;
using Vision.Tools.Halcon.Measurement;
using FindLineResult = Vision.Tools.OpenCv.Measurement.FindLineResult;

namespace Vision.Tests.Tools.OpenCv;

[Trait("Category", "Regression")]
public sealed class LineParameterTests
{
    [Fact]
    public void ExistingGeometryMath_IsActualDistanceAndIntersection_WithoutNativeImageConversion()
    {
        using var tool = new GeometryTool();
        tool.Parameters.FirstStart = new(0, 0); tool.Parameters.FirstEnd = new(3, 4);
        // 无图/无关彩色帧都不应要求HALCON图像或许可证。
        using var none = tool.RunImage(null);
        using var color = tool.RunImage(VisionImage.FromBuffer(1, 1, PixelFormat.Bgr24, [10, 20, 30]));
        Assert.True(none.Success, none.Message); Assert.True(color.Success, color.Message);
        Assert.Equal(5, none.Distance); Assert.Equal(5, color.Distance);
        tool.Parameters.FirstEnd = new(0, 0);
        using var zero = tool.Run(null); Assert.True(zero.Success); Assert.Equal(0, zero.Distance);
        tool.Parameters.Operation = GeometryOperation.LineIntersection;
        tool.Parameters.FirstEnd = new(10, 10); tool.Parameters.SecondStart = new(0, 10); tool.Parameters.SecondEnd = new(10, 0);
        using var intersection = tool.Run(null); Assert.True(intersection.Success);
        Assert.Equal(new Point2D(5, 5), intersection.Intersection);
        tool.Parameters.SecondEnd = new(10, 20);
        using var parallel = tool.Run(null); Assert.False(parallel.Success);
    }

    [Fact]
    public void PreparedLine_UsesActualSnapshot_LeavesRecipeAndDoesNotBorrowToolOrFrame()
    {
        var image = VisionImage.FromBuffer(2, 2, PixelFormat.Mono8, [0, 0, 0, 0]);
        var tool = new GeometryTool();
        tool.Parameters.FirstStart = new(100, 100); tool.Parameters.FirstEnd = new(103, 104);
        var input = new LineParameterValue("FirstLine", new(new(20, 30), new(28, 45), GeometryUnit.Pixel,
            VisionToolConfiguration.ImageIdentity(image), "TaskInput"), "TaskInput");
        var run = tool.PrepareLineRun(image, input);
        Assert.Equal(new Point2D(100, 100), tool.Parameters.FirstStart);
        Assert.Equal(new Point2D(103, 104), tool.Parameters.FirstEnd);
        tool.Parameters.Operation = GeometryOperation.LineIntersection;
        Assert.Contains("求交", Assert.Throws<InvalidOperationException>(() => tool.PrepareLineRun(image, input)).Message);
        tool.Dispose(); image.Pixels[0] = 200;
        using var result = Assert.IsType<GeometryResult>(run());
        Assert.True(result.Success, result.Message); Assert.Equal(17, result.Distance);
        Assert.Equal(GeometryOperation.Distance, result.Operation);
        Assert.Equal(new Point2D(20, 30), result.AppliedStart); Assert.Equal(new Point2D(28, 45), result.AppliedEnd);
        Assert.Equal(input.Line, result.AppliedLine);
        Assert.Throws<ObjectDisposedException>(() => tool.PrepareLineRun(image, input));
    }

    [Theory]
    [InlineData("unit")]
    [InlineData("frame")]
    [InlineData("coordinate")]
    [InlineData("missing-coordinate")]
    [InlineData("nan")]
    [InlineData("infinity")]
    [InlineData("degenerate")]
    [InlineData("parameter")]
    public void InvalidGeometry_CannotBecomeDefaultLineOrScalarZero(string invalid)
    {
        var image = VisionImage.FromBuffer(1, 1, PixelFormat.Mono8, [0]);
        var line = new LineValue(new(0, 0), new(3, 4), GeometryUnit.Pixel, VisionToolConfiguration.ImageIdentity(image), "TaskInput");
        line = invalid switch
        {
            "unit" => line with { Unit = GeometryUnit.Unknown },
            "frame" => line with { ImageIdentity = "old" },
            "coordinate" => line with { CoordinateIdentity = "Output:other-crop" },
            "missing-coordinate" => line with { CoordinateIdentity = null },
            "nan" => line with { Start = new(double.NaN, 0) },
            "infinity" => line with { End = new(double.PositiveInfinity, 0) },
            "degenerate" => line with { End = line.Start },
            _ => line
        };
        using var tool = new GeometryTool();
        Assert.Throws<InvalidOperationException>(() => tool.PrepareLineRun(image,
            new(invalid == "parameter" ? "Unknown" : "FirstLine", line, "TaskInput")));
        Assert.Equal(Point2D.Origin, tool.Parameters.FirstStart);
    }

    [Fact]
    public void LineResult_OnlyAdvertisesDeclaredPixelLine_NotWorldOrMissingEdges()
    {
        using var result = new FindLineResult { Start = new(1, 2), End = new(4, 6), EdgePointCount = 5 };
        Assert.True(result.TryGetLineField("Line", out var line)); Assert.Equal(GeometryUnit.Pixel, line!.Unit);
        Assert.False(result.TryGetLineField("Start", out _));
        result.CoordinateSpace = Vision.Core.Coordinate.CoordinateSpace.Calibration;
        Assert.False(result.TryGetLineField("Line", out _));
        result.CoordinateSpace = Vision.Core.Coordinate.CoordinateSpace.Image; result.EdgePointCount = 0;
        Assert.False(result.TryGetLineField("Line", out _));
        result.EdgePointCount = 5; result.Success = false;
        Assert.False(result.TryGetLineField("Line", out _));
    }
}
