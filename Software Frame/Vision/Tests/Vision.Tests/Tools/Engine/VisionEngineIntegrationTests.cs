using Vision.Core;
using Vision.Core.Engine;
using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Models;
using Vision.Core.Operations;
using Vision.Core.Results;
using Vision.Tools;
using Xunit;

namespace Vision.Tests.Tools.Engine;

[Trait("Category", "Regression")]
public sealed class VisionEngineIntegrationTests
{
    [Fact]
    public async Task SelectedOpenCv_ExecutesRealTemplateThroughFacade_UsingOriginalAnchor()
    {
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var template = Pattern();
        var scene = Scene(template);
        var model = await engine.PrepareModelAsync(context, Definition(template), 1, default);
        var operation = new TemplateMatchOperation(scene, model, new(), 0.98);
        Assert.Empty(await engine.ValidateAsync(context, operation, default));
        var outcome = await engine.ExecuteAsync(context, operation, default);
        Assert.Equal(VisionExecutionStatus.Success, outcome.Status);
        var match = Assert.Single(outcome.Value!.Matches);
        Assert.InRange(match.Position.DistanceTo(new(9, 10)), 0, 0.01); // 插入(7,9)，锚点(2,1)。
        Assert.Equal(scene.FrameId, outcome.Value.FrameId);
        Assert.Equal(scene.CoordinateSpaceId, outcome.Value.CoordinateSpaceId);
        Assert.Equal(1, match.Scale);
        Assert.Equal(new Point2D(9, 10), match.TemplateToImage.Transform(new(2, 1)));
        await engine.ReleaseContextAsync(context, default);
        await engine.ReleaseContextAsync(context, default);
    }

    [Fact]
    public async Task FailedPreparation_PreservesOldModel_AndSuccessfulReplacementInvalidatesOldReference()
    {
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var template = Pattern(); var scene = Scene(template);
        var original = await engine.PrepareModelAsync(context, Definition(template), 1, default);
        var uniform = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 4, 4, PixelFormat.Mono8, new byte[16]);
        await Assert.ThrowsAsync<ArgumentException>(async () => await engine.PrepareModelAsync(context, Definition(uniform), 2, default));
        Assert.Equal(VisionExecutionStatus.Success, (await engine.ExecuteAsync(context, new TemplateMatchOperation(scene, original, new(), 0.98), default)).Status);
        var replacement = await engine.PrepareModelAsync(context, Definition(template), 3, default);
        Assert.NotEqual(original.ModelId, replacement.ModelId);
        var old = await engine.ExecuteAsync(context, new TemplateMatchOperation(scene, original, new()), default);
        Assert.Equal(VisionExecutionStatus.Failed, old.Status);
        Assert.Null(old.Value);
        Assert.Equal("ModelNotCurrent", old.Issue!.Code);
    }

    [Fact]
    public async Task ForeignOrForgedModels_AreRejectedBeforeCallingNativeExecution()
    {
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var first = await engine.CreateContextAsync(default);
        var second = await engine.CreateContextAsync(default);
        var template = Pattern(); var definition = Definition(template);
        var model = await engine.PrepareModelAsync(first, definition, 1, default);
        var operation = new TemplateMatchOperation(Scene(template), model, new());
        var foreign = await engine.ExecuteAsync(second, operation, default);
        Assert.Equal(VisionExecutionStatus.Failed, foreign.Status);
        Assert.Null(foreign.Value);
        var forged = new VisionModelReference(first, model.ModelId, "opencv", definition, 1);
        var fake = await engine.ExecuteAsync(first, new TemplateMatchOperation(Scene(template), forged, new()), default);
        Assert.Equal("ModelNotCurrent", fake.Issue!.Code);
    }

    [Theory]
    [InlineData(90, 1)]
    [InlineData(-90, 0.5)]
    [InlineData(0, 2)]
    [InlineData(30, 1.5)]
    public async Task RealPoseSearch_PreservesScaleAngleAndNonCentralAnchor(double angle, double scale)
    {
        var source = EngineSyntheticFrames.Pattern();
        var (sceneImage, center) = EngineSyntheticFrames.Scene(source, angle, scale);
        var template = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), source.Width, source.Height, PixelFormat.Mono8, source.Pixels);
        var scene = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), sceneImage.Width, sceneImage.Height, PixelFormat.Mono8, sceneImage.Pixels);
        var origin = new Point2D(3, 5);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var model = await engine.PrepareModelAsync(context, new TemplateModelDefinition(template, origin, new(-180, 360, 0.5, 2)), 1, default);
        var result = await engine.ExecuteAsync(context, new TemplateMatchOperation(scene, model, new(angle, 0, scale, scale), 0.94), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        var found = Assert.Single(result.Value!.Matches);
        var offset = new Pose2D(0, 0, angle).Transform(new((origin.X - (source.Width - 1) / 2d) * scale,
            (origin.Y - (source.Height - 1) / 2d) * scale));
        var expected = new Point2D(center.X - 0.5 + offset.X, center.Y - 0.5 + offset.Y);
        Assert.InRange(found.Position.DistanceTo(expected), 0, 0.02);
        Assert.Equal(scale, found.Scale, 8);
        Assert.InRange(Math.Abs(Pose2D.NormalizeAngle(found.AngleDegrees - angle)), 0, 0.01);
    }

    [Fact]
    public async Task TemplateMask_IgnoresExcludedPixels_WithoutReplacingThemWithBlackFeatures()
    {
        var source = EngineSyntheticFrames.Pattern(8, 8);
        var template = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 8, 8, PixelFormat.Mono8, source.Pixels);
        var pixels = Enumerable.Repeat((byte)18, 24 * 24).ToArray();
        var mask = new byte[64];
        for (var y = 0; y < 8; y++)
            for (var x = 0; x < 8; x++)
            {
                pixels[(y + 9) * 24 + x + 7] = x < 4 ? source.Pixels[y * 8 + x] : (byte)(255 - source.Pixels[y * 8 + x]);
                mask[y * 8 + x] = x < 4 ? (byte)255 : (byte)0;
            }
        var scene = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 24, 24, PixelFormat.Mono8, pixels);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var masked = await engine.PrepareModelAsync(context, new TemplateModelDefinition(template, new(2, 1), new(), new VisionMask(8, 8, mask)), 1, default);
        var result = await engine.ExecuteAsync(context, new TemplateMatchOperation(scene, masked, new(), 0.99), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.InRange(Assert.Single(result.Value!.Matches).Position.DistanceTo(new(9, 10)), 0, 0.01);
        var unmasked = await engine.PrepareModelAsync(context, Definition(template), 2, default);
        Assert.Equal(VisionExecutionStatus.NoMatch, (await engine.ExecuteAsync(context, new TemplateMatchOperation(scene, unmasked, new(), 0.99), default)).Status);
    }

    [Fact]
    public async Task MaskedPoseSearch_TransformsTemplateAndMaskTogether()
    {
        var templateImage = EngineSyntheticFrames.Pattern();
        var changed = templateImage.Pixels.ToArray();
        var participation = new byte[changed.Length];
        for (var y = 0; y < templateImage.Height; y++)
            for (var x = 0; x < templateImage.Width; x++)
            {
                var index = y * templateImage.Width + x;
                if (x < templateImage.Width / 2) participation[index] = 255;
                else changed[index] = (byte)(255 - changed[index]);
            }
        var target = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), templateImage.Width, templateImage.Height,
            PixelFormat.Mono8, changed);
        var (sceneImage, center) = EngineSyntheticFrames.Scene(target, 30, 1.5);
        var template = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), templateImage.Width, templateImage.Height, PixelFormat.Mono8, templateImage.Pixels);
        var scene = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), sceneImage.Width, sceneImage.Height, PixelFormat.Mono8, sceneImage.Pixels);
        var origin = new Point2D(3, 5);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var model = await engine.PrepareModelAsync(context, new TemplateModelDefinition(template, origin, new(-180, 360, 0.5, 2),
            new VisionMask(template.Width, template.Height, participation)), 1, default);
        var outcome = await engine.ExecuteAsync(context, new TemplateMatchOperation(scene, model, new(30, 0, 1.5, 1.5), 0.94), default);
        Assert.Equal(VisionExecutionStatus.Success, outcome.Status);
        var found = Assert.Single(outcome.Value!.Matches);
        var offset = new Pose2D(0, 0, 30).Transform(new((origin.X - (template.Width - 1) / 2d) * 1.5,
            (origin.Y - (template.Height - 1) / 2d) * 1.5));
        Assert.InRange(found.Position.DistanceTo(new(center.X - 0.5 + offset.X, center.Y - 0.5 + offset.Y)), 0, 0.02);
    }

    [Fact]
    public async Task SearchMask_RestrictsAnchorCandidates_BeforeSuppression()
    {
        var template = Pattern();
        var first = Scene(template);
        var pixels = first.Pixels.ToArray();
        for (var y = 0; y < template.Height; y++)
            for (var x = 0; x < template.Width; x++) pixels[(y + 15) * 24 + x + 14] = template.Pixels[y * template.Stride + x];
        var scene = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 24, 24, PixelFormat.Mono8, pixels);
        var area = new byte[24 * 24]; area[16 * 24 + 16] = 255;
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var model = await engine.PrepareModelAsync(context, Definition(template), 1, default);
        var result = await engine.ExecuteAsync(context,
            new TemplateMatchOperation(scene, model, new(), 0.99, searchMask: new VisionMask(24, 24, area)), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(new Point2D(16, 16), Assert.Single(result.Value!.Matches).Position);
    }

    [Fact]
    public async Task ExplicitSamplingBudgetAndUnsupportedPixels_AreRejectedWithoutFallback()
    {
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var template = Pattern();
        var model = await engine.PrepareModelAsync(context, new TemplateModelDefinition(template, new(), new(-180, 360)), 1, default);
        var tooDense = new TemplateMatchOperation(Scene(template), model, new(-180, 360), sampling: new(0.001, 0.1));
        Assert.Contains(await engine.ValidateAsync(context, tooDense, default), issue => issue.Code == "SearchBudgetExceeded");
        var rejected = await engine.ExecuteAsync(context, tooDense, default);
        Assert.Equal(VisionExecutionStatus.Failed, rejected.Status); Assert.Null(rejected.Value);
        var automatic = new TemplateMatchOperation(Scene(template), model, new(), sampling: TemplateSearchSampling.NativeAutomatic);
        Assert.Equal(VisionExecutionStatus.Unsupported, (await engine.ExecuteAsync(context, automatic, default)).Status);
        var color = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 4, 4, PixelFormat.Bgr24, new byte[48]);
        Assert.Equal(VisionExecutionStatus.Unsupported,
            (await engine.ExecuteAsync(context, new TemplateMatchOperation(color, model, new()), default)).Status);
    }

    private static TemplateModelDefinition Definition(VisionFrame template) => new(template, new(2, 1), new());
    private static VisionFrame Pattern()
    {
        byte[] pixels = [20, 91, 34, 180, 3, 79, 207, 60, 110, 23, 251, 87, 134, 222, 5, 155];
        return new(Guid.NewGuid(), Guid.NewGuid(), 4, 4, PixelFormat.Mono8, pixels);
    }
    private static VisionFrame Scene(VisionFrame template)
    {
        var pixels = Enumerable.Repeat((byte)18, 24 * 24).ToArray();
        for (var y = 0; y < template.Height; y++)
            for (var x = 0; x < template.Width; x++) pixels[(y + 9) * 24 + x + 7] = template.Pixels[y * template.Stride + x];
        return new(Guid.NewGuid(), Guid.NewGuid(), 24, 24, PixelFormat.Mono8, pixels);
    }
}
