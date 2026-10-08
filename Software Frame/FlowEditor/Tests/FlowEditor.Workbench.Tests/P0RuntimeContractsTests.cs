// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：P0RuntimeContractsTests、StubCameraCount、StubServices、StubDisposable、ContextProbeNode、LegacyProbeNode。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls.Core、FlowEditor.Core、FlowEditor.Runtime、FlowEditor.Controls.Models、FlowEditor.Core.Models、FlowEditor.Controls.Nodes。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;
using FlowEditor.Controls.Models;
using FlowEditor.Core.Models;
using FlowEditor.Controls.Nodes;
using FlowEditor.Core.Nodes;
using Xunit;

namespace FlowEditor.Workbench.Tests;

/// <summary>P0 验收：NodeExecutionContext、FlowNodeException、资源生命周期的最小契约。</summary>
public class P0RuntimeContractsTests
{
    /// <summary>
    /// 测试相关入口 Context_CanBeBuilt_InUnitTest_WithoutWpf；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 CreateTest、Equal、NotNull、Null、ThrowIfCancellationRequested；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 Context_Variables_And_Services_AreSafe；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 CreateTest、Equal、GetVariable、NotNull、GetService；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 FlowNodeException_Carries_StableCode_And_Category；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 DeviceOffline、Equal、True、Config、False；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 ResourceLease_Owned_Disposes_But_Borrowed_DoesNot；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Dispose、True、False；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 ResourceScope_CleansUp_OnDispose；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Track、Equal、Dispose、True；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 Engine_Prefers_ContextContract_Over_Legacy；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 ExecuteAsync、True、False、Equal；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 Engine_LegacyNode_StillRuns_WithoutChanges；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 ExecuteAsync、True、Equal；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 Engine_Cancellation_DoesNotRetry_And_FailsFast；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Cancel、ThrowsAnyAsync、ExecuteAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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

    /// <summary>
    /// StubCameraCount 的类型定义。FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// </summary>
    private sealed class StubCameraCount(int n)
    {
        public int Count => n;
    }

    /// <summary>
    /// StubServices 的类型定义。FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：IServiceProvider。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class StubServices(params object[] services) : IServiceProvider
    {
        private readonly object[] _services = services;
        /// <summary>
        /// 测试相关入口 GetService；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 FirstOrDefault、IsInstanceOfType；具体条件和先后次序见方法体。
        /// </summary>
        public object? GetService(Type serviceType) =>
            _services.FirstOrDefault(s => serviceType.IsInstanceOfType(s));
    }

    /// <summary>
    /// StubDisposable 的类型定义。FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：IDisposable。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class StubDisposable : IDisposable
    {
        public bool Disposed { get; private set; }
        /// <summary>
        /// 测试相关入口 Dispose；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public void Dispose() => Disposed = true;
    }

    /// <summary>
    /// ContextProbeNode 的类型定义。FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：INodeExecutable、INodeContextExecutable。具体约束按这些契约及各成员实现解释。
    /// </summary>
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
        /// <summary>
        /// 测试相关入口 FindPortById；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public PortModel? FindPortById(string portId) => null;
        /// <summary>
        /// 测试相关入口 ExecuteAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public Task ExecuteAsync(CancellationToken ct) { LegacyUsed = true; return Task.CompletedTask; }
        /// <summary>
        /// 测试相关入口 ExecuteAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 ThrowIfNull、ThrowIfCancellationRequested、LogInfo；具体条件和先后次序见方法体。
        /// </summary>
        public Task ExecuteAsync(NodeExecutionContext ctx)
        {
            ArgumentNullException.ThrowIfNull(ctx);
            ctx.ThrowIfCancellationRequested();
            ContextUsed = true;
            ctx.Logger.LogInfo("probe");
            return Task.CompletedTask;
        }
        /// <summary>
        /// 测试相关入口 SyncInputValues；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public void SyncInputValues() { }
        /// <summary>
        /// 测试相关入口 SyncOutputValues；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public void SyncOutputValues() { }
        /// <summary>
        /// 测试相关入口 ResetActivation；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public void ResetActivation() { }
        /// <summary>
        /// 测试相关入口 GetActivatedOutputNames；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public IReadOnlyCollection<string>? GetActivatedOutputNames() => null;
    }

    /// <summary>
    /// LegacyProbeNode 的类型定义。FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：INodeExecutable。具体约束按这些契约及各成员实现解释。
    /// </summary>
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
        /// <summary>
        /// 测试相关入口 FindPortById；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public PortModel? FindPortById(string portId) => null;
        /// <summary>
        /// 测试相关入口 ExecuteAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public Task ExecuteAsync(CancellationToken ct) { Executed = true; return Task.CompletedTask; }
        /// <summary>
        /// 测试相关入口 SyncInputValues；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public void SyncInputValues() { }
        /// <summary>
        /// 测试相关入口 SyncOutputValues；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public void SyncOutputValues() { }
        /// <summary>
        /// 测试相关入口 ResetActivation；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public void ResetActivation() { }
        /// <summary>
        /// 测试相关入口 GetActivatedOutputNames；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public IReadOnlyCollection<string>? GetActivatedOutputNames() => null;
    }
}
