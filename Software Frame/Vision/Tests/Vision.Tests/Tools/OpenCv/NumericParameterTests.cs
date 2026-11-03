using Vision.Core.Imaging;
using Vision.Core.Results;
using Vision.Core.Tools;
using Vision.Tools.OpenCv.Inspection;
using Vision.Tools.OpenCv.Processing;
using Xunit;

namespace Vision.Tests.Tools.OpenCv;

[Trait("Category", "Unit")]
public sealed class NumericParameterTests
{
    [Fact]
    public void RealStatistics_ExposeOnlyDeclaredFields_KeepUnitsAndValidZeroSeparateFromMissing()
    {
        using var tool = new GrayStatisticsTool();
        using var result = tool.Run(VisionImage.FromBuffer(2, 2, PixelFormat.Mono8, [0, 80, 80, 120]));
        Assert.True(result.Success);
        Assert.True(result.TryGetNumericField("Minimum", out var zero)); Assert.Equal(0, zero!.Value); Assert.Equal(NumericUnit.Gray8, zero.Unit);
        Assert.True(result.TryGetNumericField("Maximum", out var maximum)); Assert.Equal(120, maximum!.Value);
        Assert.True(result.TryGetNumericField("Mean", out var mean)); Assert.Equal(70, mean!.Value);
        Assert.True(result.TryGetNumericField("Median", out var median)); Assert.Equal(80, median!.Value);
        Assert.False(result.TryGetNumericField("Histogram[0]", out _));
        Assert.False(new GrayStatisticsResult().TryGetNumericField("Minimum", out _));
        using var sixteen = tool.Run(VisionImage.FromBuffer(1, 1, PixelFormat.Mono16, [0, 128]));
        Assert.True(sixteen.TryGetNumericField("Minimum", out var raw)); Assert.Equal(32768, raw!.Value); Assert.Equal(NumericUnit.Gray16, raw.Unit);
    }

    [Fact]
    public void ThresholdNumericSnapshot_UsesZeroAndPaddedInput_WithoutWritingRecipeOrBorrowingLiveObjects()
    {
        var image = VisionImage.FromBuffer(2, 2, PixelFormat.Mono8, [0, 80, 99, 80, 120, 99], 3);
        using var statistics = new GrayStatisticsTool(); using var measured = statistics.Run(image);
        Assert.True(measured.TryGetNumericField("Minimum", out var number));
        var input = new NumericParameterValue("MinGray", number! with { ImageIdentity = VisionToolConfiguration.ImageIdentity(image) });
        var recipe = new ThresholdParameters { MinGray = 99, MaxGray = 255 };
        using var tool = new ThresholdTool { Parameters = recipe };
        var execute = tool.PrepareRun(image, input);
        using var ordinary = tool.Run(VisionImageSnapshot.Copy(image)); Assert.True(ordinary.Success, ordinary.Message); Assert.Equal(1, ordinary.WhitePixelCount); Assert.Equal(99, ordinary.AppliedMinGray);
        Assert.Equal(99, recipe.MinGray);
        image.Pixels.AsSpan().Clear(); recipe.MinGray = 200; tool.Dispose();
        using var bound = Assert.IsType<ThresholdResult>(execute());
        Assert.True(bound.Success, bound.Message); Assert.Equal(4, bound.WhitePixelCount); Assert.Equal(0, bound.AppliedMinGray);
        Assert.Equal(255, bound.AppliedMaxGray); Assert.Equal(200, recipe.MinGray);
    }

    [Theory]
    [InlineData(double.NaN, NumericUnit.Gray8, "same", "有限")]
    [InlineData(double.PositiveInfinity, NumericUnit.Gray8, "same", "有限")]
    [InlineData(70.25, NumericUnit.Gray8, "same", "整数")]
    [InlineData(0, NumericUnit.Gray16, "same", "单位")]
    [InlineData(0, NumericUnit.Unknown, "same", "单位")]
    [InlineData(0, NumericUnit.Gray8, "previous-frame", "同一帧")]
    [InlineData(0, NumericUnit.Gray8, null, "同一帧")]
    public void InvalidNumbersUnitsAndFrameIdentity_AreRejected(double value, NumericUnit unit, string? identity, string message)
    {
        var definition = new NumericParameterDefinition("MinGray", "最小灰度", NumericUnit.Gray8, true);
        Assert.Contains(message, Assert.Throws<InvalidOperationException>(() => definition.Validate(new(value, unit, identity), "same")).Message);
    }

    [Fact]
    public void FractionalMean_IsNotTruncated_AndIncompatibleIntervalFailsWithoutChangingRecipe()
    {
        var image = VisionImage.FromBuffer(2, 2, PixelFormat.Mono8, [0, 80, 81, 120]);
        using var stats = new GrayStatisticsTool(); using var measured = stats.Run(image);
        measured.TryGetNumericField("Mean", out var mean);
        using var tool = new ThresholdTool { Parameters = new() { MinGray = 99 } };
        var identity = VisionToolConfiguration.ImageIdentity(image);
        Assert.Throws<InvalidOperationException>(() => tool.PrepareRun(image, new("MinGray", mean! with { ImageIdentity = identity })));
        Assert.Throws<InvalidOperationException>(() => tool.PrepareRun(image, new("Gain", new(0, NumericUnit.Gray8, identity))));
        using var invalid = tool.PrepareRun(image, new("MaxGray", new(0, NumericUnit.Gray8, identity)))();
        Assert.False(invalid.Success); Assert.Null(((IOutputImageResult)invalid).OutputImage);
        Assert.Equal(99, tool.Parameters.MinGray); Assert.Equal(255, tool.Parameters.MaxGray);
        using var upperTool = new ThresholdTool();
        using var upper = Assert.IsType<ThresholdResult>(upperTool.PrepareRun(image, new("MaxGray", new(80, NumericUnit.Gray8, identity)))());
        Assert.True(upper.Success, upper.Message); Assert.Equal(80, upper.AppliedMaxGray); Assert.Equal(2, upper.WhitePixelCount);
        Assert.Equal(255, upperTool.Parameters.MaxGray);
    }
}
