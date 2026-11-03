using System.IO;
using Prism.DryIoc;
using Prism.Ioc;
using Vision.Controls;
using Vision.Core;
using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Models;
using Vision.Core.Results;

namespace Vision.Workbench.Tests.Controls;

public sealed class VisionEngineCompositionTests
{
    [Fact]
    public void Configuration_IsExplicitFrozenAndStrict_WithoutSdkInitialization()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            File.WriteAllText(path, "{\"visionBackendId\":\"halcon\",\"aiBackendId\":\"halcon\",\"aiDeviceId\":\"cpu\"}");
            var frozen = VisionEngineConfiguration.Load(path);
            File.WriteAllText(path, "{\"visionBackendId\":\"opencv\"}");
            Assert.Equal("halcon", frozen.VisionBackendId); Assert.Equal("cpu", frozen.AiDeviceId);
            Assert.Equal("opencv", VisionEngineConfiguration.Load(path).VisionBackendId);
            foreach (var invalid in new[] { "{}", "{\"visionBackendId\":\"opencv\",\"typo\":1}",
                         "{\"visionBackendId\":\"opencv\",\"visionBackendId\":\"halcon\"}" })
            {
                File.WriteAllText(path, invalid);
                Assert.Throws<FormatException>(() => VisionEngineConfiguration.Load(path));
            }
            File.Delete(path);
            Assert.Throws<FileNotFoundException>(() => VisionEngineConfiguration.Load(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Registration_IsLazyUnique_RejectsDuplicateAndUnknownSelection_AndClosesOnce()
    {
        var container = new DryIocContainerExtension();
        using var containerLifetime = container.Instance;
        var created = 0; var engine = new ProbeEngine();
        var owner = VisionEngineRegistration.RegisterVisionEngine(container, new("opencv"), selection =>
        { Assert.Equal("opencv", selection.VisionBackendId); created++; return engine; });
        try
        {
            Assert.Equal(0, created);
            Assert.Throws<InvalidOperationException>(() => container.RegisterVisionEngine(new("halcon")));
            Assert.Same(engine, container.Resolve<IVisionEngine>());
            Assert.Same(engine, container.Resolve<IVisionEngine>());
            Assert.Equal(1, created);
            await owner.DisposeAsync(); await owner.DisposeAsync();
            Assert.Equal(1, engine.Disposals);
            var error = Record.Exception(() => container.Resolve<IVisionEngine>());
            Assert.IsType<ObjectDisposedException>(error!.GetBaseException());
        }
        finally { await owner.DisposeAsync(); }
        var other = new DryIocContainerExtension();
        using var otherLifetime = other.Instance;
        Assert.Throws<NotSupportedException>(() => VisionEngineRegistration.RegisterVisionEngine(other, new("unknown"), _ =>
        { created++; return engine; }));
        Assert.Equal(1, created);
    }

    [Fact]
    public async Task ClosingUnusedRegistration_DoesNotInitializeAnySdk()
    {
        var container = new DryIocContainerExtension();
        using var containerLifetime = container.Instance;
        var calls = 0;
        var owner = VisionEngineRegistration.RegisterVisionEngine(container, new("halcon"), _ =>
        { calls++; throw new InvalidOperationException("Must not initialize"); });
        await owner.DisposeAsync(); await owner.DisposeAsync();
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task CloseDuringActivation_WaitsForRealFactoryReturn_AndDoesNotPublishLateEngine()
    {
        var container = new DryIocContainerExtension();
        using var containerLifetime = container.Instance;
        using var resume = new ManualResetEventSlim();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var engine = new ProbeEngine();
        var owner = VisionEngineRegistration.RegisterVisionEngine(container, new("opencv"), _ =>
        {
            started.TrySetResult();
            if (!resume.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Test activation not resumed");
            return engine;
        });
        var creating = Task.Run(() => container.Resolve<IVisionEngine>());
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var closing = owner.DisposeAsync().AsTask();
            Assert.False(closing.IsCompleted); Assert.Equal(0, engine.Disposals);
            resume.Set();
            var error = await Record.ExceptionAsync(async () => { await creating; });
            Assert.IsType<ObjectDisposedException>(error!.GetBaseException());
            await closing.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(1, engine.Disposals);
        }
        finally
        {
            resume.Set();
            try { await creating; } catch (Exception) { /* 上面精确断言根异常；这里只确保工厂真正退出。 */ }
            await owner.DisposeAsync();
        }
    }

    private sealed class ProbeEngine : IVisionEngine
    {
        internal int Disposals;
        public VisionEngineDescriptor Descriptor { get; } = new(Guid.NewGuid(), new("opencv"), []);
        public ValueTask<VisionContext> CreateContextAsync(CancellationToken token) => throw new NotSupportedException();
        public ValueTask<IReadOnlyList<VisionIssue>> ValidateAsync(VisionContext context, VisionOperation operation, CancellationToken token) => throw new NotSupportedException();
        public ValueTask<VisionModelReference> PrepareModelAsync(VisionContext context, VisionModelDefinition definition, long revision, CancellationToken token) => throw new NotSupportedException();
        public ValueTask<VisionOutcome<TResult>> ExecuteAsync<TResult>(VisionContext context, VisionOperation<TResult> operation, CancellationToken token) where TResult : class => throw new NotSupportedException();
        public ValueTask ReleaseContextAsync(VisionContext context, CancellationToken token) => throw new NotSupportedException();
        public ValueTask DisposeAsync() { Disposals++; return ValueTask.CompletedTask; }
    }
}
