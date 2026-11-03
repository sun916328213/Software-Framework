using Vision.AI.Halcon.Engine;
using Vision.Core;
using Vision.Core.Capabilities;
using Vision.Core.Engine;
using Vision.Core.Imaging;
using Vision.Core.Models;
using Vision.Core.Operations;
using Vision.Core.Results;
using Vision.Tools;
using Vision.Tools.Execution;
using Xunit;

namespace Vision.Tests.Tools.Engine;

// fake仅隔离SDK，仍走实际配置校验、文件SHA、统一引擎、模型所有权与分类后处理。
[Trait("Category", "Contract")]
public sealed class HalconClassificationBackendTests
{
    private const string AbcSha = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";

    [Fact]
    public async Task CpuSelection_UsesVerifiedModel_AndPublishesFilteredRankedOriginalClassIds()
    {
        using var file = new ModelFile();
        var native = new ProbeRuntime();
        await using var engine = Create(native);
        var context = await engine.CreateContextAsync(default);
        var model = await engine.PrepareModelAsync(context, new AiClassificationModelDefinition(file.Path, AbcSha), 1, default);
        var image = Frame();
        var outcome = await engine.ExecuteAsync(context, new AiClassificationOperation(image, model, 0.8, 2), default);
        Assert.Equal("cpu", native.Device);
        Assert.Equal(VisionExecutionStatus.Success, outcome.Status);
        Assert.Equal(new[] { "17", "8" }, outcome.Value!.Predictions.Select(item => item.ClassId));
        Assert.Equal(image.FrameId, outcome.Value.FrameId);
        Assert.Equal(image.CoordinateSpaceId, outcome.Value.CoordinateSpaceId);
        Assert.Equal(1, native.Model.Runs);
        var empty = await engine.ExecuteAsync(context, new AiClassificationOperation(image, model, 1), default);
        Assert.Equal(VisionExecutionStatus.NoMatch, empty.Status); Assert.Empty(empty.Value!.Predictions);
        await engine.ReleaseContextAsync(context, default);
        Assert.True(native.Model.Disposed);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DuplicateRawClassIds_AreRejectedEvenWhenFilteringWouldHideThem(bool belowThreshold)
    {
        using var file = new ModelFile();
        var native = new ProbeRuntime();
        native.Model.Predictions = belowThreshold
            ? [new("17", 0.95), new("17", 0.1)]
            : [new("17", 0.95), new("17", 0.9)];
        await using var engine = Create(native);
        var context = await engine.CreateContextAsync(default);
        var model = await engine.PrepareModelAsync(context, new AiClassificationModelDefinition(file.Path, AbcSha), 1, default);
        var outcome = await engine.ExecuteAsync(context, new AiClassificationOperation(Frame(), model, 0.8, 1), default);
        Assert.Equal(VisionExecutionStatus.Failed, outcome.Status);
        Assert.Null(outcome.Value);
        Assert.Equal("ExecutionFailed", outcome.Issue!.Code);
    }

    [Fact]
    public async Task RawClassIds_AreComparedOrdinally_AndNullEntryIsRejected()
    {
        using var file = new ModelFile();
        var native = new ProbeRuntime();
        native.Model.Predictions = [new("A", 0.9), new("a", 0.8)];
        await using var engine = Create(native);
        var context = await engine.CreateContextAsync(default);
        var model = await engine.PrepareModelAsync(context, new AiClassificationModelDefinition(file.Path, AbcSha), 1, default);
        var distinct = await engine.ExecuteAsync(context, new AiClassificationOperation(Frame(), model, 0, 2), default);
        Assert.Equal(VisionExecutionStatus.Success, distinct.Status);
        Assert.Equal(new[] { "A", "a" }, distinct.Value!.Predictions.Select(item => item.ClassId));
        native.Model.Predictions = [new("A", 0.9), null!];
        var malformed = await engine.ExecuteAsync(context, new AiClassificationOperation(Frame(), model, 0.8, 1), default);
        Assert.Equal(VisionExecutionStatus.Failed, malformed.Status);
        Assert.Null(malformed.Value);
    }

    [Fact]
    public async Task WrongContentHash_DoesNotEnterSdk_AndDoesNotReplaceCurrentModel()
    {
        using var file = new ModelFile();
        var native = new ProbeRuntime();
        await using var engine = Create(native);
        var context = await engine.CreateContextAsync(default);
        var original = await engine.PrepareModelAsync(context, new AiClassificationModelDefinition(file.Path, AbcSha), 1, default);
        await Assert.ThrowsAsync<InvalidDataException>(() => engine.PrepareModelAsync(context,
            new AiClassificationModelDefinition(file.Path, new string('0', 64)), 2, default).AsTask());
        Assert.Equal(1, native.Loads); Assert.False(native.Model.Disposed);
        Assert.Equal(VisionExecutionStatus.Success,
            (await engine.ExecuteAsync(context, new AiClassificationOperation(Frame(), original), default)).Status);
    }

    [Fact]
    public async Task UnsupportedInput_FailsBeforeInference_WithoutGuessingPreprocessing()
    {
        using var file = new ModelFile();
        var native = new ProbeRuntime();
        await using var engine = Create(native);
        var context = await engine.CreateContextAsync(default);
        var model = await engine.PrepareModelAsync(context, new AiClassificationModelDefinition(file.Path, AbcSha), 1, default);
        var wrongSize = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 3, 4, PixelFormat.Mono8, new byte[12]);
        var operation = new AiClassificationOperation(wrongSize, model);
        Assert.Contains(await engine.ValidateAsync(context, operation, default), issue => issue.Code == "UnsupportedPreprocessing");
        var result = await engine.ExecuteAsync(context, operation, default);
        Assert.Equal(VisionExecutionStatus.Unsupported, result.Status); Assert.Null(result.Value);
        var color = new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), 4, 4, PixelFormat.Bgr24, new byte[48]);
        Assert.Equal(VisionExecutionStatus.Unsupported,
            (await engine.ExecuteAsync(context, new AiClassificationOperation(color, model), default)).Status);
        Assert.Equal(0, native.Model.Runs);
    }

    [Fact]
    public void UnsupportedDevice_IsRejectedBeforeAnySdkActivation_AndDoesNotFallback()
    {
        var calls = 0;
        IVisionBackend Activate(VisionExecutionDomain domain, string id, string? device)
        { calls++; throw new InvalidOperationException("Must not activate"); }
        Assert.Throws<NotSupportedException>(() => VisionEngineFactory.CreateConfigured(new("opencv", "halcon", "gpu0"), Activate));
        Assert.Throws<NotSupportedException>(() => VisionEngineFactory.CreateConfigured(new("opencv", "unknown", "cpu"), Activate));
        Assert.Equal(0, calls);
    }

    private static IVisionEngine Create(ProbeRuntime runtime) =>
        VisionEngineFactory.CreateConfigured(new("opencv", "halcon", "cpu"), (domain, id, device) =>
            domain == VisionExecutionDomain.Vision ? new UnusedVisionBackend() : new HalconClassificationBackend(device!, runtime));

    private static VisionFrame Frame() => new(Guid.NewGuid(), Guid.NewGuid(), 4, 4, PixelFormat.Mono8, new byte[16]);

    private sealed class ModelFile : IDisposable
    {
        internal string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid() + ".hdl");
        internal ModelFile() => File.WriteAllText(Path, "abc");
        public void Dispose() => File.Delete(Path);
    }

    private sealed class ProbeRuntime : IHalconClassificationRuntime
    {
        public Exception? CleanupFailure => null;
        internal readonly ProbeModel Model = new();
        internal int Loads;
        internal string? Device;
        public IHalconClassificationModel Load(string path, string deviceId)
        {
            Assert.Equal("abc", File.ReadAllText(path));
            Assert.Throws<IOException>(() => { using var writer = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite); });
            Loads++; Device = deviceId; return Model;
        }
        public void Dispose() => Assert.True(Model.Disposed);
    }

    private sealed class ProbeModel : IHalconClassificationModel
    {
        public int Width => 4;
        public int Height => 4;
        internal int Runs;
        internal bool Disposed;
        internal IReadOnlyList<AiClassPrediction> Predictions = [new("42", 0.2), new("8", 0.85), new("17", 0.97)];
        public IReadOnlyList<AiClassPrediction> Run(VisionFrame image)
        {
            Assert.False(Disposed); Runs++;
            return Predictions;
        }
        public void Dispose() => Disposed = true;
    }

    private sealed class UnusedVisionBackend : IVisionBackend
    {
        public string Id => "opencv";
        public VisionExecutionDomain Domain => VisionExecutionDomain.Vision;
        public IReadOnlyList<VisionOperationCapability> Capabilities => [];
        public IReadOnlyList<Vision.Core.Diagnostics.VisionIssue> Validate(VisionOperation operation, IPreparedVisionModel? model) => [];
        public ValueTask<IPreparedVisionModel> PrepareAsync(VisionModelDefinition definition, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<BackendOutput> ExecuteAsync(IPreparedVisionModel? model, VisionOperation operation, CancellationToken cancellationToken) => throw new NotSupportedException();
        public void Dispose() { }
    }
}
