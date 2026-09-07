using FlowEditor.Core;
using FlowEditor.Models;
using FlowEditor.Nodes;
using FlowEditor.Nodes.FlowControl;
using FlowEditor.ViewModels;
using Xunit;
using static FlowEditorTest.TestGraph;

namespace FlowEditorTest;

/// <summary>P1 验收：激活边控制流语义（分支只走选中路径；循环计数生效并自然终止）。</summary>
public class ActivationControlFlowTests
{
    private readonly FlowChartExecutionEngine _engine = new();

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
