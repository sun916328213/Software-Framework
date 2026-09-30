using FlowEditor.Core;
using FlowEditor.Models;
using FlowEditor.Nodes;
using Xunit;

namespace FlowEditorTest;

/// <summary>P0 验收：NodeExecutionContext、FlowNodeException、资源生命周期的最小契约。</summary>
public class P0RuntimeContractsTests
{
    [Fact]
    public void Context_CanBeBuilt_InUnitTest_WithoutWpf()
    {
        var ctx = NodeExecutionContext.CreateTest("n1");
        Assert.Equal("n1", ctx.NodeId);
        Assert.NotNull(ctx.Logger);
        Assert.NotNull(ctx.Telemetry);
        Assert.Null(ctx.Services);
        ctx.ThrowIfCancellationRequested();

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var cancelled = NodeExecutionContext.CreateTest("n2", cts.Token);
        Assert.Throws<OperationCanceledException>(cancelled.ThrowIfCancellationRequested);
    }

    [Fact]
    public void Context_Variables_And_Services_AreSafe()
    {
        var services = new StubServices(new StubCameraCount(3));
        var ctx = NodeExecutionContext.CreateTest(
            "n1",
            variables: new Dictionary<string, object?> { ["Exposure"] = 20, ["Name"] = "Cam01" },
            services: services);
        Assert.Equal(20, ctx.GetVariable<int>("Exposure"));
        Assert.Equal("Cam01", ctx.GetVariable<string>("Name"));
        Assert.Equal(-1, ctx.GetVariable("Missing", -1));
        Assert.NotNull(ctx.GetService<StubCameraCount>());
        Assert.Null(ctx.GetService<IDisposable>());
    }

    [Fact]
    public void FlowNodeException_Carries_StableCode_And_Category()
    {
        var ex = FlowNodeException.DeviceOffline("相机掉线", nodeId: "cam-node");
        Assert.Equal("E-DEVICE-OFFLINE", ex.ErrorCode);
        Assert.Equal(NodeErrorCategory.DeviceOffline, ex.Category);
        Assert.True(ex.IsRetryable);
        Assert.Equal("cam-node", ex.NodeId);

        var cfg = FlowNodeException.Config("E-CAMERA-NAME-EMPTY", "未配置相机名称。");
        Assert.False(cfg.IsRetryable);

        var cancelled = FlowNodeException.Cancelled();
        Assert.Equal(NodeErrorCategory.Cancelled, cancelled.Category);
        Assert.False(cancelled.IsRetryable);

        var normalized = FlowNodeException.FromUnknown(new InvalidOperationException("boom"), "n");
        Assert.Equal("E-UNKNOWN", normalized.ErrorCode);
        Assert.Equal(NodeErrorCategory.Unknown, normalized.Category);

        var timeout = FlowNodeException.FromUnknown(new NodeTimeoutException("超时"), "n");
        Assert.Equal(NodeErrorCategory.Timeout, timeout.Category);
        Assert.True(timeout.IsRetryable);
    }

    [Fact]
    public void ResourceLease_Owned_Disposes_But_Borrowed_DoesNot()
    {
        var ownedInner = new StubDisposable();
        var borrowedInner = new StubDisposable();
        var owned = new NodeResourceLease<StubDisposable>("Camera:Cam01", ownedInner, NodeResourceOwnership.Owned);
        var borrowed = new NodeResourceLease<StubDisposable>("Camera:Cam01", borrowedInner, NodeResourceOwnership.Borrowed);
        owned.Dispose();
        borrowed.Dispose();
        Assert.True(ownedInner.Disposed);
        Assert.False(borrowedInner.Disposed);
        Assert.True(owned.IsDisposed);
    }

    [Fact]
    public void ResourceScope_CleansUp_OnDispose()
    {
        var scope = new NodeResourceScope();
        var a = scope.Track(new StubDisposable());
        var b = scope.Track(new NodeResourceLease<StubDisposable>("VisionTool:Blob01", new StubDisposable(), NodeResourceOwnership.Owned));
        Assert.Equal(2, scope.Count);
        scope.Dispose();
        Assert.True(a.Disposed);
        Assert.True(b.IsDisposed);
        Assert.Equal(0, scope.Count);
    }

    [Fact]
    public async Task Engine_Prefers_ContextContract_Over_Legacy()
    {
        var node = new ContextProbeNode();
        var engine = new FlowChartExecutionEngine();
        var nodes = new List<INodeExecutable> { node };
        await engine.ExecuteAsync(nodes, [], CancellationToken.None, _ => { });
        Assert.True(node.ContextUsed);
        Assert.False(node.LegacyUsed);
        Assert.Equal(NodeStatus.Success, node.Status);
    }

    [Fact]
    public async Task Engine_LegacyNode_StillRuns_WithoutChanges()
    {
        var node = new LegacyProbeNode();
        var engine = new FlowChartExecutionEngine();
        var nodes = new List<INodeExecutable> { node };
        await engine.ExecuteAsync(nodes, [], CancellationToken.None, _ => { });
        Assert.True(node.Executed);
        Assert.Equal(NodeStatus.Success, node.Status);
    }

    [Fact]
    public async Task Engine_Cancellation_DoesNotRetry_And_FailsFast()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var node = new LegacyProbeNode();
        var engine = new FlowChartExecutionEngine();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            engine.ExecuteAsync(new List<INodeExecutable> { node }, [], cts.Token, _ => { }));
    }

    private sealed class StubCameraCount(int n)
    {
        public int Count => n;
    }

    private sealed class StubServices(params object[] services) : IServiceProvider
    {
        private readonly object[] _services = services;
        public object? GetService(Type serviceType) =>
            _services.FirstOrDefault(s => serviceType.IsInstanceOfType(s));
    }

    private sealed class StubDisposable : IDisposable
    {
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }

    private sealed class ContextProbeNode : INodeExecutable, INodeContextExecutable
    {
        public bool ContextUsed { get; private set; }
        public bool LegacyUsed { get; private set; }
        public string Id { get; } = "ctx-probe";
        public string Name => "ContextProbe";
        public NodeStatus Status { get; set; }
        public long LastElapsedMs { get; set; }
        public int Count { get; set; }
        public int CurrentRunCount { get; set; }
        public int RunSequence { get; set; }
        public bool IsExcludedFromExecution { get; set; }
        public string? ParentExecutionContainerId { get; set; }
        public string? ExecutionLaneId { get; set; }
        public IReadOnlyList<PortModel> InputPorts => [];
        public IReadOnlyList<PortModel> OutputPorts => [];
        public IReadOnlyList<string>? ControlFlowPorts => null;
        public FlowJoinMode FlowJoinMode => FlowJoinMode.WaitAllAnyActive;
        public PortModel? FindPortById(string portId) => null;
        public Task ExecuteAsync(CancellationToken ct) { LegacyUsed = true; return Task.CompletedTask; }
        public Task ExecuteAsync(NodeExecutionContext ctx)
        {
            ArgumentNullException.ThrowIfNull(ctx);
            ctx.ThrowIfCancellationRequested();
            ContextUsed = true;
            ctx.Logger.LogInfo("probe");
            return Task.CompletedTask;
        }
        public void SyncInputValues() { }
        public void SyncOutputValues() { }
        public void ResetActivation() { }
        public IReadOnlyCollection<string>? GetActivatedOutputNames() => null;
    }

    private sealed class LegacyProbeNode : INodeExecutable
    {
        public bool Executed { get; private set; }
        public string Id { get; } = "legacy-probe";
        public string Name => "LegacyProbe";
        public NodeStatus Status { get; set; }
        public long LastElapsedMs { get; set; }
        public int Count { get; set; }
        public int CurrentRunCount { get; set; }
        public int RunSequence { get; set; }
        public bool IsExcludedFromExecution { get; set; }
        public string? ParentExecutionContainerId { get; set; }
        public string? ExecutionLaneId { get; set; }
        public IReadOnlyList<PortModel> InputPorts => [];
        public IReadOnlyList<PortModel> OutputPorts => [];
        public IReadOnlyList<string>? ControlFlowPorts => null;
        public FlowJoinMode FlowJoinMode => FlowJoinMode.WaitAllAnyActive;
        public PortModel? FindPortById(string portId) => null;
        public Task ExecuteAsync(CancellationToken ct) { Executed = true; return Task.CompletedTask; }
        public void SyncInputValues() { }
        public void SyncOutputValues() { }
        public void ResetActivation() { }
        public IReadOnlyCollection<string>? GetActivatedOutputNames() => null;
    }
}
