using Vision.Core;
using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Models;
using Vision.Core.Operations;
using Vision.Core.Results;
using Vision.Core.Roi;
using Vision.Tools;
using Vision.Tools.Execution;
using Xunit;

namespace Vision.Tests.Tools.Engine;

[Trait("Category", "Contract")]
public sealed class CaliperLocationFollowTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    public async Task ActualLocation_FollowsLinePairAndCircle_WithoutMutatingTemplateBaselines(double angle)
    {
        var (template, frame) = Scene(angle);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var locating = await engine.CreateContextAsync(default);
        var measuring = await engine.CreateContextAsync(default);
        var range = new TemplateSearchRange(angle);
        var definition = new TemplateModelDefinition(template, new(2, 3), range);
        var model = await engine.PrepareModelAsync(locating, definition, 1, default);
        var located = await engine.ExecuteAsync(locating, new TemplateMatchOperation(frame, model, range, 0.99), default);
        Assert.Equal(VisionExecutionStatus.Success, located.Status);
        var binding = new TemplateLocationBinding(located.Value!, model, 0);
        var (line, pair, circle) = Operations(frame, binding);
        var lineResult = await engine.ExecuteAsync(measuring, line, default);
        var pairResult = await engine.ExecuteAsync(measuring, pair, default);
        var circleResult = await engine.ExecuteAsync(measuring, circle, default);
        Assert.Equal(VisionExecutionStatus.Success, lineResult.Status);
        Assert.Equal(VisionExecutionStatus.Success, pairResult.Status);
        Assert.Equal(VisionExecutionStatus.Success, circleResult.Status);
        Assert.Equal(new Point2D(10, 25), line.ReferenceStart);
        Assert.Equal(new Point2D(70, 25), line.ReferenceEnd);
        Assert.Equal(new Point2D(90, 45), circle.ReferenceCenter);
        Assert.Equal(12, circle.ReferenceNominalRadiusPixels);
        Assert.Null(line.Model);
        if (angle == 0)
        {
            Assert.Equal(29.5, lineResult.Value!.Line!.Start.Y, 5);
            Assert.InRange(circleResult.Value!.Circle!.Center.DistanceTo(new(98, 55)), 0, 0.8);
        }
        else
        {
            Assert.Equal(97.5, lineResult.Value!.Line!.Start.X, 5);
            Assert.InRange(circleResult.Value!.Circle!.Center.DistanceTo(new(72, 98)), 0, 0.8);
        }
        Assert.Equal(10, pairResult.Value!.Statistics!.Mean.Value);
        Assert.InRange(circleResult.Value!.Circle!.RadiusPixels, 11.2, 12.8);
        var tolerance = await engine.ExecuteAsync(measuring, new MeasurementRangeOperation(
            circleResult.Value.Circle.Diameter, 22, 26, GeometryMeasurementUnit.Pixels), default);
        Assert.True(tolerance.Value!.Passed);
        await engine.PrepareModelAsync(locating, definition, 2, default);
        Assert.Contains(await engine.ValidateAsync(measuring, line, default), issue => issue.Code == "LocationModelNotCurrent");
        await Rejected(engine, measuring, line, "LocationModelNotCurrent");
        await Rejected(engine, measuring, pair, "LocationModelNotCurrent");
        await Rejected(engine, measuring, circle, "LocationModelNotCurrent");
    }

    [Fact]
    public async Task ForgedLocationAndRelabeledImage_AreRejectedForAllThreeMeasurementOperations()
    {
        var (template, frame) = Scene(0);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var locating = await engine.CreateContextAsync(default);
        var measuring = await engine.CreateContextAsync(default);
        var model = await engine.PrepareModelAsync(locating, new TemplateModelDefinition(template, new(2, 3), new()), 1, default);
        var located = await engine.ExecuteAsync(locating, new TemplateMatchOperation(frame, model, new(), 0.99), default);
        Assert.Equal(VisionExecutionStatus.Success, located.Status);
        var original = located.Value!;
        var forged = new TemplateMatchOutput(frame.FrameId, frame.CoordinateSpaceId, original.ScoreMeaning, original.Matches);
        var (line, pair, circle) = Operations(frame, new(forged, model, 0));
        await Rejected(engine, measuring, line, "LocationNotPublished");
        await Rejected(engine, measuring, pair, "LocationNotPublished");
        await Rejected(engine, measuring, circle, "LocationNotPublished");
        var copy = new VisionFrame(frame.FrameId, frame.CoordinateSpaceId, frame.Width, frame.Height, frame.Format, frame.Pixels);
        (line, pair, circle) = Operations(copy, new(original, model, 0));
        await Rejected(engine, measuring, line, "LocationFrameMismatch");
        await Rejected(engine, measuring, pair, "LocationFrameMismatch");
        await Rejected(engine, measuring, circle, "LocationFrameMismatch");
        var wrong = new VisionFrame(Guid.NewGuid(), frame.CoordinateSpaceId, frame.Width, frame.Height, frame.Format, frame.Pixels);
        Assert.Throws<ArgumentException>(() => Operations(wrong, new(original, model, 0)));
        var wrongSpace = new VisionFrame(frame.FrameId, Guid.NewGuid(), frame.Width, frame.Height, frame.Format, frame.Pixels);
        Assert.Throws<ArgumentException>(() => Operations(wrongSpace, new(original, model, 0)));
        await engine.ReleaseContextAsync(locating, default);
        (line, pair, circle) = Operations(frame, new(original, model, 0));
        await Rejected(engine, measuring, line, "LocationModelNotCurrent");
        await Rejected(engine, measuring, pair, "LocationModelNotCurrent");
        await Rejected(engine, measuring, circle, "LocationModelNotCurrent");
    }

    [Fact]
    public void NonCentralAnchorRotationAndScale_TransformOnlyNominalGeometry_NotCurrentImagePixelParameters()
    {
        var frame = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 200, 200, PixelFormat.Mono8, new byte[40000]);
        var template = EngineSyntheticFrames.Pattern(8, 8);
        var model = new VisionModelReference(new(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid(), "geometry-test",
            new TemplateModelDefinition(template, new(3, 2), new(0, 360, 1, 2)), 1);
        var located = new TemplateMatchOutput(frame.FrameId, frame.CoordinateSpaceId, "geometry only",
            [new(new(100, 80), new(3, 2), 90, 2, 1)]);
        var binding = new TemplateLocationBinding(located, model, 0);
        var line = new CaliperLineOperation(frame, new(3, 2), new(8, 2), 3, 2, 1,
            gaussianSigmaPixels: 0, locationBinding: binding);
        var pair = new CaliperEdgePairOperation(frame, new(3, 2), new(8, 2), 1, 4,
            caliperCount: 3, normalHalfLength: 2, tangentHalfWidth: 1, gaussianSigmaPixels: 0, locationBinding: binding);
        var circle = new CaliperCircleOperation(frame, new(5, 2), 6, 12, 2, 1,
            gaussianSigmaPixels: 0, angleStartDegrees: 10, angleExtentDegrees: 90,
            minimumCoverageDegrees: 45, locationBinding: binding);
        Assert.Equal(new Point2D(100, 80), line.Start);
        Assert.Equal(new Point2D(100, 90), line.End);
        Assert.Equal(new Point2D(100, 84), circle.Center);
        Assert.Equal(12, circle.NominalRadiusPixels);
        Assert.Equal(100, circle.AngleStartDegrees);
        Assert.Equal(90, circle.AngleExtentDegrees);
        Assert.Equal(2, line.NormalHalfLength);
        Assert.Equal(1, line.TangentHalfWidth);
        Assert.Equal(1, line.MaximumResidualPixels);
        Assert.Equal(1, pair.MinimumWidthPixels);
        Assert.Equal(4, pair.MaximumWidthPixels);
        Assert.Equal(6, circle.ReferenceNominalRadiusPixels);
        Assert.Equal(10, circle.ReferenceAngleStartDegrees);
        Assert.Throws<ArgumentOutOfRangeException>(() => new TemplateLocationBinding(located, model, 1));
    }

    [Fact]
    public async Task MultipleLocations_UseTheExplicitCandidateInsteadOfFirstMatch()
    {
        var (template, single) = Scene(0);
        var pixels = new byte[192 * 128];
        for (var row = 0; row < 128; row++)
        {
            single.Pixels.Slice(row * 128, 128).CopyTo(pixels.AsSpan(row * 192, 128));
        }
        for (var y = 0; y < 8; y++)
        {
            template.Pixels.Slice(y * 8, 8).CopyTo(pixels.AsSpan((y + 10) * 192 + 120, 8));
        }
        for (var y = 30; y < 40; y++)
        {
            pixels.AsSpan(y * 192 + 18, 162).Fill(200);
        }
        var frame = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 192, 128, PixelFormat.Mono8, pixels);
        await using var engine = VisionEngineFactory.Create(new("opencv"));
        var context = await engine.CreateContextAsync(default);
        var model = await engine.PrepareModelAsync(context, new TemplateModelDefinition(template, new(2, 3), new()), 1, default);
        var located = await engine.ExecuteAsync(context, new TemplateMatchOperation(frame, model, new(), 0.99, maximumMatches: 2), default);
        Assert.Equal(2, located.Value!.Matches.Count);
        var request = new CaliperLineOperation(frame, new(10, 25), new(30, 25), 5, 12, 0,
            polarity: CaliperEdgePolarity.LightToDark, minimumContrast: 50, gaussianSigmaPixels: 0,
            locationBinding: new(located.Value, model, 1));
        var result = await engine.ExecuteAsync(context, request, default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(located.Value.Matches[1].TemplateToImage.Transform(new(10, 25)), request.Start);
        Assert.Equal(request.Start.X, result.Value!.Line!.Start.X, 5);
        Assert.Equal(29.5, result.Value.Line.Start.Y, 5);
    }

    [Fact]
    public async Task SourceRevisionChangedDuringOtherContextMeasurement_DiscardsLateResult()
    {
        var backend = new PausingCaliperBackend();
        await using var engine = VisionEngineFactory.Create(new("test"), (_, _) => backend);
        var locating = await engine.CreateContextAsync(default);
        var measuring = await engine.CreateContextAsync(default);
        var (template, frame) = Scene(0);
        var definition = new TemplateModelDefinition(template, new(2, 3), new());
        var model = await engine.PrepareModelAsync(locating, definition, 1, default);
        var found = await engine.ExecuteAsync(locating, new TemplateMatchOperation(frame, model, new()), default);
        var (request, _, _) = Operations(frame, new(found.Value!, model, 0));
        var pending = engine.ExecuteAsync(measuring, request, default).AsTask();
        try
        {
            await backend.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await engine.PrepareModelAsync(locating, definition, 2, default);
        }
        finally
        {
            backend.Resume.TrySetResult();
        }
        var result = await pending;
        Assert.Equal(VisionExecutionStatus.Failed, result.Status);
        Assert.Null(result.Value);
        Assert.Equal("LocationModelNotCurrent", result.Issue!.Code);
    }

    private static async Task Rejected<T>(IVisionEngine engine, VisionContext context, VisionOperation<T> operation, string code) where T : class
    {
        var result = await engine.ExecuteAsync(context, operation, default);
        Assert.Equal(VisionExecutionStatus.Failed, result.Status);
        Assert.Null(result.Value);
        Assert.Equal(code, result.Issue!.Code);
    }

    private static (CaliperLineOperation Line, CaliperEdgePairOperation Pair, CaliperCircleOperation Circle) Operations(
        VisionFrame frame, TemplateLocationBinding binding)
    {
        return (
            new(frame, new(10, 25), new(70, 25), 7, 12, 0, polarity: CaliperEdgePolarity.LightToDark,
                minimumContrast: 50, gaussianSigmaPixels: 0, locationBinding: binding),
            new(frame, new(10, 25), new(70, 25), 10, 10, caliperCount: 7, normalHalfLength: 12,
                tangentHalfWidth: 0, minimumContrast: 50, gaussianSigmaPixels: 0, locationBinding: binding),
            new(frame, new(90, 45), 12, 24, 4, 0, minimumContrast: 50, gaussianSigmaPixels: 0,
                minimumPoints: 16, minimumCoverageDegrees: 250, locationBinding: binding));
    }

    private static (VisionFrame Template, VisionFrame Image) Scene(double angle)
    {
        var template = EngineSyntheticFrames.Pattern(8, 8);
        var pixels = Enumerable.Repeat((byte)10, 128 * 128).ToArray();
        for (var y = 0; y < 8; y++)
        {
            template.Pixels.Slice(y * 8, 8).CopyTo(pixels.AsSpan((y + 10) * 128 + 8, 8));
        }
        for (var y = 30; y < 40; y++)
        {
            pixels.AsSpan(y * 128 + 18, 61).Fill(200);
        }
        for (var y = 40; y <= 70; y++)
        {
            for (var x = 82; x <= 114; x++)
            {
                if ((x - 98) * (x - 98) + (y - 55) * (y - 55) <= 144)
                {
                    pixels[y * 128 + x] = 200;
                }
            }
        }
        if (angle == 90)
        {
            var rotated = new byte[pixels.Length];
            for (var y = 0; y < 128; y++)
            {
                for (var x = 0; x < 128; x++)
                {
                    rotated[x * 128 + 127 - y] = pixels[y * 128 + x];
                }
            }
            pixels = rotated;
        }
        return (template, new(Guid.NewGuid(), Guid.NewGuid(), 128, 128, PixelFormat.Mono8, pixels));
    }

    // 仅证明发布事务，不把可控等待当作真实原生SDK并发/精度证据。
    private sealed class PausingCaliperBackend : IVisionBackend
    {
        public TaskCompletionSource Entered
        {
            get;
        } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Resume
        {
            get;
        } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string Id => "test";
        public VisionExecutionDomain Domain => VisionExecutionDomain.Vision;
        public IReadOnlyList<VisionOperationCapability> Capabilities =>
        [new(TemplateMatchOperation.Id, VisionExecutionDomain.Vision, VisionOperationFeatures.None),
         new(CaliperLineOperation.Id, VisionExecutionDomain.Vision, VisionOperationFeatures.None)];
        public IReadOnlyList<VisionIssue> Validate(VisionOperation operation, IPreparedVisionModel? model) => [];
        public ValueTask<IPreparedVisionModel> PrepareAsync(VisionModelDefinition definition, CancellationToken token)
            => ValueTask.FromResult<IPreparedVisionModel>(new Prepared());
        public async ValueTask<BackendOutput> ExecuteAsync(IPreparedVisionModel? model, VisionOperation operation, CancellationToken token)
        {
            if (operation is TemplateMatchOperation match)
            {
                return new(new TemplateMatchOutput(match.Image.FrameId, match.Image.CoordinateSpaceId,
                    "fake transaction", [new(new(10, 13), new(2, 3), 0, 1, 1)]));
            }
            var line = (CaliperLineOperation)operation;
            Entered.TrySetResult();
            await Resume.Task;
            var samples = Enumerable.Range(0, line.CaliperCount).Select(index =>
            {
                var x = line.Start.X + index * (line.End.X - line.Start.X) / (line.CaliperCount - 1);
                return new CaliperEdgeSample(index, new(x, line.Start.Y), new(x, 29.5), -190, true, 0);
            });
            return new(new CaliperLineOutput(line.Image.FrameId, line.Image.CoordinateSpaceId, CaliperLineFitState.Fitted,
                new(new(line.Start.X, 29.5), new(line.End.X, 29.5), line.CaliperCount, 0, 0), samples));
        }
        public void Dispose()
        {
        }
        private sealed class Prepared : IPreparedVisionModel
        {
            public void Dispose()
            {
            }
        }
    }
}
