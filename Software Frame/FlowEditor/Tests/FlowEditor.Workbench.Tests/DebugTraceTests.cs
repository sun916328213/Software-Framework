// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：DebugTraceTests。成员说明可结合下方具体实现阅读。
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

/// <summary>P4 验收：执行日志/数据追踪 + 断点/单步门闩。</summary>
public class DebugTraceTests
{
    /// <summary>构造 A(源100) → B(+1)，含数据连线，返回 (实例, 数据连线ID)。</summary>
    private static (FlowInstance Instance, string DataConnId) BuildTracedFlow()
    {
        var (a, _) = New<SourceNode>("A");
        var (b, _) = New<ValueNode>("B");
        var dataConn = Data(a, b);
        var flowConn = Flow(a, b);

        var doc = new FlowChartDocumentModel
        {
            Name = "追踪流",
            Nodes =
            {
                Desc(a), Desc(b),
            },
            Connections =
            {
                ToConnDoc(flowConn),
                ToConnDoc(dataConn),
            }
        };
        var instance = FlowInstance.Create(FlowProgram.FromDocument(doc));
        return (instance, dataConn.Id);
    }

    /// <summary>
    /// 验证 Desc 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 GetType、ToList、Select；具体条件和先后次序见方法体。
    /// </summary>
    private static NodeDocumentModel Desc(NodeViewModel vm)
    {
        var m = vm.Model;
        return new NodeDocumentModel
        {
            Id = m.Id,
            TypeName = m.GetType().AssemblyQualifiedName ?? m.GetType().FullName!,
            Name = m.Name,
            IsInEnabled = false,
            IsOutEnabled = true,
            CustomInputPorts = m.CustomInputPorts.Select(p => new CustomPortDocument { Id = p.Id, Name = p.Name }).ToList(),
            CustomOutputPorts = m.CustomOutputPorts.Select(p => new CustomPortDocument { Id = p.Id, Name = p.Name }).ToList(),
        };
    }

    /// <summary>
    /// 验证 ToConnDoc 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// </summary>
    private static ConnectionDocumentModel ToConnDoc(ConnectionModel c)
        => new()
        {
            Id = c.Id,
            SourceNodeId = c.SourceNodeId,
            SourcePortId = c.SourcePortId,
            TargetNodeId = c.TargetNodeId,
            TargetPortId = c.TargetPortId
        };

    // ── 数据追踪 ──

    [Fact]
    public async Task Journal_RecordsLatestEdgeValue()
    {
        var (instance, dataConnId) = BuildTracedFlow();

        await instance.RunAsync(CancellationToken.None);

        Assert.True(instance.Journal.TryGetLatestEdgeValue(dataConnId, out var value));
        Assert.Equal(100, value); // SourceNode 输出 100 经数据连线传给 B
    }

    /// <summary>
    /// 验证 Journal_RecordsNodeLifecycle 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 BuildTracedFlow、First、RunAsync、ToList、GetNodeTimeline；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Journal_RecordsNodeLifecycle()
    {
        var (instance, _) = BuildTracedFlow();
        var bId = instance.Nodes.First(n => n.Name == "B").Id;

        await instance.RunAsync(CancellationToken.None);

        var timeline = instance.Journal.GetNodeTimeline(bId).ToList();
        Assert.Contains(timeline, e => e.Kind == JournalKind.NodeStart);
        Assert.Contains(timeline, e => e.Kind == JournalKind.NodeFinish && e.Text.Contains("Success"));
    }

    // ── 断点 / 单步 ──

    [Fact]
    public async Task Breakpoint_PausesAtTargetNode_ContinueResumes()
    {
        var program = BuildThreeChain(out var bId);
        var instance = FlowInstance.Create(program);
        instance.Debug = new DebugController();
        instance.Debug.AddBreakpoint(bId);

        var pausedIds = new List<string>();
        instance.Debug.Paused += id => pausedIds.Add(id);

        var runTask = instance.RunAsync(CancellationToken.None);

        // 等待命中断点（B 处挂起）
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!instance.Debug.IsPaused && sw.ElapsedMilliseconds < 3000)
            await Task.Delay(10);

        Assert.True(instance.Debug.IsPaused, "应在 B 节点断点处暂停");
        Assert.Equal([bId], pausedIds);
        // A 已完成，C 尚未执行
        Assert.Equal(NodeStatus.Success, instance.Nodes.First(n => n.Name == "A").Status);
        Assert.Equal(NodeStatus.Idle, instance.Nodes.First(n => n.Name == "C").Status);

        instance.Debug.Continue();
        await runTask;

        Assert.Empty(pausedIds.Skip(1));                    // 仅命中一次
        Assert.Equal(NodeStatus.Success, instance.Nodes.First(n => n.Name == "C").Status);
    }

    /// <summary>
    /// 验证 StepMode_PausesEveryNode_ContinueWalksThrough 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 BuildThreeChain、Create、Increment、Continue、RunAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task StepMode_PausesEveryNode_ContinueWalksThrough()
    {
        var program = BuildThreeChain(out _);
        var instance = FlowInstance.Create(program);
        instance.Debug = new DebugController { StepMode = true };

        int stepCount = 0;
        // 门闩先建立后触发 Paused：在事件内直接 Continue，等价于"每步走一次"
        instance.Debug.Paused += _ =>
        {
            Interlocked.Increment(ref stepCount);
            instance.Debug.Continue();
        };

        await instance.RunAsync(CancellationToken.None);

        Assert.Equal(3, stepCount); // 三个节点各步进一次
    }

    /// <summary>
    /// 验证 BuildThreeChain 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 New、Desc、ToConnDoc、Flow、FromDocument；具体条件和先后次序见方法体。
    /// </summary>
    private static FlowProgram BuildThreeChain(out string bId)
    {
        var (a, _) = New<CountingNode>("A");
        var (b, _) = New<CountingNode>("B");
        var (c, _) = New<CountingNode>("C");
        bId = b.Id;
        var doc = new FlowChartDocumentModel
        {
            Nodes = { Desc(a), Desc(b), Desc(c) },
            Connections = { ToConnDoc(Flow(a, b)), ToConnDoc(Flow(b, c)) }
        };
        return FlowProgram.FromDocument(doc);
    }
}
