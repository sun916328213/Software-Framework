using FlowEditor.Core;
using FlowEditor.Models;
using FlowEditor.Nodes;
using FlowEditor.ViewModels;
using Xunit;
using static FlowEditorTest.TestGraph;

namespace FlowEditorTest;

/// <summary>执行引擎三种模式的行为回归（P0 重构验收）。</summary>
public class ExecutionEngineTests
{
    private readonly FlowChartExecutionEngine _engine = new();

    // ── DAG 模式 ──

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

    // ── 调试串行模式 ──

    [Fact]
    public async Task SequentialDebug_ExecutesTopologicallyWithStepCallback()
    {
        var a = New<ValueNode>("A").Vm;
        var b = New<ValueNode>("B").Vm;
        var nodes = new List<NodeViewModel> { b, a }; // 故意乱序传入
        var conns = new List<ConnectionModel> { Flow(a, b) };

        var steps = new List<string>();
        await _engine.ExecuteAsync(nodes, conns, CancellationToken.None, _ => { },
            onBeforeNode: n => { steps.Add(n.Name); return Task.CompletedTask; });

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

    [Fact]
    public async Task Execute_EmptyCanvas_LogsAndReturns()
    {
        var logs = new List<string>();
        await _engine.ExecuteAsync<NodeViewModel>([], [], CancellationToken.None, logs.Add);

        var entry = Assert.Single(logs);
        Assert.Contains("画布为空", entry);
    }
}
