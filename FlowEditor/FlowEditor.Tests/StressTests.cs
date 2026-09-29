using System.Diagnostics;
using FlowEditor.Core;
using FlowEditor.Models;
using FlowEditor.Nodes;
using FlowEditor.ViewModels;
using Xunit;
using static FlowEditorTest.TestGraph;

namespace FlowEditorTest;

/// <summary>
/// 多节点场景压力测试。
/// 覆盖：大图编译性能、长链执行、菱形多路径执行、并发实例隔离、大量连线编译。
/// 验证系统在高负载条件下不崩溃、不无响应。
/// </summary>
public class StressTests
{
    private readonly TestFlowChartExecutionEngine _engine = new();

    // ── 编译性能 ──

    /// <summary>500 节点链编译时间 < 200ms。</summary>
    [Fact]
    public void Compile_500NodeChain_Under200ms()
    {
        var (nodes, conns) = BuildChain(500);
        var sw = Stopwatch.StartNew();
        var graph = TestGraph.Compile(nodes, conns);
        sw.Stop();

        Assert.Equal(500, graph.Nodes.Count);
        Assert.False(graph.HasCycle);
        Assert.True(sw.ElapsedMilliseconds < 200,
            $"500 节点链编译应在 200ms 内完成（实际 {sw.ElapsedMilliseconds}ms）");
    }

    /// <summary>1000 节点链编译时间 < 500ms。</summary>
    [Fact]
    public void Compile_1000NodeChain_Under500ms()
    {
        var (nodes, conns) = BuildChain(1000);
        var sw = Stopwatch.StartNew();
        var graph = TestGraph.Compile(nodes, conns);
        sw.Stop();

        Assert.Equal(1000, graph.Nodes.Count);
        Assert.True(sw.ElapsedMilliseconds < 500,
            $"1000 节点链编译应在 500ms 内完成（实际 {sw.ElapsedMilliseconds}ms）");
    }

    // ── 长链执行 ──

    /// <summary>100 节点链执行：所有节点执行成功，最终值正确。</summary>
    [Fact]
    public async Task Execute_100NodeChain_AllSucceed()
    {
        var (src, srcModel) = New<SourceNode>("Src");
        var nodes = new List<NodeViewModel> { src };
        var models = new List<ValueNode>();
        var conns = new List<ConnectionModel>();

        for (int i = 0; i < 99; i++)
        {
            var (vm, model) = New<ValueNode>($"N{i}");
            nodes.Add(vm);
            models.Add(model);
            conns.Add(Flow(nodes[i], nodes[i + 1]));
            conns.Add(Data(nodes[i], nodes[i + 1]));
        }

        await _engine.ExecuteAsync(nodes, conns, CancellationToken.None, _ => { });

        Assert.All(nodes, n => Assert.Equal(NodeStatus.Success, n.Status));
        // 最终值 = 100 + 99 = 199
        Assert.Equal(199, models[^1].Value);
    }

    /// <summary>300 节点链执行不超时（5 秒）。</summary>
    [Fact]
    public async Task Execute_300NodeChain_CompletesWithin5Seconds()
    {
        var (nodes, conns) = BuildCountingChain(300);
        var sw = Stopwatch.StartNew();
        await _engine.ExecuteAsync(nodes, conns, CancellationToken.None, _ => { });
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 5000,
            $"300 节点链执行应在 5 秒内完成（实际 {sw.ElapsedMilliseconds}ms）");
        Assert.All(nodes, n => Assert.Equal(NodeStatus.Success, n.Status));
    }

    // ── 菱形多路径 ──

    /// <summary>50 层菱形（每层 2 节点并行）执行不崩溃，汇聚节点正确。</summary>
    [Fact]
    public async Task Execute_50LayerDiamond_AllSucceed()
    {
        var (a, _) = New<CountingNode>("A");
        var nodes = new List<NodeViewModel> { a };
        var conns = new List<ConnectionModel> { };

        var prev = a;
        for (int i = 0; i < 50; i++)
        {
            var (left, _) = New<CountingNode>($"L{i}");
            var (right, _) = New<CountingNode>($"R{i}");
            var (merge, _) = New<CountingNode>($"M{i}");
            nodes.Add(left);
            nodes.Add(right);
            nodes.Add(merge);

            conns.Add(Flow(prev, left));
            conns.Add(Flow(prev, right));
            conns.Add(Flow(left, merge));
            conns.Add(Flow(right, merge));

            prev = merge;
        }

        await _engine.ExecuteAsync(nodes, conns, CancellationToken.None, _ => { });

        Assert.All(nodes, n => Assert.Equal(NodeStatus.Success, n.Status));
    }

    // ── 并发实例 ──

    /// <summary>5 个 FlowInstance 并发执行不崩溃，结果互不干扰。</summary>
    [Fact]
    public async Task FiveConcurrentInstances_NoCrash_Isolated()
    {
        var program = BuildSimpleProgram();
        var instances = new List<FlowInstance>();
        for (int i = 0; i < 5; i++)
            instances.Add(FlowInstance.Create(program));

        // 各注入不同初值
        for (int i = 0; i < instances.Count; i++)
        {
            var node = instances[i].Nodes.First(n => n.Name == "A");
            ((ValueNode)node).Value = 100 + i * 100;
        }

        // 并发执行
        await Task.WhenAll(instances.Select(inst => inst.RunAsync(CancellationToken.None)));

        // 验证结果隔离
        for (int i = 0; i < instances.Count; i++)
        {
            var b = (ValueNode)instances[i].Nodes.First(n => n.Name == "B");
            Assert.Equal(100 + i * 100 + 1, b.Value);  // input + 1
        }
    }

    // ── 大量连线 ──

    /// <summary>100 节点全互联编译不崩溃。</summary>
    [Fact]
    public void Compile_100Nodes_AllConnected_NoCrash()
    {
        var nodes = new List<NodeViewModel>();
        for (int i = 0; i < 100; i++)
        {
            var (vm, _) = New<CountingNode>($"N{i}");
            nodes.Add(vm);
        }

        var conns = new List<ConnectionModel>();
        // 每个节点连到后面 5 个节点（模拟密集连线）
        for (int i = 0; i < nodes.Count; i++)
        {
            for (int j = 1; j <= 5 && i + j < nodes.Count; j++)
            {
                conns.Add(Flow(nodes[i], nodes[i + j]));
            }
        }

        var sw = Stopwatch.StartNew();
        var graph = TestGraph.Compile(nodes, conns);
        sw.Stop();

        Assert.Equal(100, graph.Nodes.Count);
        Assert.False(graph.HasCycle);
        Assert.True(sw.ElapsedMilliseconds < 500,
            $"100 节点密集连线编译应在 500ms 内完成（实际 {sw.ElapsedMilliseconds}ms）");
    }

    // ── 内存安全 ──

    /// <summary>连续多次编译同一图不导致内存泄漏（不抛 OOM）。</summary>
    [Fact]
    public void RepeatedCompilation_NoMemoryLeak()
    {
        var (nodes, conns) = BuildChain(200);

        for (int i = 0; i < 50; i++)
        {
            var graph = TestGraph.Compile(nodes, conns);
            Assert.Equal(200, graph.Nodes.Count);
        }
    }

    // ── 辅助方法 ──

    private static (List<NodeViewModel> nodes, List<ConnectionModel> conns) BuildChain(int count)
    {
        var nodes = new List<NodeViewModel>();
        var conns = new List<ConnectionModel>();
        for (int i = 0; i < count; i++)
        {
            var (vm, _) = New<CountingNode>($"N{i}");
            nodes.Add(vm);
        }
        for (int i = 0; i < count - 1; i++)
            conns.Add(Flow(nodes[i], nodes[i + 1]));
        return (nodes, conns);
    }

    private static (List<NodeViewModel> nodes, List<ConnectionModel> conns) BuildCountingChain(int count)
    {
        return BuildChain(count);
    }

    private static FlowProgram BuildSimpleProgram()
    {
        var a = new ValueNode { Name = "A" };
        var b = new ValueNode { Name = "B" };
        var doc = new FlowChartDocumentModel
        {
            Name = "压力测试程序",
            Nodes = { ToDescriptor(a), ToDescriptor(b) },
            Connections =
            {
                new ConnectionDocumentModel
                {
                    Id = Guid.NewGuid().ToString("N"),
                    SourceNodeId = a.Id, SourcePortId = $"{a.Id}_Next_Out",
                    TargetNodeId = b.Id, TargetPortId = $"{b.Id}_Next_IN"
                },
                new ConnectionDocumentModel
                {
                    Id = Guid.NewGuid().ToString("N"),
                    SourceNodeId = a.Id, SourcePortId = a.OutputPorts.First(p => p.Name == "Value").Id,
                    TargetNodeId = b.Id, TargetPortId = b.InputPorts.First(p => p.Name == "Value").Id
                },
            }
        };
        return FlowProgram.FromDocument(doc);
    }

    private static NodeDocumentModel ToDescriptor(NodeModel m)
    {
        return new NodeDocumentModel
        {
            Id = m.Id,
            TypeName = m.GetType().AssemblyQualifiedName ?? m.GetType().FullName!,
            Name = m.Name,
            X = m.X,
            Y = m.Y,
            IsInEnabled = false,
            IsOutEnabled = true,
            Permission = m.Permission,
            CustomInputPorts = m.CustomInputPorts.Select(p => new CustomPortDocument { Id = p.Id, Name = p.Name, BoundProperty = p.PropertyName }).ToList(),
            CustomOutputPorts = m.CustomOutputPorts.Select(p => new CustomPortDocument { Id = p.Id, Name = p.Name, BoundProperty = p.PropertyName }).ToList(),
        };
    }
}
