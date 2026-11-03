using System.Text.Json;
using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Presentation;
using Vision.Tools.OpenCv.Matching;
using Xunit;

namespace Vision.Tests.Tools.OpenCv;

[Trait("Category", "Regression")]
public sealed class TemplatePoseTests
{
    [Theory]
    [InlineData(90, 1, TemplateMatchMethod.CCoeffNormed)]
    [InlineData(-90, 1, TemplateMatchMethod.CCorrNormed)]
    [InlineData(180, 1, TemplateMatchMethod.SqDiffNormed)]
    [InlineData(90, 1.5, TemplateMatchMethod.CCorrNormed)]
    [InlineData(0, 2, TemplateMatchMethod.CCoeffNormed)]
    [InlineData(0, 0.5, TemplateMatchMethod.CCoeffNormed)]
    [InlineData(30, 1.5, TemplateMatchMethod.CCoeffNormed)]
    [InlineData(-30, 0.75, TemplateMatchMethod.SqDiffNormed)]
    public void Search_ReturnsActualPoseAndOriginalCenter_WithIndependentInverseRaster(double angle, double scale, TemplateMatchMethod method)
    {
        var template = Pattern();
        var (scene, center) = Scene(template, angle, scale);
        using var tool = new TemplateMatchTool();
        tool.Parameters = Parameters(template, angle - 10, 20, scale, scale);
        tool.Parameters.Method = method;
        using var result = tool.Run(scene);
        Assert.True(result.Success, result.Message);
        var pose = Assert.Single(result.Fixtures);
        Assert.InRange(pose.Pose.X, center.X - 0.6, center.X + 0.6);
        Assert.InRange(pose.Pose.Y, center.Y - 0.6, center.Y + 0.6);
        Assert.InRange(Math.Abs(Pose2D.NormalizeAngle(pose.Pose.AngleDeg - angle)), 0, 0.01);
        Assert.Equal(scale, pose.Scale, 8);
        Assert.InRange(pose.Score, 0.94, 1);
        Assert.Equal(template.Width, tool.Parameters.Template!.Width);
    }

    [Theory]
    [InlineData(17.3, TemplateMatchMethod.CCoeffNormed)]
    [InlineData(-38.2, TemplateMatchMethod.CCoeffNormed)]
    [InlineData(179.4, TemplateMatchMethod.CCoeffNormed)]
    [InlineData(17.3, TemplateMatchMethod.SqDiffNormed)]
    [InlineData(-38.2, TemplateMatchMethod.CCorrNormed)]
    public void NonGridAngles_AreLocatedAndRefined_WithoutLoweringFinalScore(double angle, TemplateMatchMethod method)
    {
        var template = Pattern(96, 64);
        var (scene, center) = Scene(template, angle, 1, 320, 240);
        using var tool = new TemplateMatchTool { Parameters = Parameters(template, -180, 360, 1, 1) };
        tool.Parameters.Method = method;
        using var result = tool.Run(scene);
        Assert.True(result.Success, result.Message);
        var match = Assert.Single(result.Matches);
        Assert.InRange(match.Center.DistanceTo(center), 0, 1);
        Assert.InRange(Math.Abs(Pose2D.NormalizeAngle(match.AngleDeg - angle)), 0, 0.25);
        Assert.InRange(match.Score, 0.94, 1);
    }

    [Theory]
    [InlineData(40, 30)]
    [InlineData(900, 500)]
    public void FullAngleSearch_OnTwoMegapixelImage_UsesCoarseLocalizationBeforeFullResolutionBudget(int originX, int originY)
    {
        var template = Pattern(96, 64);
        var (scene, center) = Scene(template, 17.3, 1, 1920, 1080, originX, originY);
        using var tool = new TemplateMatchTool { Parameters = Parameters(template, -180, 360, 1, 1) };
        using var result = tool.Run(scene);
        Assert.True(result.Success, result.Message);
        var match = Assert.Single(result.Matches);
        Assert.InRange(match.Center.DistanceTo(center), 0, 1);
        Assert.InRange(Math.Abs(Pose2D.NormalizeAngle(match.AngleDeg - 17.3)), 0, 0.25);
        Assert.InRange(match.Score, 0.94, 1);
    }

    [Fact]
    public void Refinement_DoesNotSearchOutsideConfiguredAngleRange()
    {
        var template = Pattern(48, 32);
        var (scene, _) = Scene(template, 37.3, 1);
        using var tool = new TemplateMatchTool { Parameters = Parameters(template, -10, 20, 1, 1) };
        using var result = tool.Run(scene);
        Assert.True(result.Success, result.Message);
        Assert.Empty(result.Matches);
    }

    [Theory]
    [InlineData("AngleStartDeg", "\"NaN\"")]
    [InlineData("AngleExtentDeg", "-1")]
    [InlineData("AngleStepDeg", "0")]
    [InlineData("ScaleMin", "0")]
    [InlineData("ScaleMax", "0.5")]
    [InlineData("ScaleStep", "0")]
    [InlineData("MaxOverlap", "1.1")]
    public void InvalidSearchSettings_AreRejectedBeforeNativeExecution(string field, string value)
    {
        var json = $"{{\"AngleStartDeg\":10,\"ScaleMin\":1,\"ScaleMax\":1,\"{field}\":{value}}}";
        using var tool = new TemplateMatchTool
        {
            Parameters = JsonSerializer.Deserialize<TemplateMatchParameters>(json, new JsonSerializerOptions
            {
                NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals
            })!
        };
        tool.Parameters.Template = Pattern();
        Assert.False(tool.Validate().IsValid);
    }

    [Fact]
    public void ExcessivePoseGrid_IsRejectedInsteadOfUnboundedWork()
    {
        using var tool = new TemplateMatchTool { Parameters = Parameters(Pattern(), 0, 360, 0.5, 2, 0.001) };
        Assert.False(tool.Validate().IsValid);
    }

    [Fact]
    public void MultipleAnglesAtSameObject_AreSuppressedAcrossPoseSamples()
    {
        var template = Pattern();
        var (scene, _) = Scene(template, 30, 1.5);
        using var tool = new TemplateMatchTool { Parameters = Parameters(template, 20, 20, 1.4, 1.6) };
        tool.Parameters.MaxMatches = 4;
        using var result = tool.Run(scene);
        Assert.True(result.Success, result.Message);
        Assert.Single(result.Matches);
    }

    [Fact]
    public void BackgroundPreparation_FreezesAllPixelsAndPoseParameters_AndDoesNotBorrowOriginalTool()
    {
        var template = Pattern(); var (scene, _) = Scene(template, 90, 1.5);
        var tool = new TemplateMatchTool { Parameters = Parameters(template, 80, 20, 1.5, 1.5) };
        var background = Assert.IsAssignableFrom<IBackgroundImageTool>(tool);
        var run = background.PrepareRun(scene);
        tool.Parameters = new();
        Array.Fill(template.Pixels, (byte)0); Array.Fill(scene.Pixels, (byte)0);
        tool.Dispose();
        using var result = Assert.IsType<TemplateMatchResult>(run());
        Assert.True(result.Success, result.Message);
        var pose = Assert.Single(result.Fixtures);
        Assert.Equal(90, pose.Pose.AngleDeg, 6); Assert.Equal(1.5, pose.Scale, 6);
    }

    [Fact]
    public void LegacyMissingSettings_KeepOriginalZeroAngleAndUnitScale()
    {
        using var tool = new TemplateMatchTool { Parameters = JsonSerializer.Deserialize<TemplateMatchParameters>("{}")! };
        tool.Parameters.Template = Pattern();
        var (scene, center) = Scene(tool.Parameters.Template, 0, 1);
        using var result = tool.Run(scene);
        Assert.True(result.Success, result.Message);
        var pose = Assert.Single(result.Fixtures);
        Assert.Equal(0, pose.Pose.AngleDeg); Assert.Equal(1, pose.Scale);
        Assert.Equal(center, result.Matches[0].Center);
    }

    [Fact]
    public void DifferentTargetsAtDifferentAngles_AreNotMergedByGlobalSuppression()
    {
        var template = Pattern(); var (first, _) = Scene(template, 90, 1); var (second, _) = Scene(template, -90, 1);
        for (var y = 0; y < second.Height - 20; y++)
            for (var x = 0; x < second.Width - 70; x++)
                if (second.Pixels[y * second.Width + x] != 47)
                    first.Pixels[(y + 20) * first.Width + x + 70] = second.Pixels[y * second.Width + x];
        using var tool = new TemplateMatchTool { Parameters = Parameters(template, -90, 180, 1, 1, 90) };
        tool.Parameters.MaxMatches = 2;
        using var result = tool.Run(first);
        Assert.True(result.Success, result.Message); Assert.Equal(2, result.Matches.Count);
        Assert.Equal(new[] { -90d, 90d }, result.Matches.Select(match => match.AngleDeg).Order().ToArray());
    }

    [Fact]
    public void SearchAcrossAngleBoundary_ReportsCanonicalEquivalentAngle()
    {
        var template = Pattern(); var (scene, _) = Scene(template, -175, 1);
        using var tool = new TemplateMatchTool { Parameters = Parameters(template, 170, 30, 1, 1) };
        using var result = tool.Run(scene);
        Assert.True(result.Success, result.Message);
        Assert.Equal(-175, Assert.Single(result.Fixtures).Pose.AngleDeg, 6);
    }

    [Fact]
    public void Downscaling_CanFindTemplateWhoseOriginalSizeExceedsSearchImage()
    {
        var template = Pattern(); var (scene, _) = Scene(template, 0, 0.5);
        var small = new byte[16 * 12];
        for (var y = 0; y < 12; y++) Array.Copy(scene.Pixels, (y + 28) * scene.Width + 38, small, y * 16, 16);
        using var tool = new TemplateMatchTool { Parameters = Parameters(template, 0, 0, 0.5, 0.5) };
        using var result = tool.Run(VisionImage.FromBuffer(16, 12, PixelFormat.Mono8, small));
        Assert.True(result.Success, result.Message);
        Assert.Equal(new Point2D(8, 6), Assert.Single(result.Matches).Center);
    }

    [Fact]
    public void UniformTemplateIsRejected_AndUniformSearchDoesNotPublishNonFinitePeaks()
    {
        var template = Pattern();
        using var tool = new TemplateMatchTool { Parameters = Parameters(template, 0, 10, 1, 1) };
        using var empty = tool.Run(VisionImage.FromBuffer(60, 50, PixelFormat.Mono8, new byte[3000]));
        Assert.True(empty.Success, empty.Message); Assert.Empty(empty.Matches);
        Array.Fill(template.Pixels, (byte)0);
        using var invalid = tool.Run(VisionImage.FromBuffer(60, 50, PixelFormat.Mono8, new byte[3000]));
        Assert.False(invalid.Success); Assert.Empty(invalid.Fixtures);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void SameObject_IsNotDuplicatedAtExtremeOverlapSettings(double overlap)
    {
        var template = Pattern(); var (scene, _) = Scene(template, 30, 1.5);
        using var tool = new TemplateMatchTool { Parameters = Parameters(template, 20, 20, 1.5, 1.5) };
        tool.Parameters.MaxMatches = 4; tool.Parameters.MaxOverlap = overlap;
        using var result = tool.Run(scene);
        Assert.True(result.Success, result.Message);
        Assert.Single(result.Matches);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    public void CrowdedPoseResponse_FailsExplicitlyInsteadOfSilentlyDiscardingCandidates(double extent)
    {
        var template = Pattern(); var (scene, _) = Scene(template, 90, 1);
        var pixels = Enumerable.Repeat((byte)47, 500 * 120).ToArray();
        for (var copy = 0; copy < 5; copy++)
            for (var y = 0; y < scene.Height; y++)
                for (var x = 30; x < 70; x++) pixels[y * 500 + x + copy * 90] = scene.Pixels[y * scene.Width + x];
        using var tool = new TemplateMatchTool { Parameters = Parameters(template, 90, extent, 1, 1) };
        tool.Parameters.MinScore = 0.99; tool.Parameters.MaxMatches = 1;
        using var result = tool.Run(VisionImage.FromBuffer(500, 120, PixelFormat.Mono8, pixels));
        Assert.False(result.Success); Assert.Contains("候选数量", result.Message); Assert.Empty(result.Matches);
    }

    [Fact]
    public void ResponseBudgetIsCheckedBeforeNativeSearch()
    {
        using var tool = new TemplateMatchTool { Parameters = Parameters(Pattern(), 0, 180, 1, 1, 1) };
        using var result = tool.Run(VisionImage.FromBuffer(1500, 1000, PixelFormat.Mono8, new byte[1_500_000]));
        Assert.False(result.Success); Assert.Contains("预算", result.Message); Assert.Empty(result.Matches);
    }

    internal static TemplateMatchParameters Parameters(VisionImage template, double start, double extent, double min, double max, double step = 5)
    {
        // JSON makes the red test exercise the current algorithm, not fail to compile on absent fields.
        var json = FormattableString.Invariant($"{{\"AngleStartDeg\":{start},\"AngleExtentDeg\":{extent},\"AngleStepDeg\":{step},\"ScaleMin\":{min},\"ScaleMax\":{max},\"ScaleStep\":0.1,\"MaxOverlap\":0.3}}");
        var p = JsonSerializer.Deserialize<TemplateMatchParameters>(json)!;
        p.Template = template; p.MinScore = 0.94;
        return p;
    }

    internal static VisionImage Pattern(int width = 24, int height = 16)
    {
        // Non-symmetric fixed arithmetic texture, independent of OpenCV or the matcher.
        var pixels = new byte[width * height];
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++) pixels[y * width + x] = (byte)(30 + (x * 31 + y * 17 + x * y * 7) % 190);
        return VisionImage.FromBuffer(width, height, PixelFormat.Mono8, pixels);
    }

    internal static (VisionImage Image, Point2D Center) Scene(VisionImage template, double angle, double scale, int width = 160, int height = 120, int originX = 40, int originY = 30)
    {
        var radians = angle * Math.PI / 180;
        var c = Math.Cos(radians); var s = Math.Sin(radians);
        if (Math.Abs(c) < 1e-12) c = 0;
        if (Math.Abs(s) < 1e-12) s = 0;
        var center = new Point2D(originX + Math.Ceiling(scale * (Math.Abs(c) * template.Width + Math.Abs(s) * template.Height)) / 2,
            originY + Math.Ceiling(scale * (Math.Abs(s) * template.Width + Math.Abs(c) * template.Height)) / 2);
        var pixels = Enumerable.Repeat((byte)47, width * height).ToArray();
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var dx = x + 0.5 - center.X; var dy = y + 0.5 - center.Y;
                var u = (c * dx + s * dy) / scale + (template.Width - 1) / 2d;
                var v = (-s * dx + c * dy) / scale + (template.Height - 1) / 2d;
                if (u < 0 || v < 0 || u > template.Width - 1 || v > template.Height - 1) continue;
                var left = (int)Math.Floor(u); var top = (int)Math.Floor(v);
                var right = Math.Min(left + 1, template.Width - 1); var bottom = Math.Min(top + 1, template.Height - 1);
                var a = u - left; var b = v - top;
                var value = (1 - a) * (1 - b) * template.Pixels[top * template.Width + left] + a * (1 - b) * template.Pixels[top * template.Width + right]
                    + (1 - a) * b * template.Pixels[bottom * template.Width + left] + a * b * template.Pixels[bottom * template.Width + right];
                pixels[y * width + x] = (byte)Math.Clamp(Math.Round(value), 0, 255);
            }
        return (VisionImage.FromBuffer(width, height, PixelFormat.Mono8, pixels), center);
    }
}
