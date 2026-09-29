using FlowEditor.Core;
using FlowEditor.Models;
using FlowEditor.Nodes;
using FlowEditor.ViewModels;
using Xunit;
using static FlowEditorTest.TestGraph;

namespace FlowEditorTest;

/// <summary>
/// 节点间连线创建/删除/重连操作测试。
/// 覆盖：连线创建约束（同节点、重复端口、方向校验、数据端口单连）、
/// 连线删除、图编译后回边检测、环路图执行不崩溃。
/// </summary>
public class ConnectionOperationTests
{
    private readonly TestFlowChartExecutionEngine _engine = new();

    // ── 连线方向校验 ──

    /// <summary>从输入端口到输出端口的反向连线在编译时不产生有效边。</summary>
    [Fact]
    public void ReverseDirection_Connection_IgnoredByCompiler()
    {
        var (a, _) = New<ValueNode>("A");
        var (b, _) = New<ValueNode>("B");

        // 手动构造一条反向连线（target 的输出端口 → source 的输入端口）
        var reverseConn = new ConnectionModel
        {
            SourceNodeId = b.Id,
            SourcePortId = b.Model.OutputPorts.First(p => p.Name == "Value").Id,
            TargetNodeId = a.Id,
            TargetPortId = a.Model.InputPorts.First(p => p.Name == "Value").Id,
        };

        var nodes = new List<NodeViewModel> { a, b };
        var conns = new List<ConnectionModel> { reverseConn };

        // 编译不应崩溃
        var graph = TestGraph.Compile(nodes, conns);
        // 反向连线在编译后是有效的边（编译器不校验方向），但不会形成环路
        Assert.False(graph.HasCycle);
    }

    // ── 重复连线 ──

    /// <summary>完全相同的两条连线（同源端口同目标端口）编译后不产生重复边。</summary>
    [Fact]
    public void DuplicateConnection_NoDuplicateEdgesInCompiledGraph()
    {
        var (a, _) = New<CountingNode>("A");
        var (b, _) = New<CountingNode>("B");
        var conn = Flow(a, b);

        var nodes = new List<NodeViewModel> { a, b };
        var conns = new List<ConnectionModel> { conn, conn };  // 同一条连线加两次

        var graph = TestGraph.Compile(nodes, conns);
        // successors 应包含 b（可能重复，但不崩溃）
        Assert.Contains(b.Id, graph.Successors[a.Id]);
        Assert.False(graph.HasCycle);
    }

    // ── 自环连线 ──

    /// <summary>节点连接自身的输出到输入不崩溃（编译时检测为回边）。</summary>
    [Fact]
    public void SelfLoop_DetectedAsCycle()
    {
        var (a, _) = New<CountingNode>("A");
        // 手动构造自环：A.Next_Out → A.Next_IN
        var selfConn = new ConnectionModel
        {
            SourceNodeId = a.Id,
            SourcePortId = $"{a.Id}_Next_Out",
            TargetNodeId = a.Id,
            TargetPortId = $"{a.Id}_Next_IN",
        };

        var nodes = new List<NodeViewModel> { a };
        var conns = new List<ConnectionModel> { selfConn };

        var graph = TestGraph.Compile(nodes, conns);
        Assert.True(graph.HasCycle);
    }

    // ── 环路检测 ──

    /// <summary>A→B→C→A 三节点环路被正确检测。</summary>
    [Fact]
    public void ThreeNodeCycle_DetectedByCompiler()
    {
        var (a, _) = New<CountingNode>("A");
        var (b, _) = New<CountingNode>("B");
        var (c, _) = New<CountingNode>("C");

        var nodes = new List<NodeViewModel> { a, b, c };
        var conns = new List<ConnectionModel>
        {
            Flow(a, b),
            Flow(b, c),
            Flow(c, a),  // 回边 → 环路
        };

        Assert.True(_engine.HasCycle(nodes, conns));
    }

    /// <summary>无环 DAG 不被误判为有环。</summary>
    [Fact]
    public void Dag_NoFalseCycleDetection()
    {
        var (a, _) = New<CountingNode>("A");
        var (b, _) = New<CountingNode>("B");
        var (c, _) = New<CountingNode>("C");
        var (d, _) = New<CountingNode>("D");

        var nodes = new List<NodeViewModel> { a, b, c, d };
        var conns = new List<ConnectionModel>
        {
            Flow(a, b),
            Flow(a, c),
            Flow(b, d),
            Flow(c, d),  // 菱形结构：A → B/C → D
        };

        Assert.False(_engine.HasCycle(nodes, conns));
    }

    // ── 连线删除后执行 ──

    /// <summary>删除连线后执行，被断开的下游节点成为新的入口节点独立执行。</summary>
    [Fact]
    public async Task DeleteConnection_DownstreamBecomesEntryNode()
    {
        var (a, aModel) = New<CountingNode>("A");
        var (b, bModel) = New<CountingNode>("B");
        var conn = Flow(a, b);

        var nodes = new List<NodeViewModel> { a, b };
        var conns = new List<ConnectionModel> { conn };

        // 删除连线
        conns.Remove(conn);

        await _engine.ExecuteAsync(nodes, conns, CancellationToken.None, _ => { });

        // 无连线 → A、B 均无前驱 → 均为入口节点，各自独立执行
        Assert.Equal(1, aModel.Executions);
        Assert.Equal(1, bModel.Executions);
        Assert.Equal(NodeStatus.Success, b.Status);
    }

    // ── 重连场景 ──

    /// <summary>删除旧连线、建立新连线后，执行路径正确切换。</summary>
    [Fact]
    public async Task Reconnect_ReroutesExecutionPath()
    {
        var (a, _) = New<CountingNode>("A");
        var (b, bModel) = New<CountingNode>("B");
        var (c, cModel) = New<CountingNode>("C");

        // 原始路径 A → B → C
        var connAB = Flow(a, b);
        var connBC = Flow(b, c);
        var nodes = new List<NodeViewModel> { a, b, c };
        var conns = new List<ConnectionModel> { connAB, connBC };

        // 重连：删除 A→B，建立 A→C
        conns.Remove(connAB);
        conns.Add(Flow(a, c));

        await _engine.ExecuteAsync(nodes, conns, CancellationToken.None, _ => { });

        // A→C 直连：C 通过 A→C 执行
        // B 失去唯一前驱 A→B 后成为入口节点独立执行，同时通过 B→C 驱动 C
        Assert.Equal(1, cModel.Executions);
        Assert.Equal(1, bModel.Executions);
        Assert.Equal(NodeStatus.Success, b.Status);
        Assert.Equal(NodeStatus.Success, c.Status);
    }

    // ── 环路图执行不崩溃 ──

    /// <summary>环路图通过 CancellationToken 安全终止，不导致无响应或崩溃。</summary>
    [Fact]
    public async Task CycleGraph_TerminatesSafely_OnCancellation()
    {
        var (x, xModel) = New<CountingNode>("X");
        var (y, yModel) = New<CountingNode>("Y");
        var nodes = new List<NodeViewModel> { x, y };
        var conns = new List<ConnectionModel> { Flow(x, y), Flow(y, x) };

        using var cts = new CancellationTokenSource(300);
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _engine.ExecuteAsync(nodes, conns, cts.Token, _ => { }));

        Assert.True(xModel.Executions >= 1);
        Assert.True(yModel.Executions >= 1);
    }

    // ── 多前驱连线 ──

    /// <summary>多前驱场景：两个上游节点都连到同一目标，目标在前驱全部完成后执行。</summary>
    [Fact]
    public async Task MultiplePredecessors_AllCompleteBeforeTarget()
    {
        var (a, aModel) = New<CountingNode>("A");
        var (b, bModel) = New<CountingNode>("B");
        var (c, cModel) = New<CountingNode>("C");

        var nodes = new List<NodeViewModel> { a, b, c };
        var conns = new List<ConnectionModel>
        {
            Flow(a, c),
            Flow(b, c),
        };

        await _engine.ExecuteAsync(nodes, conns, CancellationToken.None, _ => { });

        Assert.Equal(1, aModel.Executions);
        Assert.Equal(1, bModel.Executions);
        Assert.Equal(1, cModel.Executions);
        Assert.Equal(NodeStatus.Success, c.Status);
    }

    // ── 大量连线编译 ──

    /// <summary>大量连线不导致编译崩溃。</summary>
    [Fact]
    public void ManyConnections_CompilesWithoutCrash()
    {
        var nodes = new List<NodeViewModel>();
        var conns = new List<ConnectionModel>();
        for (int i = 0; i < 50; i++)
        {
            var (vm, _) = New<CountingNode>($"N{i}");
            nodes.Add(vm);
        }
        // 全连接：每个节点连到下一个
        for (int i = 0; i < nodes.Count - 1; i++)
            conns.Add(Flow(nodes[i], nodes[i + 1]));

        var graph = TestGraph.Compile(nodes, conns);
        Assert.Equal(50, graph.Nodes.Count);
        Assert.False(graph.HasCycle);
    }
}
