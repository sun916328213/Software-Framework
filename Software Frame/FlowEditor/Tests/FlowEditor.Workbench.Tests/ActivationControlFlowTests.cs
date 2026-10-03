// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ActivationControlFlowTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls.Core、FlowEditor.Core、FlowEditor.Runtime、FlowEditor.Controls.Models、FlowEditor.Core.Models、FlowEditor.Controls.Nodes。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;
using FlowEditor.Controls.Models;
using FlowEditor.Core.Models;
using FlowEditor.Controls.Nodes;
using FlowEditor.Core.Nodes;
using FlowEditor.Controls.Nodes.FlowControl;
using FlowEditor.Controls.ViewModels;
using Xunit;
using static FlowEditor.Workbench.Tests.TestGraph;

namespace FlowEditor.Workbench.Tests;

/// <summary>P1 验收：激活边控制流语义（分支只走选中路径；循环计数生效并自然终止）。</summary>
public class ActivationControlFlowTests
{
    private readonly TestFlowChartExecutionEngine _engine = new();

    // ── 条件分支 ──

    [Fact]
    public async Task Condition_OnlySelectedBranchExecutes()
    {
        var a = New<ValueNode>("A").Vm;
        var condModel = new ConditionNode { Name = "COND", Condition = false };
        var cond = new NodeViewModel(condModel);
        var trueBranch = New<CountingNode>("T").Vm;
        var falseBranch = New<CountingNode>("F").Vm;
        var nodes = new List<NodeViewModel> { a, cond, trueBranch, falseBranch };
        var conns = new List<ConnectionModel>
        {
            Flow(a, cond),
            Branch(cond, "True", trueBranch),
            Branch(cond, "False", falseBranch),
        };

        await _engine.ExecuteAsync(nodes, conns, CancellationToken.None, _ => { });

        Assert.Equal(NodeStatus.Success, falseBranch.Status);   // 选中分支执行
        Assert.Equal(NodeStatus.Idle, trueBranch.Status);       // 未选中分支被跳过
    }

    /// <summary>
    /// 验证 Condition_UnselectedBranch_DoesNotPropagateData 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 New、Flow、Branch、Data、ExecuteAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Condition_UnselectedBranch_DoesNotPropagateData()
    {
        var (a, aModel) = New<SourceNode>("A");
        var condModel = new ConditionNode { Name = "COND", Condition = false };
        var cond = new NodeViewModel(condModel);
        var (trueTarget, trueTargetModel) = New<ValueNode>("T");
        var nodes = new List<NodeViewModel> { a, cond, trueTarget };
        var conns = new List<ConnectionModel>
        {
            Flow(a, cond),
            Branch(cond, "True", trueTarget),
            Data(a, trueTarget),   // 数据连线绕过条件直达 T：数据仍会传递，但 T 不应被执行
        };

        await _engine.ExecuteAsync(nodes, conns, CancellationToken.None, _ => { });

        Assert.Equal(NodeStatus.Idle, trueTarget.Status);
        Assert.NotEqual(101, trueTargetModel.Value); // T 未执行，未 +1
    }

    /// <summary>
    /// 验证 Condition_SelectedBranchRejoinsCommonSuccessor_CommonSuccessorExecutes 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 New、Branch、Flow、ExecuteAsync、Equal；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Condition_SelectedBranchRejoinsCommonSuccessor_CommonSuccessorExecutes()
    {
        var dispatch = new NodeViewModel(new SwitchNode
        {
            Name = "Dispatch",
            InputValue = 1,
            RulesText = "1=OUT1\n2=OUT2"
        });
        var (delay1, delay1Model) = New<CountingNode>("Delay1");
        var (delay2, delay2Model) = New<CountingNode>("Delay2");
        var (delay3, delay3Model) = New<CountingNode>("Delay3");
        var nodes = new List<NodeViewModel> { dispatch, delay1, delay2, delay3 };
        var conns = new List<ConnectionModel>
        {
            Branch(dispatch, "OUT1", delay1),
            Branch(dispatch, "OUT2", delay2),
            Flow(delay1, delay3),
            Flow(delay2, delay3),
        };

        await _engine.ExecuteAsync(nodes, conns, CancellationToken.None, _ => { });

        Assert.Equal(1, delay1Model.Executions);
        Assert.Equal(0, delay2Model.Executions);
        Assert.Equal(1, delay3Model.Executions);
        Assert.Equal(NodeStatus.Success, delay3.Status);
    }

    /// <summary>
    /// 验证 Dispatch_NoRuleMatched_RejoinIsStoppedByDispatchError 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 New、Branch、Flow、ThrowsAsync、ExecuteAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Dispatch_NoRuleMatched_RejoinIsStoppedByDispatchError()
    {
        var dispatch = new NodeViewModel(new SwitchNode
        {
            Name = "Dispatch",
            InputValue = 99,
            RulesText = "1=OUT1\n2=OUT2",
            UseDefaultBranch = false
        });
        var delay1 = New<CountingNode>("Delay1").Vm;
        var delay2 = New<CountingNode>("Delay2").Vm;
        var (delay3, delay3Model) = New<CountingNode>("Delay3");
        var nodes = new List<NodeViewModel> { dispatch, delay1, delay2, delay3 };
        var conns = new List<ConnectionModel>
        {
            Branch(dispatch, "OUT1", delay1),
            Branch(dispatch, "OUT2", delay2),
            Flow(delay1, delay3),
            Flow(delay2, delay3),
        };

        var error = await Assert.ThrowsAsync<AggregateException>(() =>
            _engine.ExecuteAsync(nodes, conns, CancellationToken.None, _ => { }));

        Assert.Contains("没有匹配的分发规则", error.ToString());
        Assert.Equal(NodeStatus.Error, dispatch.Status);
        Assert.Equal(0, delay3Model.Executions);
        Assert.Equal(NodeStatus.Idle, delay3.Status);
    }

    /// <summary>
    /// 验证 Condition_LegacyFallback_NoBranchWiring_RunsAllSuccessors 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 New、Flow、ExecuteAsync、Equal；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Condition_LegacyFallback_NoBranchWiring_RunsAllSuccessors()
    {
        var (a, _) = New<ValueNode>("A");
        var cond = new NodeViewModel(new ConditionNode { Name = "COND" });
        var x = New<CountingNode>("X").Vm;
        var y = New<CountingNode>("Y").Vm;
        var nodes = new List<NodeViewModel> { a, cond, x, y };
        var conns = new List<ConnectionModel>
        {
            Flow(a, cond),
            new() { SourceNodeId = cond.Id, SourcePortId = $"{cond.Id}_Next_Out", TargetNodeId = x.Id, TargetPortId = $"{x.Id}_Next_IN" },
            new() { SourceNodeId = cond.Id, SourcePortId = $"{cond.Id}_Next_Out", TargetNodeId = y.Id, TargetPortId = $"{y.Id}_Next_IN" },
        };

        await _engine.ExecuteAsync(nodes, conns, CancellationToken.None, _ => { });

        // True/False 未接线 → 旧图兼容：Next_Out 正常放行
        Assert.Equal(NodeStatus.Success, x.Status);
        Assert.Equal(NodeStatus.Success, y.Status);
    }

    // ── 结构化循环 ──

    [Fact]
    public async Task Loop_ExecutesBodyIterationsTimes_ThenCompletesAndTerminates()
    {
        var loopModel = new LoopNode { Name = "LOOP", Iterations = 3 };
        var loop = new NodeViewModel(loopModel);
        var body = New<CountingNode>("W").Vm;
        var done = New<CountingNode>("D").Vm;
        var nodes = new List<NodeViewModel> { loop, body, done };
        var conns = new List<ConnectionModel>
        {
            Branch(loop, "Body", body),
            Flow(body, loop),                     // 回边构成循环
            Branch(loop, "Completed", done),
        };

        // 不取消——验证环路经激活边剪枝后自然终止
        await _engine.ExecuteAsync(nodes, conns, CancellationToken.None, _ => { });

        Assert.Equal(3, ((CountingNode)body.Model).Executions);
        Assert.Equal(1, ((CountingNode)done.Model).Executions);
        Assert.All(nodes, n => Assert.Equal(NodeStatus.Success, n.Status));
        Assert.Equal(0, loopModel.CurrentIndex);  // 完成后自动复位
    }

    /// <summary>
    /// 验证 Loop_IterationsBelowOne_ClampsToSingleExecution 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 New、Branch、Flow、ExecuteAsync、Equal；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Loop_IterationsBelowOne_ClampsToSingleExecution()
    {
        var loopModel = new LoopNode { Name = "LOOP", Iterations = 0 };
        var loop = new NodeViewModel(loopModel);
        var body = New<CountingNode>("W").Vm;
        var nodes = new List<NodeViewModel> { loop, body };
        var conns = new List<ConnectionModel>
        {
            Branch(loop, "Body", body),
            Flow(body, loop),
        };

        await _engine.ExecuteAsync(nodes, conns, CancellationToken.None, _ => { });

        Assert.Equal(1, ((CountingNode)body.Model).Executions);
    }

    /// <summary>
    /// 验证 Loop_LegacyFallback_NoStructuredPorts_PassesThrough 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 New、ExecuteAsync、Equal；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Loop_LegacyFallback_NoStructuredPorts_PassesThrough()
    {
        var loop = new NodeViewModel(new LoopNode { Name = "LOOP" });
        var next = New<CountingNode>("N").Vm;
        var nodes = new List<NodeViewModel> { loop, next };
        var conns = new List<ConnectionModel>
        {
            new() { SourceNodeId = loop.Id, SourcePortId = $"{loop.Id}_Next_Out", TargetNodeId = next.Id, TargetPortId = $"{next.Id}_Next_IN" },
        };

        // 反序列化的旧图 IsOutEnabled=true → Next_Out 存在且 Body/Completed 未接线 → 直通一次
        await _engine.ExecuteAsync(nodes, conns, CancellationToken.None, _ => { });

        Assert.Equal(1, ((CountingNode)next.Model).Executions);
        Assert.Equal(NodeStatus.Success, next.Status);
    }

    /// <summary>
    /// 验证 Loop_LegacyFallback_WithBackEdge_StillCancellable 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 New、Branch、Flow、ThrowsAsync、ExecuteAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Loop_LegacyFallback_WithBackEdge_StillCancellable()
    {
        // 旧图形态：仅 Next_Out 接入回边（Body/Completed 无出线）→ 引擎回退全放行 →
        // 保持旧行为：无限循环，只能通过取消终止
        var loopModel = new LoopNode { Name = "LOOP" };
        loopModel.IsOutEnabled = true; // 模拟反序列化旧图恢复的通用 Out 端口
        var loop = new NodeViewModel(loopModel);
        var body = New<CountingNode>("W").Vm;
        var nodes = new List<NodeViewModel> { loop, body };
        var conns = new List<ConnectionModel> { Branch(loop, "Next_Out", body), Flow(body, loop) };

        using var cts = new CancellationTokenSource(500);
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _engine.ExecuteAsync(nodes, conns, cts.Token, _ => { }));

        Assert.True(((CountingNode)body.Model).Executions >= 2);
    }

    /// <summary>
    /// 验证 Loop_StructuredPortsWired_NextOutEdgeIsPruned 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 New、Branch、Flow、ExecuteAsync、Equal；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Loop_StructuredPortsWired_NextOutEdgeIsPruned()
    {
        // 迁移语义：一旦 Body/Completed 有出线，未激活的 Next_Out 出线被剪枝
        var loopModel = new LoopNode { Name = "LOOP", Iterations = 1 };
        loopModel.IsOutEnabled = true;
        var loop = new NodeViewModel(loopModel);
        var body = New<CountingNode>("W").Vm;
        var legacyNext = New<CountingNode>("OLD").Vm;
        var nodes = new List<NodeViewModel> { loop, body, legacyNext };
        var conns = new List<ConnectionModel>
        {
            Branch(loop, "Body", body),
            Flow(body, loop),
            Branch(loop, "Next_Out", legacyNext),
        };

        await _engine.ExecuteAsync(nodes, conns, CancellationToken.None, _ => { });

        Assert.Equal(1, ((CountingNode)body.Model).Executions);
        Assert.Equal(0, ((CountingNode)legacyNext.Model).Executions);   // 未激活端口连线被剪枝
        Assert.Equal(NodeStatus.Idle, legacyNext.Status);
    }

    /// <summary>从 source 节点名为 portName 的输出端口连到 target 的 Next_IN。</summary>
    private static ConnectionModel Branch(NodeViewModel source, string portName, NodeViewModel target)
        => new()
        {
            SourceNodeId = source.Id,
            SourcePortId = source.Model.OutputPorts.First(p => p.Name == portName).Id,
            TargetNodeId = target.Id,
            TargetPortId = $"{target.Id}_Next_IN"
        };
}
