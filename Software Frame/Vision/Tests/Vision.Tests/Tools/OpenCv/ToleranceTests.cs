using Xunit;
using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Results;
using Vision.Core.Tools;
using Vision.Tools.Halcon.Measurement;

namespace Vision.Tests.Tools.OpenCv;

[Trait("Category", "Regression")]
public sealed class ToleranceTests
{
    [Theory]
    [InlineData(0, true)]
    [InlineData(10, true)]
    [InlineData(5.25, true)]
    [InlineData(-0.01, false)]
    [InlineData(10.01, false)]
    public void ActualClosedInterval_IncludesEndpointsAndZero_NgIsSuccessfulFalse(double value, bool expected)
    {
        var image = Image();
        using var tool = new ToleranceTool(); tool.Parameters.Maximum = 10;
        using var result = Assert.IsType<ToleranceResult>(tool.PrepareRun(image, Input(image, value))());
        Assert.True(result.Success, result.Message); Assert.Equal(expected, result.IsWithinTolerance);
        Assert.True(result.TryGetBooleanField(nameof(ToleranceResult.IsWithinTolerance), out var decision)); Assert.Equal(expected, decision);
        Assert.Equal(value, result.AppliedMeasurement!.Value); Assert.Equal(0, result.AppliedMinimum); Assert.Equal(10, result.AppliedMaximum);
    }

    [Fact]
    public void MissingInputAndUnexecutedResult_HaveNoBoolean_NotFalseOrOk()
    {
        using var tool = new ToleranceTool();
        using var missing = Assert.IsType<ToleranceResult>(tool.Run(Image()));
        Assert.False(missing.Success); Assert.Null(missing.IsWithinTolerance); Assert.False(missing.TryGetBooleanField("IsWithinTolerance", out _));
        using var empty = new ToleranceResult(); Assert.False(empty.TryGetBooleanField("IsWithinTolerance", out _));
    }

    [Fact]
    public void PreparedMeasurementAndLimits_AreIndependentOfOriginalToolAndImage()
    {
        var image = Image(); var tool = new ToleranceTool(); tool.Parameters.Maximum = 10;
        var run = tool.PrepareRun(image, Input(image, 0));
        tool.Parameters.Minimum = 50; tool.Parameters.Maximum = 100; tool.Dispose(); image.Pixels[0] = 200;
        using var result = Assert.IsType<ToleranceResult>(run());
        Assert.True(result.Success); Assert.True(result.IsWithinTolerance); Assert.Equal(0, result.AppliedMeasurement!.Value); Assert.Equal(10, result.AppliedMaximum);
        Assert.Throws<ObjectDisposedException>(() => tool.PrepareRun(image, Input(image, 0)));
    }

    [Theory]
    [InlineData("nan")]
    [InlineData("infinity")]
    [InlineData("unit")]
    [InlineData("frame")]
    [InlineData("coordinate")]
    [InlineData("missing-coordinate")]
    [InlineData("parameter")]
    [InlineData("limits")]
    public void InvalidInputOrLimits_CannotProduceVerdict(string invalid)
    {
        var image = Image(); using var tool = new ToleranceTool();
        var number = Input(image, 0).Number;
        number = invalid switch
        {
            "nan" => number with { Value = double.NaN },
            "infinity" => number with { Value = double.PositiveInfinity },
            "unit" => number with { Unit = NumericUnit.Gray8 },
            "frame" => number with { ImageIdentity = "old" },
            "coordinate" => number with { CoordinateIdentity = "other-crop" },
            "missing-coordinate" => number with { CoordinateIdentity = null },
            _ => number
        };
        if (invalid == "limits") { tool.Parameters.Minimum = 10; tool.Parameters.Maximum = 0; }
        Assert.Throws<InvalidOperationException>(() => tool.PrepareRun(image,
            new(invalid == "parameter" ? "Unknown" : "Measurement", number) { ReferenceCoordinateIdentity = "TaskInput" }));
    }

    [Theory]
    [InlineData(NumericUnit.Gray8)]
    [InlineData(NumericUnit.Gray16)]
    public void GrayUnits_OnlyAcceptExplicitMatchingDomain(NumericUnit unit)
    {
        var image = Image(); using var tool = new ToleranceTool(); tool.Parameters.Unit = unit;
        using var result = Assert.IsType<ToleranceResult>(tool.PrepareRun(image,
            new("Measurement", new(0, unit, VisionToolConfiguration.ImageIdentity(image))))());
        Assert.True(result.IsWithinTolerance);
    }

    [Fact]
    public void GeometryEmitsOnlyActualBoundPixelFields_ZeroSurvives_ManualUnknownUnitDoesNot()
    {
        var image = Image(); using var tool = new GeometryTool();
        using var manual = tool.Run(null); Assert.False(manual.TryGetNumericField("Distance", out _));
        var run = tool.PreparePointRun(image, new("FirstStart",
            new(Point2D.Origin, GeometryUnit.Pixel, VisionToolConfiguration.ImageIdentity(image), "TaskInput"), "TaskInput"));
        using var actual = Assert.IsType<GeometryResult>(run());
        Assert.True(actual.TryGetNumericField("Distance", out var number)); Assert.Equal(0, number!.Value);
        Assert.Equal(NumericUnit.Pixel, number.Unit); Assert.Equal("TaskInput", number.CoordinateIdentity);
        Assert.False(actual.TryGetNumericField("Diameter", out _));
        tool.Parameters.Operation = GeometryOperation.CircleDiameter;
        using var diameter = Assert.IsType<GeometryResult>(tool.PrepareCircleRun(image,
            new("Circle", new(new(0, 0), 3, GeometryUnit.Pixel, VisionToolConfiguration.ImageIdentity(image), "TaskInput"), "TaskInput"))());
        Assert.True(diameter.TryGetNumericField("Diameter", out var length)); Assert.Equal(6, length!.Value);
    }
    private static VisionImage Image() => VisionImage.FromBuffer(1, 1, PixelFormat.Mono8, [0]);
    private static NumericParameterValue Input(VisionImage image, double value) =>
        new("Measurement", new(value, NumericUnit.Pixel, VisionToolConfiguration.ImageIdentity(image)) { CoordinateIdentity = "TaskInput" })
        { ReferenceCoordinateIdentity = "TaskInput" };
}
