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

[Trait("Category", "Contract")]
public sealed class VisionEngineLifetimeTests
{
    [Fact]
    public async Task Factory_ActivatesOnlyChosenDomains_AndAiUsesTheSameFacade()
    {
        List<string> activations = [];
        var vision = new ProbeBackend("selected-vision", VisionExecutionDomain.Vision);
        var ai = new ProbeBackend("selected-ai", VisionExecutionDomain.Ai);
        await using (var engine = VisionEngineFactory.Create(new("selected-vision", "selected-ai", "cpu"), (domain, id) =>
        {
            activations.Add(id);
            return domain == VisionExecutionDomain.Vision ? vision : ai;
        }))
        {
            Assert.Equal(new[] { "selected-vision", "selected-ai" }, activations);
            var context = await engine.CreateContextAsync(default);
            var definition = new AiClassificationModelDefinition("never-opened-by-test.onnx", new string('a', 64));
            var model = await engine.PrepareModelAsync(context, definition, 1, default);
            var outcome = await engine.ExecuteAsync(context, new AiClassificationOperation(Frame(), model), default);
            Assert.Equal(VisionExecutionStatus.Success, outcome.Status);
            Assert.Equal("pass", Assert.Single(outcome.Value!.Predictions).ClassId);
            Assert.Equal(0, vision.ExecuteCount);
            Assert.Equal(1, ai.ExecuteCount);
        }
        Assert.Equal(1, vision.DisposeCount); Assert.Equal(1, ai.DisposeCount);
        activations.Clear();
        await using var noAi = VisionEngineFactory.Create(new("only-vision"), (_, id) =>
        {
            activations.Add(id); return new ProbeBackend(id, VisionExecutionDomain.Vision);
        });
        Assert.Equal(new[] { "only-vision" }, activations);
    }

    [Fact]
    public void FailedSelectedBackend_DoesNotFallback_AndPartialStartupIsDisposed()
    {
        var first = new ProbeBackend("vision", VisionExecutionDomain.Vision);
        var failure = new InvalidOperationException("Selected AI runtime unavailable");
        var seen = new List<string>();
        var error = Assert.Throws<InvalidOperationException>(() => VisionEngineFactory.Create(new("vision", "ai", "gpu0"), (_, id) =>
        {
            seen.Add(id); return id == "vision" ? first : throw failure;
        }));
        Assert.Same(failure, error);
        Assert.Equal(new[] { "vision", "ai" }, seen);
        Assert.Equal(1, first.DisposeCount);
        Assert.Throws<NotSupportedException>(() => VisionEngineFactory.Create(new("unregistered")));
        Assert.Throws<NotSupportedException>(() => VisionEngineFactory.Create(new("opencv", "not-implemented", "gpu0")));
    }

    [Fact]
    public async Task ExecutionDuration_IncludesActualBackendWait_WithoutTreatingItAsHardTimeout()
    {
        var started = Signal();
        var resume = Signal();
        var backend = new ProbeBackend("vision", VisionExecutionDomain.Vision)
        {
            Execute = async (_, operation, _) =>
            {
                started.TrySetResult();
                await resume.Task;
                return Output(operation);
            }
        };
        await using var engine = VisionEngineFactory.Create(new("vision"), (_, _) => backend);
        var context = await engine.CreateContextAsync(default);
        var model = await engine.PrepareModelAsync(context, Definition(), 1, default);
        var running = engine.ExecuteAsync(context, new TemplateMatchOperation(Frame(), model, new()), default).AsTask();
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var start = System.Diagnostics.Stopwatch.GetTimestamp();
            await Task.Delay(20);
            var observedWait = System.Diagnostics.Stopwatch.GetElapsedTime(start);
            resume.TrySetResult();
            var result = await running.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(VisionExecutionStatus.Success, result.Status);
            Assert.True(result.ExecutionDuration >= observedWait);
        }
        finally
        {
            resume.TrySetResult();
            await running;
        }
    }

    [Fact]
    public async Task ContextClose_WaitsForActualSdkReturn_RejectsReentry_AndDiscardsLateOutput()
    {
        var started = Signal(); var resume = Signal();
        var backend = new ProbeBackend("vision", VisionExecutionDomain.Vision)
        {
            Execute = async (_, operation, _) =>
            {
                started.TrySetResult(); await resume.Task;
                return Output(operation); // 故意忽略取消，模拟尚未实际停止的SDK。
            }
        };
        var engine = VisionEngineFactory.Create(new("vision"), (_, _) => backend);
        Task<VisionOutcome<TemplateMatchOutput>>? running = null;
        try
        {
            var context = await engine.CreateContextAsync(default);
            var model = await engine.PrepareModelAsync(context, Definition(), 1, default);
            var operation = new TemplateMatchOperation(Frame(), model, new());
            running = engine.ExecuteAsync(context, operation, default).AsTask();
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Assert.ThrowsAsync<InvalidOperationException>(() => engine.ExecuteAsync(context, operation, default).AsTask());
            var closing = engine.ReleaseContextAsync(context, default).AsTask();
            Assert.False(closing.IsCompleted);
            Assert.False(Assert.Single(backend.Models).Disposed);
            Assert.Equal(0, backend.DisposeCount);
            await Assert.ThrowsAsync<ObjectDisposedException>(() => engine.ValidateAsync(context, operation, default).AsTask());
            resume.TrySetResult();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
            await closing.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(backend.Models[0].Disposed);
            Assert.Equal(1, backend.Models[0].DisposeCount);
            await engine.ReleaseContextAsync(context, default);
        }
        finally
        {
            resume.TrySetResult();
            if (running != null) { try { await running; } catch (OperationCanceledException) { } }
            await engine.DisposeAsync();
        }
        Assert.Equal(1, backend.DisposeCount);
    }

    [Fact]
    public async Task CancelledCloseWait_CanBeAwaitedAgain_WithoutLosingOwnedResources()
    {
        var started = Signal(); var resume = Signal();
        var backend = new ProbeBackend("vision", VisionExecutionDomain.Vision)
        {
            Execute = async (_, operation, _) => { started.TrySetResult(); await resume.Task; return Output(operation); }
        };
        await using var engine = VisionEngineFactory.Create(new("vision"), (_, _) => backend);
        var context = await engine.CreateContextAsync(default);
        var model = await engine.PrepareModelAsync(context, Definition(), 1, default);
        var running = engine.ExecuteAsync(context, new TemplateMatchOperation(Frame(), model, new()), default).AsTask();
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            using var cancellation = new CancellationTokenSource();
            var abandonedWait = engine.ReleaseContextAsync(context, cancellation.Token).AsTask();
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => abandonedWait);
            Assert.False(backend.Models[0].Disposed);
            var closing = engine.ReleaseContextAsync(context, default).AsTask();
            Assert.False(closing.IsCompleted);
            resume.TrySetResult();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
            await closing.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(backend.Models[0].Disposed);
        }
        finally { resume.TrySetResult(); try { await running; } catch (OperationCanceledException) { } }
    }

    [Fact]
    public async Task EngineDispose_DoesNotReleaseBackendUntilContextsActuallyStop()
    {
        var started = Signal(); var resume = Signal();
        var backend = new ProbeBackend("vision", VisionExecutionDomain.Vision)
        {
            Execute = async (_, operation, _) => { started.TrySetResult(); await resume.Task; return Output(operation); }
        };
        var engine = VisionEngineFactory.Create(new("vision"), (_, _) => backend);
        var context = await engine.CreateContextAsync(default);
        var model = await engine.PrepareModelAsync(context, Definition(), 1, default);
        var running = engine.ExecuteAsync(context, new TemplateMatchOperation(Frame(), model, new()), default).AsTask();
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var closing = engine.DisposeAsync().AsTask();
            Assert.False(closing.IsCompleted);
            Assert.Equal(0, backend.DisposeCount);
            Assert.False(backend.Models[0].Disposed);
            await Assert.ThrowsAsync<ObjectDisposedException>(() => engine.CreateContextAsync(default).AsTask());
            resume.TrySetResult();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
            await closing.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(backend.Models[0].Disposed); Assert.Equal(1, backend.DisposeCount);
        }
        finally { resume.TrySetResult(); try { await running; } catch (OperationCanceledException) { } await engine.DisposeAsync(); }
    }

    [Fact]
    public async Task CancelledPreparation_CleansLateCandidate_AndKeepsPreviousModelCurrent()
    {
        var started = Signal(); var resume = Signal();
        var backend = new ProbeBackend("vision", VisionExecutionDomain.Vision);
        await using var engine = VisionEngineFactory.Create(new("vision"), (_, _) => backend);
        var context = await engine.CreateContextAsync(default);
        var original = await engine.PrepareModelAsync(context, Definition(), 1, default);
        var candidate = new ProbeModel();
        backend.Prepare = async (_, _) => { started.TrySetResult(); await resume.Task; return candidate; };
        using var cancellation = new CancellationTokenSource();
        var preparing = engine.PrepareModelAsync(context, Definition(), 2, cancellation.Token).AsTask();
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10)); cancellation.Cancel();
            Assert.False(preparing.IsCompleted); Assert.False(candidate.Disposed);
            resume.TrySetResult();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => preparing);
            Assert.True(candidate.Disposed);
            Assert.False(backend.Models[0].Disposed);
            Assert.Equal(VisionExecutionStatus.Success,
                (await engine.ExecuteAsync(context, new TemplateMatchOperation(Frame(), original, new()), default)).Status);
        }
        finally { resume.TrySetResult(); try { await preparing; } catch (OperationCanceledException) { } }
    }

    [Fact]
    public async Task FailedRetirement_IsVisible_AndOldResourceIsRetainedForCloseRetry()
    {
        var backend = new ProbeBackend("vision", VisionExecutionDomain.Vision);
        await using var engine = VisionEngineFactory.Create(new("vision"), (_, _) => backend);
        var context = await engine.CreateContextAsync(default);
        await engine.PrepareModelAsync(context, Definition(), 1, default);
        backend.Models[0].FailDisposals = 1;
        var next = await engine.PrepareModelAsync(context, Definition(), 2, default);
        Assert.False(backend.Models[0].Disposed);
        var operation = new TemplateMatchOperation(Frame(), next, new());
        Assert.Contains(await engine.ValidateAsync(context, operation, default), issue => issue.Code == "ResourceCleanupFailed");
        Assert.Equal(VisionExecutionStatus.Failed, (await engine.ExecuteAsync(context, operation, default)).Status);
        Assert.Equal(0, backend.ExecuteCount);
        await engine.ReleaseContextAsync(context, default);
        Assert.All(backend.Models, resource => Assert.True(resource.Disposed));
        Assert.Equal(2, backend.Models[0].DisposeCount);
    }

    [Fact]
    public async Task BackendCannotReuseOwnedNativeModel_AsANewPreparationCandidate()
    {
        var backend = new ProbeBackend("vision", VisionExecutionDomain.Vision);
        await using var engine = VisionEngineFactory.Create(new("vision"), (_, _) => backend);
        var context = await engine.CreateContextAsync(default);
        var original = await engine.PrepareModelAsync(context, Definition(), 1, default);
        backend.Prepare = (_, _) => ValueTask.FromResult<IPreparedVisionModel>(backend.Models[0]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => engine.PrepareModelAsync(context, Definition(), 2, default).AsTask());
        Assert.False(backend.Models[0].Disposed);
        Assert.Equal(VisionExecutionStatus.Success,
            (await engine.ExecuteAsync(context, new TemplateMatchOperation(Frame(), original, new()), default)).Status);
    }

    [Fact]
    public async Task ModelResource_CannotBeSharedAcrossContexts_AndRejectionDoesNotDisposeItsOwner()
    {
        var backend = new ProbeBackend("vision", VisionExecutionDomain.Vision);
        await using var engine = VisionEngineFactory.Create(new("vision"), (_, _) => backend);
        var first = await engine.CreateContextAsync(default);
        var second = await engine.CreateContextAsync(default);
        var original = await engine.PrepareModelAsync(first, Definition(), 1, default);
        backend.Prepare = (_, _) => ValueTask.FromResult<IPreparedVisionModel>(backend.Models[0]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => engine.PrepareModelAsync(second, Definition(), 2, default).AsTask());
        Assert.False(backend.Models[0].Disposed);
        Assert.Equal(VisionExecutionStatus.Success,
            (await engine.ExecuteAsync(first, new TemplateMatchOperation(Frame(), original, new()), default)).Status);
    }

    [Fact]
    public async Task WrongFrameOutput_FailsWithoutPublishingData()
    {
        var backend = new ProbeBackend("vision", VisionExecutionDomain.Vision)
        {
            Execute = (_, _, _) => ValueTask.FromResult(new BackendOutput(new TemplateMatchOutput(Guid.NewGuid(), Guid.NewGuid(), "test", []), true))
        };
        await using var engine = VisionEngineFactory.Create(new("vision"), (_, _) => backend);
        var context = await engine.CreateContextAsync(default);
        var model = await engine.PrepareModelAsync(context, Definition(), 1, default);
        var result = await engine.ExecuteAsync(context, new TemplateMatchOperation(Frame(), model, new()), default);
        Assert.Equal(VisionExecutionStatus.Failed, result.Status); Assert.Null(result.Value);
    }

    [Fact]
    public async Task FailedContextCleanup_CanBeRetried_WithoutDisposingBackendUnderLiveModels()
    {
        var backend = new ProbeBackend("vision", VisionExecutionDomain.Vision);
        var engine = VisionEngineFactory.Create(new("vision"), (_, _) => backend);
        try
        {
            var context = await engine.CreateContextAsync(default);
            await engine.PrepareModelAsync(context, Definition(), 1, default);
            backend.Models[0].FailDisposals = 1;
            await Assert.ThrowsAsync<AggregateException>(() => engine.DisposeAsync().AsTask());
            Assert.False(backend.Models[0].Disposed);
            Assert.Equal(0, backend.DisposeCount);
            await engine.ReleaseContextAsync(context, default);
            Assert.True(backend.Models[0].Disposed);
            await engine.DisposeAsync();
            await engine.DisposeAsync();
            Assert.Equal(1, backend.DisposeCount);
        }
        finally { await engine.DisposeAsync(); }
    }

    [Fact]
    public async Task CandidateCleanupFailure_PreservesBothOriginalErrorAndResourceOwnership()
    {
        var started = Signal(); var resume = Signal();
        var backend = new ProbeBackend("vision", VisionExecutionDomain.Vision);
        await using var engine = VisionEngineFactory.Create(new("vision"), (_, _) => backend);
        var context = await engine.CreateContextAsync(default);
        await engine.PrepareModelAsync(context, Definition(), 1, default);
        var candidate = new ProbeModel { FailDisposals = 1 };
        backend.Prepare = async (_, _) => { started.TrySetResult(); await resume.Task; return candidate; };
        using var cancellation = new CancellationTokenSource();
        var preparing = engine.PrepareModelAsync(context, Definition(), 2, cancellation.Token).AsTask();
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10)); cancellation.Cancel(); resume.TrySetResult();
            var error = await Assert.ThrowsAsync<AggregateException>(() => preparing);
            Assert.Contains(error.InnerExceptions, exception => exception is OperationCanceledException);
            Assert.Contains(error.InnerExceptions, exception => exception.Message == "Native disposal failed");
            Assert.False(candidate.Disposed);
            Assert.False(backend.Models[0].Disposed);
            await engine.ReleaseContextAsync(context, default);
            Assert.True(candidate.Disposed);
            Assert.Equal(2, candidate.DisposeCount);
        }
        finally
        {
            resume.TrySetResult();
            try { await preparing; } catch (AggregateException) { }
        }
    }

    [Theory]
    [InlineData(VisionModelKind.Template)]
    [InlineData(VisionModelKind.AiClassification)]
    [InlineData((VisionModelKind)999)]
    public async Task UnknownModelDefinition_IsRejectedBeforeEitherBackendPreparation(VisionModelKind kind)
    {
        var prepares = 0;
        var vision = new ProbeBackend("vision", VisionExecutionDomain.Vision);
        var ai = new ProbeBackend("ai", VisionExecutionDomain.Ai);
        ValueTask<IPreparedVisionModel> Prepare(VisionModelDefinition definition, CancellationToken token)
        {
            prepares++;
            return ValueTask.FromResult<IPreparedVisionModel>(new ProbeModel());
        }

        vision.Prepare = Prepare;
        ai.Prepare = Prepare;
        await using var engine = VisionEngineFactory.Create(new("vision", "ai", "cpu"),
            (domain, _) => domain == VisionExecutionDomain.Vision ? vision : ai);
        var context = await engine.CreateContextAsync(default);

        await Assert.ThrowsAsync<NotSupportedException>(() => engine.PrepareModelAsync(
            context, new UnknownDefinition(kind), 1, default).AsTask());
        Assert.Equal(0, prepares);
    }

    private sealed class UnknownDefinition(VisionModelKind kind) : VisionModelDefinition
    {
        public override VisionModelKind Kind => kind;
    }

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static VisionFrame Frame() => new(Guid.NewGuid(), Guid.NewGuid(), 4, 4, PixelFormat.Mono8, new byte[16]);
    private static TemplateModelDefinition Definition() => new(Frame(), new(), new());
    private static BackendOutput Output(VisionOperation operation)
    {
        var frame = operation.Image ?? throw new InvalidOperationException("This image fixture requires an input frame.");
        return operation switch
        {
            AiClassificationOperation => new(new AiClassificationOutput(frame.FrameId, frame.CoordinateSpaceId, [new("pass", 0.9)])),
            _ => new(new TemplateMatchOutput(frame.FrameId, frame.CoordinateSpaceId, "test", [new(new(1, 1), new(), 0, 1, 0.9)]))
        };
    }

    private sealed class ProbeModel : IPreparedVisionModel
    {
        internal bool Disposed;
        internal int DisposeCount;
        internal int FailDisposals;
        public void Dispose()
        {
            DisposeCount++;
            if (FailDisposals-- > 0) throw new InvalidOperationException("Native disposal failed");
            Disposed = true;
        }
    }

    private sealed class ProbeBackend(string id, VisionExecutionDomain domain) : IVisionBackend
    {
        public string Id => id;
        public VisionExecutionDomain Domain => domain;
        public IReadOnlyList<VisionOperationCapability> Capabilities { get; } =
            [new(domain == VisionExecutionDomain.Vision ? TemplateMatchOperation.Id : AiClassificationOperation.Id, domain, VisionOperationFeatures.None)];
        internal readonly List<ProbeModel> Models = [];
        internal int DisposeCount;
        internal int ExecuteCount;
        internal Func<VisionModelDefinition, CancellationToken, ValueTask<IPreparedVisionModel>>? Prepare;
        internal Func<IPreparedVisionModel?, VisionOperation, CancellationToken, ValueTask<BackendOutput>>? Execute;
        public IReadOnlyList<Vision.Core.Diagnostics.VisionIssue> Validate(VisionOperation operation, IPreparedVisionModel? model) => [];
        public ValueTask<IPreparedVisionModel> PrepareAsync(VisionModelDefinition definition, CancellationToken token)
        {
            if (Prepare != null) return Prepare(definition, token);
            var model = new ProbeModel(); Models.Add(model); return ValueTask.FromResult<IPreparedVisionModel>(model);
        }
        public ValueTask<BackendOutput> ExecuteAsync(IPreparedVisionModel? model, VisionOperation operation, CancellationToken token)
        {
            ExecuteCount++;
            Assert.False(((ProbeModel)model!).Disposed);
            return Execute?.Invoke(model, operation, token) ?? ValueTask.FromResult(Output(operation));
        }
        public void Dispose() { Assert.All(Models, model => Assert.True(model.Disposed)); DisposeCount++; }
    }
}
