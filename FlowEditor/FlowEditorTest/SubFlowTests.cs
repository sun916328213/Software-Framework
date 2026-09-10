using FlowEditor.Core;
using FlowEditor.Models;
using FlowEditor.Nodes;
using FlowEditor.Nodes.FlowControl;
using Xunit;

namespace FlowEditorTest;

/// <summary>P5 验收：子流程节点（端口映射 / 递归深度防护 / 取消传播）。</summary>
public class SubFlowTests
{
    [Fact]
    public void SubFlow_RemainsLoadableButIsHiddenFromToolbox()
    {
        var attribute = typeof(SubFlowNode)
            .GetCustomAttributes(typeof(FlowToolAttribute), false)
            .Cast<FlowToolAttribute>()
            .Single();

        Assert.False(attribute.VisibleInToolbox);
        Assert.IsType<SubFlowNode>(new SubFlowNode());
    }

    /// <summary>构造子流程文档：N1(ValueNode) → N2(ValueNode)，数据连线 N1.Value→N2.Value。</summary>
    private static FlowChartDocumentModel BuildInnerPlusChain()
    {
        var n1 = new ValueNode { Name = "N1", IsInEnabled = false };
        var n2 = new ValueNode { Name = "N2", IsInEnabled = false };

        return new FlowChartDocumentModel
        {
            Name = "内部加一链",
            Nodes =
            {
                Desc(n1), Desc(n2),
            },
            Connections =
            {
                Conn(n1.Id, $"{n1.Id}_Next_Out", n2.Id, $"{n2.Id}_Next_IN"),
                Conn(n1.Id, OutPortId(n1, "Value"), n2.Id, InPortId(n2, "Value")),
            }
        };
    }

    private static NodeDocumentModel Desc(NodeModel m) => new()
    {
        Id = m.Id,
        TypeName = m.GetType().AssemblyQualifiedName ?? m.GetType().FullName!,
        Name = m.Name,
        IsInEnabled = m.IsInEnabled,
        IsOutEnabled = true,
        CustomInputPorts = m.CustomInputPorts.Select(p => new CustomPortDocument { Id = p.Id, Name = p.Name }).ToList(),
        CustomOutputPorts = m.CustomOutputPorts.Select(p => new CustomPortDocument { Id = p.Id, Name = p.Name }).ToList(),
    };

    private static ConnectionDocumentModel Conn(string s, string sp, string t, string tp)
        => new() { Id = Guid.NewGuid().ToString("N"), SourceNodeId = s, SourcePortId = sp, TargetNodeId = t, TargetPortId = tp };

    private static string OutPortId(NodeModel n, string name) => n.OutputPorts.First(p => p.Name == name).Id;
    private static string InPortId(NodeModel n, string name) => n.InputPorts.First(p => p.Name == name).Id;

    /// <summary>构建带映射端口的子流程节点：输入 N1.Value，输出 N2.Value。</summary>
    private static SubFlowNode BuildMappedSubFlow(FlowChartDocumentModel inner)
        => new()
        {
            Name = "SUB",
            InnerFlow = inner,
            IsInEnabled = false,
            IsOutEnabled = false,
        };

    [Fact]
    public async Task SubFlow_MapsDataAcrossBoundary()
    {
        var sub = BuildMappedSubFlow(BuildInnerPlusChain());
        sub.AddCustomInputPort("N1.Value");
        sub.AddCustomOutputPort("N2.Value");

        // 模拟引擎填充：注入外部输入
        sub.CustomInputPorts[0].Value = 100;

        await sub.ExecuteAsync(CancellationToken.None);

        Assert.Equal(102, (int?)sub.CustomOutputPorts[0].Value); // 100 →N1=101→N2=102
    }

    [Fact]
    public async Task SubFlow_WithoutInnerFlow_PassesThrough()
    {
        var sub = new SubFlowNode();
        await sub.ExecuteAsync(CancellationToken.None); // 不抛出即通过
    }

    [Fact]
    public void RecursionGuard_ThrowsBeyondMaxDepth()
    {
        var disposables = new List<IDisposable>();
        try
        {
            for (var i = 0; i < FlowRecursionGuard.MaxDepth; i++)
                disposables.Add(FlowRecursionGuard.Enter());

            Assert.Throws<InvalidOperationException>(() => FlowRecursionGuard.Enter());
            Assert.Equal(FlowRecursionGuard.MaxDepth, FlowRecursionGuard.CurrentDepth);
        }
        finally
        {
            foreach (var d in disposables) d.Dispose();
        }
    }

    [Fact]
    public void RecursionGuard_DepthRestoresAfterDispose()
    {
        using (FlowRecursionGuard.Enter())
        {
            Assert.Equal(1, FlowRecursionGuard.CurrentDepth);
        }
        Assert.Equal(0, FlowRecursionGuard.CurrentDepth);
    }

    [Fact]
    public async Task SubFlow_CancelPropagatesToInnerNodes()
    {
        var doc = new FlowChartDocumentModel { Nodes = { Desc(new CancelAwareNode { Name = "SLOW" }) } };
        var sub = BuildMappedSubFlow(doc);

        // 通过注入点捕获子流程实际使用的物化实例（工厂创建的是克隆节点，
        // 必须观测克隆而非测试持有的原始对象）
        FlowInstance? captured = null;
        SubFlowNode.InstanceFactory = p =>
        {
            var inst = FlowInstance.Create(p);
            captured = inst;
            return inst;
        };
        try
        {
            using var cts = new CancellationTokenSource();
            var runTask = sub.ExecuteAsync(cts.Token);
            Assert.NotNull(captured);

            var clone = (CancelAwareNode)captured!.Nodes.First(n => n.Name == "SLOW");

            // 确定性时序：等内部克隆节点确认已进入执行体，再触发取消。
            // 若任务提前失败则跳出循环，让下方断言呈现真实原因。
            while (!clone.Started)
            {
                if (runTask.IsCompleted)
                    break;
                await Task.Delay(10);
            }
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runTask);
            Assert.True(clone.WasCancelled, "取消应传播到子流程内部节点");
        }
        finally
        {
            SubFlowNode.InstanceFactory = null;
        }
    }

    /// <summary>长执行节点（尊重取消）。</summary>
    public class CancelAwareNode : NodeModel
    {
        public volatile bool Started;
        public volatile bool WasCancelled;

        public override async Task ExecuteAsync(CancellationToken ct)
        {
            Started = true;
            try
            {
                await Task.Delay(60_000, ct);
            }
            catch (OperationCanceledException)
            {
                WasCancelled = true;
                throw;
            }
        }
    }
}
