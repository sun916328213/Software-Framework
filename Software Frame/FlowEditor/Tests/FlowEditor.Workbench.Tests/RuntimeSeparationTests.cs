// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：RuntimeSeparationTests。成员说明可结合下方具体实现阅读。
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

    /// <summary>
    /// 验证 ToDescriptor 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 GetType、ToList、Select；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 验证 TwoInstances_ConcurrentRun_Isolated 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 BuildPlusOneProgram、Create、Inject、WhenAll、RunAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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

    /// <summary>
    /// 验证 SameProgram_RerunOnSameInstance_RetainsIndependentState 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 BuildPlusOneProgram、Create、Inject、RunAsync、Equal；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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

    /// <summary>
    /// 验证 Program_ListSnapshot_IsolatedFromSourceDocument 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Add、FromDocument、Single；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Program_ListSnapshot_IsolatedFromSourceDocument()
    {
        var doc = new FlowChartDocumentModel { Name = "d" };
        doc.Nodes.Add(new NodeDocumentModel { Id = "n1", TypeName = typeof(CountingNode).AssemblyQualifiedName! });
        var program = FlowProgram.FromDocument(doc);

        doc.Nodes.Add(new NodeDocumentModel { Id = "n2", TypeName = typeof(CountingNode).AssemblyQualifiedName! });

        Assert.Single(program.Nodes); // 编译后源文档变更不影响程序
    }

    /// <summary>
    /// 验证 Instance_StatusEvents_Fire 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 BuildPlusOneProgram、Create、Add、RunAsync、Contains；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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

    /// <summary>
    /// 验证 Inject 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 First；具体条件和先后次序见方法体。
    /// </summary>
    private static void Inject(FlowInstance instance, string nodeName, int value)
    {
        var node = instance.Nodes.First(n => n.Name == nodeName);
        node.CustomInputPorts.First(p => p.Name == "Value").Value = value;
    }

    /// <summary>
    /// 验证 FindValueNode 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 First；具体条件和先后次序见方法体。
    /// </summary>
    private static ValueNode FindValueNode(FlowInstance instance, string name)
        => (ValueNode)instance.Nodes.First(n => n.Name == name);
}
