using Vision.AI.Halcon.Engine;
using Vision.Core;
using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Imaging;
using Vision.Core.Models;
using Vision.Core.Operations;
using Vision.Core.Results;
using Vision.Tools;
using Vision.Tools.Execution;
using Xunit;

namespace Vision.Tests.Tools.Engine;

[Trait("Category", "Contract")]
public sealed class AiPredictionJudgmentEngineTests
{
    [Fact]
    public async Task ExplicitCandidateAndInclusiveScore_AreTrulyNoImageNoSdk_AndKeepSourceSnapshot()
    {
        var source = Snapshot([new("Part", 0.9), new("Other", 0.85)]);
        await using var engine = CommonEngine();
        var context = await engine.CreateContextAsync(default);
        var request = new AiPredictionJudgmentOperation(source, 1, "Other", 0.85);
        Assert.Null(request.Image);
        Assert.Null(request.Model);
        var result = await engine.ExecuteAsync(context, request, default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.True(result.Value!.Passed);
        Assert.Same(source, result.Value.Source);
        Assert.Same(source.Predictions[1], result.Value.ActualPrediction);
        Assert.Equal(source.FrameId, result.Value.FrameId);
        Assert.Equal(source.CoordinateSpaceId, result.Value.CoordinateSpaceId);
        var numeric = await engine.ExecuteAsync(context,
            new AiPredictionJudgmentOperation(Snapshot([new("17", 0.9)]), 0, "017", 0), default);
        Assert.False(numeric.Value!.Passed);
    }

    [Theory]
    [InlineData("part", 0.9)]
    [InlineData("Part ", 0.9)]
    [InlineData("Other", 0.8)]
    [InlineData("Part", 0.9001)]
    public async Task MismatchedClassOrLowScore_IsNgSuccess_WithoutTrimSearchOrAutomaticCandidateChange(string expected, double minimum)
    {
        var source = Snapshot([new("Part", 0.9), new("Other", 0.85)]);
        await using var engine = CommonEngine();
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new AiPredictionJudgmentOperation(source, 0, expected, minimum), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.False(result.Value!.Passed);
        Assert.Same(source.Predictions[0], result.Value.ActualPrediction);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EmptyOrMissingExplicitPrediction_IsNgWithNullActual_NotNoMatchOrAnotherPrediction(bool empty)
    {
        var source = Snapshot(empty ? [] : [new("Part", 0.9)]);
        var index = empty ? 0 : int.MaxValue;
        await using var engine = CommonEngine();
        var context = await engine.CreateContextAsync(default);
        var result = await engine.ExecuteAsync(context, new AiPredictionJudgmentOperation(source, index, "Part", 0), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.False(result.Value!.Passed);
        Assert.Null(result.Value.ActualPrediction);
        Assert.Equal(index, result.Value.PredictionIndex);
    }

    [Fact]
    public void InvalidRulesAndContradictoryDecision_AreRejectedWithoutInventedDefaults()
    {
        var source = Snapshot([new("Part", 0.9)]);
        Assert.Throws<ArgumentNullException>(() => new AiPredictionJudgmentOperation(null!, 0, "Part", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AiPredictionJudgmentOperation(source, -1, "Part", 0));
        Assert.Throws<ArgumentException>(() => new AiPredictionJudgmentOperation(source, 0, " ", 0));
        foreach (var score in new[] { double.NaN, double.PositiveInfinity, -0.1, 1.1 })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new AiPredictionJudgmentOperation(source, 0, "Part", score));
        }
        Assert.Throws<ArgumentException>(() => new AiPredictionJudgmentOutput(source, 0, "Part", 0.9, false));
    }

    [Fact]
    public async Task ActualHalconAdapterWithSdkFake_PublishesClassification_ThenJudgesAfterModelContextClose()
    {
        // abc仅作为SHA/读租约的测试内容，末端SDK为fake；不是HALCON权重或真实推理验收。
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".hdl");
        File.WriteAllText(path, "abc");
        try
        {
            var runtime = new ClassificationRuntime();
            await using var engine = VisionEngineFactory.CreateConfigured(new("opencv", "halcon", "cpu"), (domain, _, device) =>
                domain == VisionExecutionDomain.Ai ? new HalconClassificationBackend(device!, runtime) : new NoJudgmentSdk("opencv"));
            var classifying = await engine.CreateContextAsync(default);
            var model = await engine.PrepareModelAsync(classifying, new AiClassificationModelDefinition(path,
                "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad"), 1, default);
            var frame = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 4, 4, PixelFormat.Mono8, new byte[16]);
            var classified = await engine.ExecuteAsync(classifying, new AiClassificationOperation(frame, model, 0.8, 2), default);
            Assert.Equal(VisionExecutionStatus.Success, classified.Status);
            Assert.Equal(new[] { "17", "8" }, classified.Value!.Predictions.Select(prediction => prediction.ClassId));
            Assert.Equal(1, runtime.Model.Runs);
            await engine.ReleaseContextAsync(classifying, default);
            Assert.True(runtime.Model.Disposed);
            var evaluating = await engine.CreateContextAsync(default);
            var ok = await engine.ExecuteAsync(evaluating, new AiPredictionJudgmentOperation(classified.Value, 0, "17", 0.97), default);
            var ng = await engine.ExecuteAsync(evaluating, new AiPredictionJudgmentOperation(classified.Value, 1, "17", 0), default);
            Assert.Equal(VisionExecutionStatus.Success, ok.Status);
            Assert.True(ok.Value!.Passed);
            Assert.Same(classified.Value, ok.Value.Source);
            Assert.Equal(VisionExecutionStatus.Success, ng.Status);
            Assert.False(ng.Value!.Passed);
            Assert.Equal(1, runtime.Model.Runs);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static AiClassificationOutput Snapshot(AiClassPrediction[] predictions) => new(Guid.NewGuid(), Guid.NewGuid(), predictions);
    private static IVisionEngine CommonEngine() => VisionEngineFactory.Create(new("test"), (_, _) => new NoJudgmentSdk("test"));

    private sealed class NoJudgmentSdk(string id) : IVisionBackend
    {
        public string Id => id;
        public VisionExecutionDomain Domain => VisionExecutionDomain.Vision;
        public IReadOnlyList<VisionOperationCapability> Capabilities => [];
        public IReadOnlyList<VisionIssue> Validate(VisionOperation operation, IPreparedVisionModel? model)
            => throw new InvalidOperationException("Prediction judgment must not validate through SDK");
        public ValueTask<IPreparedVisionModel> PrepareAsync(VisionModelDefinition definition, CancellationToken token) => throw new NotSupportedException();
        public ValueTask<BackendOutput> ExecuteAsync(IPreparedVisionModel? model, VisionOperation operation, CancellationToken token)
            => throw new InvalidOperationException("Prediction judgment must not execute through SDK");
        public void Dispose()
        {
        }
    }

    private sealed class ClassificationRuntime : IHalconClassificationRuntime
    {
        public Exception? CleanupFailure => null;
        internal ClassificationModel Model { get; } = new();
        public IHalconClassificationModel Load(string path, string deviceId)
        {
            Assert.Equal("cpu", deviceId);
            return Model;
        }
        public void Dispose()
        {
        }
    }

    private sealed class ClassificationModel : IHalconClassificationModel
    {
        public int Width => 4;
        public int Height => 4;
        internal int Runs;
        internal bool Disposed;
        public IReadOnlyList<AiClassPrediction> Run(VisionFrame image)
        {
            Assert.False(Disposed);
            Runs++;
            return [new("8", 0.85), new("17", 0.97)];
        }
        public void Dispose() => Disposed = true;
    }
}
