using FlowEditor.Core;
using FlowEditor.Models;
using FlowEditor.Nodes;
using FlowEditor.ViewModels;
using Xunit;
using static FlowEditorTest.TestGraph;

namespace FlowEditorTest;

/// <summary>P3 验收：编辑/运行分离——同一 FlowProgram 并发多实例互不干扰，支持无头运行。</summary>
public class RuntimeSeparationTests
{
    /// <summary>构造「入口(可注入值) → +1」的程序文档。</summary>
    private static FlowProgram BuildPlusOneProgram()
    {
        var (a, _) = New<ValueNode>("A");
        var (b, _) = New<ValueNode>("B");
        var doc = new FlowChartDocumentModel
        {
            Name = "加一流程",
            Nodes =
            {
                ToDescriptor(a),
                ToDescriptor(b),
            },
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
                    SourceNodeId = a.Id, SourcePortId = a.Model.OutputPorts.First(p => p.Name == "Value").Id,
                    TargetNodeId = b.Id, TargetPortId = b.Model.InputPorts.First(p => p.Name == "Value").Id
                },
            }
        };
        return FlowProgram.FromDocument(doc);
    }

    private static NodeDocumentModel ToDescriptor(NodeViewModel vm)
    {
        var m = vm.Model;
        return new NodeDocumentModel
        {
            Id = m.Id,
            TypeName = m.GetType().AssemblyQualifiedName ?? m.GetType().FullName!,
            Name = m.Name,
            X = m.X,
            Y = m.Y,
            IsInEnabled = false, // 程序级入口由测试直接注入，规避 IN 必连约束
            IsOutEnabled = true,
            Permission = m.Permission,
            CustomInputPorts = m.CustomInputPorts.Select(p => new CustomPortDocument { Id = p.Id, Name = p.Name, BoundProperty = p.PropertyName }).ToList(),
            CustomOutputPorts = m.CustomOutputPorts.Select(p => new CustomPortDocument { Id = p.Id, Name = p.Name, BoundProperty = p.PropertyName }).ToList(),
        };
    }

    [Fact]
    public async Task TwoInstances_ConcurrentRun_Isolated()
    {
        var program = BuildPlusOneProgram();
        var i1 = FlowInstance.Create(program);
        var i2 = FlowInstance.Create(program);

        // 各自向入口节点注入不同初值（实例间端口对象互不共享）
        Inject(i1, "A", 100);
        Inject(i2, "A", 500);

        await Task.WhenAll(i1.RunAsync(CancellationToken.None), i2.RunAsync(CancellationToken.None));

        Assert.Equal(102, FindValueNode(i1, "B").Value); // 100 → 101 → 102
        Assert.Equal(502, FindValueNode(i2, "B").Value); // 500 → 501 → 502

        // 执行计数互不干扰
        Assert.Equal(1, FindValueNode(i1, "B").Count);
        Assert.Equal(1, FindValueNode(i2, "B").Count);
    }

    [Fact]
    public async Task SameProgram_RerunOnSameInstance_RetainsIndependentState()
    {
        var program = BuildPlusOneProgram();
        var instance = FlowInstance.Create(program);
        Inject(instance, "A", 0);

        await instance.RunAsync(CancellationToken.None);
        await instance.RunAsync(CancellationToken.None);

        // 每轮重新注入/覆盖端口值，结果确定（不因上轮残留翻倍）
        Assert.Equal(2, FindValueNode(instance, "B").Value);
    }

    [Fact]
    public void Program_ListSnapshot_IsolatedFromSourceDocument()
    {
        var doc = new FlowChartDocumentModel { Name = "d" };
        doc.Nodes.Add(new NodeDocumentModel { Id = "n1", TypeName = typeof(CountingNode).AssemblyQualifiedName! });
        var program = FlowProgram.FromDocument(doc);

        doc.Nodes.Add(new NodeDocumentModel { Id = "n2", TypeName = typeof(CountingNode).AssemblyQualifiedName! });

        Assert.Single(program.Nodes); // 编译后源文档变更不影响程序
    }

    [Fact]
    public async Task Instance_StatusEvents_Fire()
    {
        var program = BuildPlusOneProgram();
        var instance = FlowInstance.Create(program);

        var events = new List<(string, NodeStatus)>();
        instance.NodeStatusChanged += (id, s) => events.Add((id, s));

        await instance.RunAsync(CancellationToken.None);

        Assert.Contains(events, e => e.Item2 == NodeStatus.Success);
        Assert.Equal(4, events.Count); // A/B 各 start 无关——Status 只在 Success/Error 写入：2 节点 × Success
    }

    private static void Inject(FlowInstance instance, string nodeName, int value)
    {
        var node = instance.Nodes.First(n => n.Name == nodeName);
        node.CustomInputPorts.First(p => p.Name == "Value").Value = value;
    }

    private static ValueNode FindValueNode(FlowInstance instance, string name)
        => (ValueNode)instance.Nodes.First(n => n.Name == name);
}
