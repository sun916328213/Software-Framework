using Xunit;
using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Results;
using Vision.Core.Tools;
using Vision.Tools.Halcon.Measurement;
using FindCircleTool = Vision.Tools.OpenCv.Measurement.FindCircleTool;
using FindCircleResult = Vision.Tools.OpenCv.Measurement.FindCircleResult;

namespace Vision.Tests.Tools.OpenCv;

[Trait("Category", "Regression")]
public sealed class CircleParameterTests
{
    [Fact]
    public void ActualHoughSelectedCircle_ProvidesTypedPixels_AndNoCircleDoesNotProduceOrigin()
    {
        var pixels = new byte[160 * 140];
        for (var y = 0; y < 140; y++)
            for (var x = 0; x < 160; x++)
                if ((x - 70) * (x - 70) + (y - 60) * (y - 60) <= 30 * 30) pixels[y * 160 + x] = 255;
        using var tool = new FindCircleTool();
        tool.Parameters.ExpectedCenter = new(70, 60); tool.Parameters.RadiusMin = 25; tool.Parameters.RadiusMax = 35;
        using var result = tool.Run(VisionImage.FromBuffer(160, 140, PixelFormat.Mono8, pixels));
        Assert.True(result.Success, result.Message);
        Assert.InRange(result.Center.X, 68, 72); Assert.InRange(result.Center.Y, 58, 62); Assert.InRange(result.Radius, 28, 32);
        Assert.True(result.TryGetCircleField("Circle", out var circle)); Assert.Equal(GeometryUnit.Pixel, circle!.Unit);
        Assert.Equal(result.Center, circle.Center); Assert.Equal(result.Radius, circle.Radius);
        Assert.False(result.TryGetCircleField("Radius", out _)); // 没有把圆隐式变成无单位标量。
        using var missing = tool.Run(VisionImage.FromBuffer(160, 140, PixelFormat.Mono8, new byte[160 * 140]));
        Assert.False(missing.Success); Assert.False(missing.TryGetCircleField("Circle", out _));
    }

    [Fact]
    public void DiameterSnapshot_IsExactMath_LeavesRecipe_AndDoesNotBorrowToolOrFrame()
    {
        var image = VisionImage.FromBuffer(1, 1, PixelFormat.Mono8, [0]);
        var tool = new GeometryTool(); tool.Parameters.Operation = GeometryOperation.CircleDiameter;
        tool.Parameters.Radius = 99; tool.Parameters.CircleCenter = new(1000, 1000);
        var input = new CircleParameterValue("Circle", new(new(0, 0), 5.25, GeometryUnit.Pixel,
            VisionToolConfiguration.ImageIdentity(image), "TaskInput"), "TaskInput");
        var run = tool.PrepareCircleRun(image, input);
        Assert.Equal(99, tool.Parameters.Radius); Assert.Equal(new Point2D(1000, 1000), tool.Parameters.CircleCenter);
        tool.Parameters.Operation = GeometryOperation.Distance;
        Assert.Throws<InvalidOperationException>(() => tool.PrepareCircleRun(image, input));
        tool.Dispose(); image.Pixels[0] = 255;
        using var result = Assert.IsType<GeometryResult>(run());
        Assert.True(result.Success, result.Message); Assert.Equal(10.5, result.Diameter);
        Assert.Equal(5.25, result.AppliedRadius); Assert.Equal(Point2D.Origin, result.AppliedCenter);
        Assert.Equal(input.Circle, result.AppliedCircle); Assert.Equal(GeometryOperation.CircleDiameter, result.Operation);
        Assert.Throws<ObjectDisposedException>(() => tool.PrepareCircleRun(image, input));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.MaxValue)]
    public void ManualDiameter_RejectsDegenerateOrUnrepresentableRadius(double radius)
    {
        using var tool = new GeometryTool();
        tool.Parameters.Operation = GeometryOperation.CircleDiameter; tool.Parameters.Radius = radius;
        using var result = tool.RunImage(null);
        Assert.False(result.Success); Assert.False(result.Validation!.IsValid);
    }

    [Fact]
    public void ManualDiameter_IsManagedMath_IgnoresUnrelatedBgr_AndKeepsLegacyEnumValues()
    {
        Assert.Equal(0, (int)GeometryOperation.Distance); Assert.Equal(1, (int)GeometryOperation.LineIntersection);
        using var tool = new GeometryTool();
        tool.Parameters.Operation = GeometryOperation.CircleDiameter; tool.Parameters.Radius = 6.75;
        using var none = tool.RunImage(null);
        using var color = tool.RunImage(VisionImage.FromBuffer(1, 1, PixelFormat.Bgr24, [1, 2, 3]));
        Assert.True(none.Success, none.Message); Assert.True(color.Success, color.Message);
        Assert.Equal(13.5, none.Diameter); Assert.Equal(13.5, color.Diameter); Assert.Null(color.AppliedCircle);
    }

    [Theory]
    [InlineData("unit")]
    [InlineData("frame")]
    [InlineData("coordinate")]
    [InlineData("missing-coordinate")]
    [InlineData("center-nan")]
    [InlineData("center-infinity")]
    [InlineData("radius-zero")]
    [InlineData("radius-negative")]
    [InlineData("radius-nan")]
    [InlineData("radius-infinity")]
    [InlineData("diameter-overflow")]
    [InlineData("parameter")]
    public void InvalidCircle_CannotUseRecipeRadiusOrDefaultOrigin(string invalid)
    {
        var image = VisionImage.FromBuffer(1, 1, PixelFormat.Mono8, [0]);
        var circle = new CircleValue(new(0, 0), 5, GeometryUnit.Pixel, VisionToolConfiguration.ImageIdentity(image), "TaskInput");
        circle = invalid switch
        {
            "unit" => circle with { Unit = GeometryUnit.Unknown },
            "frame" => circle with { ImageIdentity = "old" },
            "coordinate" => circle with { CoordinateIdentity = "Output:other-crop" },
            "missing-coordinate" => circle with { CoordinateIdentity = null },
            "center-nan" => circle with { Center = new(double.NaN, 0) },
            "center-infinity" => circle with { Center = new(0, double.PositiveInfinity) },
            "radius-zero" => circle with { Radius = 0 },
            "radius-negative" => circle with { Radius = -1 },
            "radius-nan" => circle with { Radius = double.NaN },
            "radius-infinity" => circle with { Radius = double.PositiveInfinity },
            "diameter-overflow" => circle with { Radius = double.MaxValue },
            _ => circle
        };
        using var tool = new GeometryTool(); tool.Parameters.Operation = GeometryOperation.CircleDiameter; tool.Parameters.Radius = 99;
        Assert.Throws<InvalidOperationException>(() => tool.PrepareCircleRun(image,
            new(invalid == "parameter" ? "Unknown" : "Circle", circle, "TaskInput")));
        Assert.Equal(99, tool.Parameters.Radius);
    }

    [Theory]
    [InlineData("min")]
    [InlineData("max")]
    [InlineData("canny")]
    [InlineData("accumulator")]
    [InlineData("center")]
    [InlineData("integer-overflow")]
    public void InvalidHoughParameters_AreRejectedBeforeNativeExecution(string invalid)
    {
        using var tool = new FindCircleTool();
        switch (invalid)
        {
            case "min": tool.Parameters.RadiusMin = double.NaN; break;
            case "max": tool.Parameters.RadiusMax = double.PositiveInfinity; break;
            case "canny": tool.Parameters.CannyHigh = double.NaN; break;
            case "accumulator": tool.Parameters.AccumThreshold = double.NaN; break;
            case "center": tool.Parameters.ExpectedCenter = new(double.NaN, 0); break;
            case "integer-overflow": tool.Parameters.RadiusMax = (double)int.MaxValue + 1; break;
        }
        Assert.False(tool.Validate().IsValid);
        using var result = tool.Run(VisionImage.FromBuffer(1, 1, PixelFormat.Mono8, [0]));
        Assert.False(result.Success); Assert.False(result.Validation!.IsValid);
    }

    [Fact]
    public void DefaultFailedNonPixelAndMalformedResults_DoNotAdvertiseCircle()
    {
        using var result = new FindCircleResult();
        Assert.False(result.TryGetCircleField("Circle", out _));
        result.Radius = 3; Assert.True(result.TryGetCircleField("Circle", out _)); // 圆心(0,0)本身合法。
        result.CoordinateSpace = Vision.Core.Coordinate.CoordinateSpace.Calibration;
        Assert.False(result.TryGetCircleField("Circle", out _));
        result.CoordinateSpace = Vision.Core.Coordinate.CoordinateSpace.Image; result.Center = new(double.NaN, 0);
        Assert.False(result.TryGetCircleField("Circle", out _));
        result.Center = new(0, 0); result.Success = false; Assert.False(result.TryGetCircleField("Circle", out _));
    }
}
