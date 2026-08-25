using System.Diagnostics;
using FlowEditor.Core;
using FlowEditor.Models;
using FlowEditor.ViewModels;
using Xunit;
using static FlowEditorTest.TestGraph;

namespace FlowEditorTest;

/// <summary>CompiledFlowGraph 编译正确性与性能（P0-4 验收）。</summary>
public class CompiledFlowGraphTests
{
    [Fact]
    public void Compile_1000NodeChain_Under100ms()
    {
        const int N = 1000;
        var nodes = new List<NodeViewModel>(N);
        for (var i = 0; i < N; i++)
            nodes.Add(New<CountingNode>($"N{i}").Vm);

        var conns = new List<ConnectionModel>(N - 1);
        for (var i = 0; i < N - 1; i++)
            conns.Add(Flow(nodes[i], nodes[i + 1]));

        // 预热 JIT 后测量
        CompiledFlowGraph<NodeViewModel>.Compile([nodes[0], nodes[1]], []);
        var sw = Stopwatch.StartNew();
        var graph = CompiledFlowGraph<NodeViewModel>.Compile(nodes, conns);
        sw.Stop();

        Assert.Equal(N, graph.TopologicalOrder.Count);
        Assert.True(sw.ElapsedMilliseconds < 100, $"编译耗时 {sw.ElapsedMilliseconds}ms 应 < 100ms");
    }

    [Fact]
    public void Compile_LinearChain_ProducesValidTopologicalOrder()
    {
        var a = New<ValueNode>("A").Vm;
        var b = New<ValueNode>("B").Vm;
        var c = New<ValueNode>("C").Vm;
        var nodes = new List<NodeViewModel> { c, b, a }; // 故意乱序
        var conns = new List<ConnectionModel> { Flow(a, b), Flow(b, c) };

        var graph = CompiledFlowGraph<NodeViewModel>.Compile(nodes, conns);

        Assert.False(graph.HasCycle);
        Assert.Equal(["A", "B", "C"], graph.TopologicalOrder.Select(n => n.Name));
        var entry = Assert.Single(graph.EntryNodes);
        Assert.Equal("A", entry.Name);
    }

    [Fact]
    public void Compile_WithCycle_DetectsBackEdgeAndKeepsEntry()
    {
        var x = New<CountingNode>("X").Vm;
        var y = New<CountingNode>("Y").Vm;
        var nodes = new List<NodeViewModel> { x, y };
        var conns = new List<ConnectionModel> { Flow(x, y), Flow(y, x) };

        var graph = CompiledFlowGraph<NodeViewModel>.Compile(nodes, conns);

        Assert.True(graph.HasCycle);
        Assert.Single(graph.BackEdges);          // 恰好一条回边
        Assert.All(graph.EntryNodes, n => Assert.Contains(n, nodes)); // 环路下入口回退不抛异常
    }

    [Fact]
    public void IncomingConnections_GroupsByTarget_PreservingConnectionOrder()
    {
        var a = New<SourceNode>("A").Vm;
        var b = New<ValueNode>("B").Vm;
        var flowConn = Flow(a, b);
        var dataConn = Data(a, b);
        var conns = new List<ConnectionModel> { dataConn, flowConn };

        var graph = CompiledFlowGraph<NodeViewModel>.Compile([a, b], conns);

        var incoming = graph.IncomingConnections[b.Id];
        Assert.Equal(2, incoming.Count);
        Assert.Same(dataConn, incoming[0]);      // 保持原始连线顺序
        Assert.Same(flowConn, incoming[1]);
    }
}
