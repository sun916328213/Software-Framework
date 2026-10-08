// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：FlowGraphPerformanceCollection、CompiledFlowGraphTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Diagnostics、FlowEditor.Controls.Core、FlowEditor.Core、FlowEditor.Runtime、FlowEditor.Controls.Models、FlowEditor.Core.Models。导入不代表所有类型都会在运行时加载。
using System.Diagnostics;
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;
using FlowEditor.Controls.Models;
using FlowEditor.Core.Models;
using FlowEditor.Controls.ViewModels;
using Xunit;
using static FlowEditor.Workbench.Tests.TestGraph;

namespace FlowEditor.Workbench.Tests;

/// <summary>需要独占进程负载以保持阈值可比性的性能测试集合。</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class FlowGraphPerformanceCollection
{
    public const string Name = "FlowGraphPerformance";
}

/// <summary>CompiledFlowGraph 编译正确性与性能（P0-4 验收）。</summary>
[Collection(FlowGraphPerformanceCollection.Name)]
public class CompiledFlowGraphTests
{
    /// <summary>
    /// 测试相关入口 Compile_1000NodeChain_Under100ms；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Add、New、Flow、Compile、StartNew；具体条件和先后次序见方法体。
    /// </summary>
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
        TestGraph.Compile([nodes[0], nodes[1]], []);
        var sw = Stopwatch.StartNew();
        var graph = TestGraph.Compile(nodes, conns);
        sw.Stop();

        Assert.Equal(N, graph.TopologicalOrder.Count);
        Assert.True(sw.ElapsedMilliseconds < 100, $"编译耗时 {sw.ElapsedMilliseconds}ms 应 < 100ms");
    }

    /// <summary>
    /// 测试相关入口 Compile_LinearChain_ProducesValidTopologicalOrder；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 New、Flow、Compile、False、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Compile_LinearChain_ProducesValidTopologicalOrder()
    {
        var a = New<ValueNode>("A").Vm;
        var b = New<ValueNode>("B").Vm;
        var c = New<ValueNode>("C").Vm;
        var nodes = new List<NodeViewModel> { c, b, a }; // 故意乱序
        var conns = new List<ConnectionModel> { Flow(a, b), Flow(b, c) };

        var graph = TestGraph.Compile(nodes, conns);

        Assert.False(graph.HasCycle);
        Assert.Equal(["A", "B", "C"], graph.TopologicalOrder.Select(n => n.Name));
        var entry = Assert.Single(graph.EntryNodes);
        Assert.Equal("A", entry.Name);
    }

    /// <summary>
    /// 测试相关入口 Compile_WithCycle_DetectsBackEdgeAndKeepsEntry；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 New、Flow、Compile、True、Single；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Compile_WithCycle_DetectsBackEdgeAndKeepsEntry()
    {
        var x = New<CountingNode>("X").Vm;
        var y = New<CountingNode>("Y").Vm;
        var nodes = new List<NodeViewModel> { x, y };
        var conns = new List<ConnectionModel> { Flow(x, y), Flow(y, x) };

        var graph = TestGraph.Compile(nodes, conns);

        Assert.True(graph.HasCycle);
        Assert.Single(graph.BackEdges);          // 恰好一条回边
        Assert.All(graph.EntryNodes,
            node => Assert.Contains(nodes, viewModel => viewModel.Id == node.Id)); // 环路下入口回退不抛异常
    }

    /// <summary>
    /// 测试相关入口 IncomingConnections_GroupsByTarget_PreservingConnectionOrder；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 New、Flow、Data、Compile、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void IncomingConnections_GroupsByTarget_PreservingConnectionOrder()
    {
        var a = New<SourceNode>("A").Vm;
        var b = New<ValueNode>("B").Vm;
        var flowConn = Flow(a, b);
        var dataConn = Data(a, b);
        var conns = new List<ConnectionModel> { dataConn, flowConn };

        var graph = TestGraph.Compile([a, b], conns);

        var incoming = graph.IncomingConnections[b.Id];
        Assert.Equal(2, incoming.Count);
        Assert.Same(dataConn, incoming[0]);      // 保持原始连线顺序
        Assert.Same(flowConn, incoming[1]);
    }
}
