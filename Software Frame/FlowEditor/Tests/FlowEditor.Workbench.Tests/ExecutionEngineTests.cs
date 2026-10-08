// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ExecutionEngineTests、TrackingLogContext、Scope、ConcurrencyProbe、SynchronousWorkNode、ControlledNode。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls.Core、FlowEditor.Core、FlowEditor.Runtime、FlowEditor.Controls.Models、FlowEditor.Core.Models、FlowEditor.Controls.Nodes。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;
using FlowEditor.Controls.Models;
using FlowEditor.Core.Models;
using FlowEditor.Controls.Nodes;
using FlowEditor.Core.Nodes;
using FlowEditor.Controls.ViewModels;
using Xunit;
using static FlowEditor.Workbench.Tests.TestGraph;

namespace FlowEditor.Workbench.Tests;

/// <summary>执行引擎三种模式的行为回归（P0 重构验收）。</summary>
public class ExecutionEngineTests
{
    private readonly TestFlowChartExecutionEngine _engine = new();

    /// <summary>
    /// TrackingLogContext 的类型定义。FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：IExecutionLogContext。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class TrackingLogContext : IExecutionLogContext
    {
        /// <summary>
        /// Scope 的类型定义。FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// 继承或实现的契约：IDisposable。具体约束按这些契约及各成员实现解释。
        /// </summary>
        private sealed class Scope(Action onDispose) : IDisposable
        {
            /// <summary>记录节点日志作用域已经退出。</summary>
            public void Dispose() => onDispose();
        }

        /// <inheritdoc />
        public long RunId => 42;

        /// <summary>记录引擎进入过的节点名称。</summary>
        public List<string?> EnteredNodes { get; } = [];

        /// <summary>记录已正常退出的节点作用域数量。</summary>
        public int ExitedScopes { get; private set; }

        /// <summary>记录控制流回边推进次数。</summary>
        public int BumpCount { get; private set; }

        /// <inheritdoc />
        /// <remarks>
        /// 测试相关入口 EnterNode；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Add；具体条件和先后次序见方法体。
        /// </remarks>
        public IDisposable EnterNode(string? nodeName)
        {
            EnteredNodes.Add(nodeName);
            return new Scope(() => ExitedScopes++);
        }

        /// <inheritdoc />
        /// <remarks>
        /// 测试相关入口 BumpRun；准备条件、协作调用与结果检查以方法正文为准。
        /// </remarks>
        public void BumpRun() => BumpCount++;
    }

    /// <summary>
    /// ConcurrencyProbe 的类型定义。FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：IDisposable。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class ConcurrencyProbe : IDisposable
    {
        private readonly Barrier _rendezvous = new(2);
        private int _active;
        public int MaxActive;

        /// <summary>
        /// 测试相关入口 Enter；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Increment、CompareExchange；具体条件和先后次序见方法体。
        /// </summary>
        public void Enter()
        {
            var active = Interlocked.Increment(ref _active);
            int observed;
            do
            {
                observed = MaxActive;
                if (active <= observed) break;
            } while (Interlocked.CompareExchange(ref MaxActive, active, observed) != observed);
        }

        /// <summary>
        /// 测试相关入口 Exit；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Decrement；具体条件和先后次序见方法体。
        /// </summary>
        public void Exit() => Interlocked.Decrement(ref _active);

        /// <summary>
        /// 测试相关入口 WaitForPeer；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 SignalAndWait、FromSeconds；具体条件和先后次序见方法体。
        /// </summary>
        public void WaitForPeer(CancellationToken token)
        {
            if (!_rendezvous.SignalAndWait(TimeSpan.FromSeconds(10), token))
                throw new TimeoutException("The engine did not schedule the second synchronous branch.");
        }

        /// <summary>
        /// 测试相关入口 Dispose；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Dispose；具体条件和先后次序见方法体。
        /// </summary>
        public void Dispose() => _rendezvous.Dispose();
    }

    /// <summary>
    /// SynchronousWorkNode 的类型定义。FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：NodeModel。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class SynchronousWorkNode(ConcurrencyProbe probe) : NodeModel
    {
        /// <summary>
        /// 测试相关入口 ExecuteAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Enter、WaitForPeer、Exit；具体条件和先后次序见方法体。
        /// </summary>
        public override Task ExecuteAsync(CancellationToken ct)
        {
            probe.Enter();
            try
            {
                probe.WaitForPeer(ct);
                return Task.CompletedTask;
            }
            finally
            {
                probe.Exit();
            }
        }
    }

    /// <summary>
    /// ControlledNode 的类型定义。FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：NodeModel。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class ControlledNode : NodeModel
    {
        private readonly TaskCompletionSource _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 测试相关入口 ExecuteAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 TrySetResult、WaitAsync；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        public override async Task ExecuteAsync(CancellationToken ct)
        {
            Started.TrySetResult();
            await _release.Task.WaitAsync(ct);
        }

        /// <summary>
        /// 测试相关入口 Complete；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 TrySetResult；具体条件和先后次序见方法体。
        /// </summary>
        public void Complete() => _release.TrySetResult();
    }

    // ── DAG 模式 ──

    [Fact]
    public async Task Dag_ConcurrentRuns_KeepCancellationAndOrderIsolated()
    {
        var first = new ControlledNode { Name = "First" };
        var second = new ControlledNode { Name = "Second" };
        var firstVm = new NodeViewModel(first);
        var secondVm = new NodeViewModel(second);
        var order = new List<NodeViewModel>();
        using var cancellation = new CancellationTokenSource();
        var firstRun = _engine.ExecuteAsync([firstVm], [], cancellation.Token, _ => { });
        var secondRun = _engine.ExecuteAsync([secondVm], [], CancellationToken.None, _ => { }, executionOrder: order);
        try
        {
            await Task.WhenAll(first.Started.Task, second.Started.Task).WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => firstRun.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.False(secondRun.IsCompleted);
            second.Complete();
            await secondRun.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal([secondVm], order);
            Assert.Equal(NodeStatus.Success, secondVm.Status);
        }
        finally
        {
            cancellation.Cancel();
            first.Complete();
            second.Complete();
        }
    }

    [Fact]
    public async Task Dag_InjectedLogContext_IsUsedWithoutRuntimeDependingOnDesktopLogger()
    {
        var context = new TrackingLogContext();
        var engine = new FlowChartExecutionEngine(context);
        var node = new ValueNode { Name = "独立运行节点" };
        var logs = new List<string>();

        await engine.ExecuteAsync(
            new List<NodeModel> { node },
            new List<ConnectionModel>(),
            CancellationToken.None,
            logs.Add);

        Assert.Equal([node.Name], context.EnteredNodes);
        Assert.Equal(1, context.ExitedScopes);
        Assert.Contains(logs, message => message.StartsWith("#42 [独立运行节点]", StringComparison.Ordinal));
    }

    /// <summary>
    /// 测试相关入口 Dag_Chain_ExecutesInTopologicalOrderAndPropagatesData；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 New、Flow、Data、ExecuteAsync、Equal；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Dag_Chain_ExecutesInTopologicalOrderAndPropagatesData()
    {
        var (a, aModel) = New<SourceNode>("A");
        var (b, _) = New<ValueNode>("B");
        var (c, cModel) = New<ValueNode>("C");
        var nodes = new List<NodeViewModel> { a, b, c };
        var conns = new List<ConnectionModel> { Flow(a, b), Flow(b, c), Data(a, b), Data(b, c) };

        var order = new List<NodeViewModel>();
        await _engine.ExecuteAsync(nodes, conns, CancellationToken.None, _ => { }, executionOrder: order);

        Assert.Equal(["A", "B", "C"], order.Select(n => n.Name));
        Assert.Equal(102, cModel.Value); // A=100 → B=101 → C=102
        Assert.All(nodes, n => Assert.Equal(NodeStatus.Success, n.Status));
    }

    /// <summary>
    /// 测试相关入口 Dag_NodeFails_DownstreamSkippedButIndependentBranchRuns；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 New、Flow、ThrowsAsync、ExecuteAsync、Equal；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Dag_NodeFails_DownstreamSkippedButIndependentBranchRuns()
    {
        var a = New<ValueNode>("A").Vm;
        var fail = New<FailNode>("F").Vm;
        var downstream = New<ValueNode>("C").Vm;   // F 的下游，应被跳过
        var independent = New<ValueNode>("D").Vm;  // 独立分支，应正常执行
        var nodes = new List<NodeViewModel> { a, fail, downstream, independent };
        var conns = new List<ConnectionModel>
        {
            Flow(a, fail),
            Flow(fail, downstream),
            Flow(a, independent),
        };

        await Assert.ThrowsAsync<AggregateException>(
            () => _engine.ExecuteAsync(nodes, conns, CancellationToken.None, _ => { }));

        Assert.Equal(NodeStatus.Error, fail.Status);
        Assert.Equal(NodeStatus.Idle, downstream.Status);  // 跳过不执行
        Assert.Equal(NodeStatus.Success, independent.Status);
    }

    /// <summary>
    /// 测试相关入口 Dag_OneOutputToTwoSynchronousTools_RunsBothBranchesConcurrently；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 New、ExecuteAsync、Flow、Equal；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Dag_OneOutputToTwoSynchronousTools_RunsBothBranchesConcurrently()
    {
        var start = New<ValueNode>("Start").Vm;
        using var probe = new ConcurrencyProbe();
        var left = new NodeViewModel(new SynchronousWorkNode(probe) { Name = "Left" });
        var right = new NodeViewModel(new SynchronousWorkNode(probe) { Name = "Right" });

        await _engine.ExecuteAsync(
            new List<NodeViewModel> { start, left, right },
            new List<ConnectionModel> { Flow(start, left), Flow(start, right) },
            CancellationToken.None,
            _ => { });

        Assert.Equal(2, probe.MaxActive);
    }

    /// <summary>
    /// 测试相关入口 Dag_TwoActiveBranchesRejoin_WaitsForBothBeforeRunningJoin；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 New、ExecuteAsync、Flow、WaitAsync、FromSeconds；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Dag_TwoActiveBranchesRejoin_WaitsForBothBeforeRunningJoin()
    {
        var start = New<ValueNode>("Start").Vm;
        var slowModel = new ControlledNode { Name = "Slow" };
        var slow = new NodeViewModel(slowModel);
        var (fast, fastModel) = New<CountingNode>("Fast");
        var (join, joinModel) = New<CountingNode>("Join");
        var runTask = _engine.ExecuteAsync(
            new List<NodeViewModel> { start, slow, fast, join },
            new List<ConnectionModel>
            {
                Flow(start, slow), Flow(start, fast),
                Flow(slow, join), Flow(fast, join)
            },
            CancellationToken.None,
            _ => { });

        await slowModel.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await WaitUntilAsync(() => Volatile.Read(ref fastModel.Executions) == 1);
        Assert.Equal(0, Volatile.Read(ref joinModel.Executions));

        slowModel.Complete();
        await runTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(1, joinModel.Executions);
    }

    // ── 环路模式 ──

    [Fact]
    public async Task Cycle_RepeatsUntilCancelled()
    {
        var (x, xModel) = New<CountingNode>("X");
        var (y, yModel) = New<CountingNode>("Y");
        var nodes = new List<NodeViewModel> { x, y };
        var conns = new List<ConnectionModel> { Flow(x, y), Flow(y, x) }; // 回边成环

        using var cts = new CancellationTokenSource(300);
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _engine.ExecuteAsync(nodes, conns, cts.Token, _ => { }));

        Assert.True(xModel.Executions >= 2, $"X 应被重复调度（实际 {xModel.Executions} 次）");
        Assert.True(yModel.Executions >= 2, $"Y 应被重复调度（实际 {yModel.Executions} 次）");
    }

    /// <summary>
    /// 测试相关入口 Cycle_ForkedBranchesRemainConcurrentBeforeAndMerge；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 New、Flow、ToMergeInput、ThrowsAnyAsync、ExecuteAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Cycle_ForkedBranchesRemainConcurrentBeforeAndMerge()
    {
        var (start, _) = New<CountingNode>("Start");
        var (fork, _) = New<CountingNode>("Fork");
        using var probe = new ConcurrencyProbe();
        var left = new NodeViewModel(new SynchronousWorkNode(probe) { Name = "Left" });
        var right = new NodeViewModel(new SynchronousWorkNode(probe) { Name = "Right" });
        var merge = new NodeViewModel(new FlowEditor.Controls.Nodes.FlowControl.BranchMergeNode
        {
            Name = "Merge",
            MergeOperator = FlowEditor.Controls.Nodes.FlowControl.BranchMergeOperator.AND
        });
        var connections = new List<ConnectionModel>
        {
            Flow(start, fork),
            Flow(fork, left),
            Flow(fork, right),
            ToMergeInput(left, merge, "分支1"),
            ToMergeInput(right, merge, "分支2"),
            Flow(merge, fork)
        };

        using var cts = new CancellationTokenSource(650);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _engine.ExecuteAsync(
                [start, fork, left, right, merge], connections, cts.Token, _ => { }));

        Assert.Equal(2, probe.MaxActive);
    }

    // ── 调试串行模式 ──

    [Fact]
    public async Task SequentialDebug_ExecutesTopologicallyWithStepCallback()
    {
        var (aVm, a) = New<ValueNode>("A");
        var (bVm, b) = New<ValueNode>("B");
        var nodes = new List<NodeModel> { b, a }; // 故意乱序传入
        var conns = new List<ConnectionModel> { Flow(aVm, bVm) };

        var steps = new List<string>();
        await _engine.ExecuteAsync(nodes, conns, CancellationToken.None, _ => { },
            onBeforeNode: nodeId =>
            {
                steps.Add(nodes.Single(node => node.Id == nodeId).Name);
                return Task.CompletedTask;
            });

        Assert.Equal(["A", "B"], steps);
    }

    // ── 公共 API 兼容 ──

    [Fact]
    public void PublicApi_HasCycleAndTopologicalSort_BehaviorPreserved()
    {
        var a = New<ValueNode>("A").Vm;
        var b = New<ValueNode>("B").Vm;
        var nodes = new List<NodeViewModel> { a, b };
        var conns = new List<ConnectionModel> { Flow(a, b) };

        Assert.False(_engine.HasCycle(nodes, conns));
        Assert.True(_engine.HasCycle([a, b], [Flow(b, a), Flow(a, b)]));

        var sorted = _engine.TopologicalSort(nodes, conns);
        Assert.Equal(["A", "B"], sorted.Select(n => n.Name));

        var layers = _engine.TopologicalLayers(nodes, conns);
        Assert.Equal(2, layers.Count);
        Assert.Equal("A", layers[0][0].Name);
        Assert.Equal("B", layers[1][0].Name);
    }

    /// <summary>
    /// 测试相关入口 Execute_EmptyCanvas_LogsAndReturns；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 ExecuteAsync、Single、Contains；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Execute_EmptyCanvas_LogsAndReturns()
    {
        var logs = new List<string>();
        await _engine.ExecuteAsync<NodeModel>([], [], CancellationToken.None, logs.Add);

        var entry = Assert.Single(logs);
        Assert.Contains("画布为空", entry);
    }

    /// <summary>
    /// 测试相关入口 WaitUntilAsync；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 FromSeconds、condition、Delay；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (!condition())
            await Task.Delay(10, cts.Token);
    }

    /// <summary>
    /// 测试相关入口 ToMergeInput；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 First；具体条件和先后次序见方法体。
    /// </summary>
    private static ConnectionModel ToMergeInput(
        NodeViewModel source, NodeViewModel merge, string inputName)
        => new()
        {
            SourceNodeId = source.Id,
            SourcePortId = source.OutputPorts.First(port => port.Name == "Next_Out").Id,
            TargetNodeId = merge.Id,
            TargetPortId = merge.InputPorts.First(port => port.Name == inputName).Id
        };
}
