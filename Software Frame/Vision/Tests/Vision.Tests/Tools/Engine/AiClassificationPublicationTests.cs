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
public sealed class AiClassificationPublicationTests
{
    [Theory]
    [InlineData("below-threshold")]
    [InlineData("too-many")]
    [InlineData("duplicate-id")]
    [InlineData("ascending-score")]
    public async Task InvalidBackendPredictions_FailBeforePublication_WithoutRepairingOrTruncatingThem(string fault)
    {
        AiClassPrediction[] predictions = fault switch
        {
            "below-threshold" => [new("17", 0.79)],
            "too-many" => [new("17", 0.95), new("8", 0.9)],
            "duplicate-id" => [new("17", 0.95), new("17", 0.9)],
            _ => [new("17", 0.8), new("8", 0.95)]
        };
        var maximum = fault == "too-many" ? 1 : 2;
        await using var engine = Create(predictions);
        var context = await engine.CreateContextAsync(default);
        var model = await engine.PrepareModelAsync(context,
            new AiClassificationModelDefinition("not-a-real-model.hdl", new string('a', 64)), 1, default);
        var result = await engine.ExecuteAsync(context, new AiClassificationOperation(Frame(), model, 0.8, maximum), default);
        Assert.Equal(VisionExecutionStatus.Failed, result.Status);
        Assert.Null(result.Value);
        Assert.Equal("ExecutionFailed", result.Issue!.Code);
    }

    [Fact]
    public async Task InclusiveThresholdEqualScoresAndDistinctOrdinalClassIds_RemainValid()
    {
        AiClassPrediction[] predictions = [new("017", 0.8), new("17", 0.8)];
        await using var engine = Create(predictions);
        var context = await engine.CreateContextAsync(default);
        var model = await engine.PrepareModelAsync(context,
            new AiClassificationModelDefinition("not-a-real-model.hdl", new string('a', 64)), 1, default);
        var image = Frame();
        var result = await engine.ExecuteAsync(context, new AiClassificationOperation(image, model, 0.8, 2), default);
        Assert.Equal(VisionExecutionStatus.Success, result.Status);
        Assert.Equal(new[] { "017", "17" }, result.Value!.Predictions.Select(prediction => prediction.ClassId));
        Assert.All(result.Value.Predictions, prediction => Assert.Equal(0.8, prediction.Score));
        await engine.ReleaseContextAsync(context, default);
        Assert.Equal(image.FrameId, result.Value.FrameId);
        Assert.Equal(2, result.Value.Predictions.Count);
    }

    [Fact]
    public async Task EmptyFilteredResult_IsStillNoMatchWithEmptySnapshot_NotMalformedPredictions()
    {
        await using var engine = Create([]);
        var context = await engine.CreateContextAsync(default);
        var model = await engine.PrepareModelAsync(context,
            new AiClassificationModelDefinition("not-a-real-model.hdl", new string('a', 64)), 1, default);
        var result = await engine.ExecuteAsync(context, new AiClassificationOperation(Frame(), model, 1, 2), default);
        Assert.Equal(VisionExecutionStatus.NoMatch, result.Status);
        Assert.Empty(result.Value!.Predictions);
    }

    private static IVisionEngine Create(AiClassPrediction[] predictions)
        => VisionEngineFactory.CreateConfigured(new("opencv", "halcon", "cpu"),
            (domain, id, _) => new PublicationBackend(domain, id, predictions));

    private static VisionFrame Frame() => new(Guid.NewGuid(), Guid.NewGuid(), 4, 4, PixelFormat.Mono8, new byte[16]);

    // 只伪造最末端不可信发布结果，仍走真实选择、准备、上下文租约及发布检查；不加载HALCON或真实模型。
    private sealed class PublicationBackend(VisionExecutionDomain domain, string id, AiClassPrediction[] predictions) : IVisionBackend
    {
        public string Id => id;
        public VisionExecutionDomain Domain => domain;
        public IReadOnlyList<VisionOperationCapability> Capabilities => domain == VisionExecutionDomain.Ai
            ? [new(AiClassificationOperation.Id, domain, VisionOperationFeatures.None)] : [];
        public IReadOnlyList<VisionIssue> Validate(VisionOperation operation, IPreparedVisionModel? model) => [];
        public ValueTask<IPreparedVisionModel> PrepareAsync(VisionModelDefinition definition, CancellationToken token)
            => ValueTask.FromResult<IPreparedVisionModel>(new Prepared());
        public ValueTask<BackendOutput> ExecuteAsync(IPreparedVisionModel? model, VisionOperation operation, CancellationToken token)
            => ValueTask.FromResult(new BackendOutput(new AiClassificationOutput(operation.Image!.FrameId,
                operation.Image.CoordinateSpaceId, predictions), predictions.Length == 0));
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
