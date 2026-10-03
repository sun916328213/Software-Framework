// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：MultiLoopTests。成员说明可结合下方具体实现阅读。
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

/// <summary>
/// 多循环结构功能测试。
/// 覆盖：嵌套循环（内层+外层）、串联多循环、循环体含条件分支、
/// 循环体多节点、循环内错误路由退出、循环自然终止。
/// </summary>
public class MultiLoopTests
{
    private readonly TestFlowChartExecutionEngine _engine = new();

    // ── 单循环基础验证 ──

    /// <summary>LoopNode 计数 3 次后自然终止，Completed 分支执行。</summary>
    [Fact]
    public async Task SingleLoop_3Iterations_TerminatesNaturally()
    {
        var (loop, loopModel) = New<LoopNode>("L");
        loopModel.Iterations = 3;
        var (body, bodyModel) = New<CountingNode>("B");
        var (done, doneModel) = New<CountingNode>("D");

        var nodes = new List<NodeViewModel> { loop, body, done };
        var conns = new List<ConnectionModel>
        {
            Branch(loop, "Body", body),
            Branch(loop, "Completed", done),
        };

        await _engine.ExecuteAsync(nodes, conns, CancellationToken.None, _ => { });

        // LoopNode 自身被调度 4 次（3 次 Body + 1 次 Completed）
        // body 执行 3 次
        Assert.Equal(3, bodyModel.Executions);
        Assert.Equal(1, doneModel.Executions);
        Assert.Equal(NodeStatus.Success, done.Status);
    }

    // ── 串联多循环 ──

    /// <summary>两个串联的 LoopNode：第一个循环结束后进入第二个循环。</summary>
    [Fact]
    public async Task SerialLoops_SecondLoopStartsAfterFirst()
    {
        var (loop1, loop1Model) = New<LoopNode>("L1");
        loop1Model.Iterations = 2;
        var (body1, body1Model) = New<CountingNode>("B1");

        var (loop2, loop2Model) = New<LoopNode>("L2");
        loop2Model.Iterations = 3;
        var (body2, body2Model) = New<CountingNode>("B2");

        var (done, doneModel) = New<CountingNode>("D");

        var nodes = new List<NodeViewModel> { loop1, body1, loop2, body2, done };
        var conns = new List<ConnectionModel>
        {
            Branch(loop1, "Body", body1),
            Branch(loop1, "Completed", loop2),
            Branch(loop2, "Body", body2),
            Branch(loop2, "Completed", done),
        };

        await _engine.ExecuteAsync(nodes, conns, CancellationToken.None, _ => { });

        Assert.Equal(2, body1Model.Executions);
        Assert.Equal(3, body2Model.Executions);
        Assert.Equal(1, doneModel.Executions);
    }

    // ── 循环体含条件分支 ──

    /// <summary>循环体内有条件分支：每轮根据条件走 True 或 False。</summary>
    [Fact]
    public async Task LoopWithCondition_TrueBranchTaken()
    {
        var (loop, loopModel) = New<LoopNode>("L");
        loopModel.Iterations = 3;
        var (cond, condModel) = New<ConditionNode>("C");
        condModel.Condition = true;
        var (trueBranch, trueModel) = New<CountingNode>("T");
        var (falseBranch, falseModel) = New<CountingNode>("F");
        var (done, _) = New<CountingNode>("D");

        var nodes = new List<NodeViewModel> { loop, cond, trueBranch, falseBranch, done };
        var conns = new List<ConnectionModel>
        {
            Branch(loop, "Body", cond),
            Branch(cond, "True", trueBranch),
            Branch(cond, "False", falseBranch),
            Branch(loop, "Completed", done),
        };

        await _engine.ExecuteAsync(nodes, conns, CancellationToken.None, _ => { });

        // cond 执行 3 次，每轮激活 True → trueBranch 执行 3 次，falseBranch 不执行
        Assert.Equal(3, trueModel.Executions);
        Assert.Equal(0, falseModel.Executions);
        Assert.Equal(NodeStatus.Idle, falseBranch.Status);
    }

    /// <summary>循环体内条件分支走 False 分支。</summary>
    [Fact]
    public async Task LoopWithCondition_FalseBranchTaken()
    {
        var (loop, loopModel) = New<LoopNode>("L");
        loopModel.Iterations = 2;
        var (cond, condModel) = New<ConditionNode>("C");
        condModel.Condition = false;
        var (trueBranch, trueModel) = New<CountingNode>("T");
        var (falseBranch, falseModel) = New<CountingNode>("F");
        var (done, _) = New<CountingNode>("D");

        var nodes = new List<NodeViewModel> { loop, cond, trueBranch, falseBranch, done };
        var conns = new List<ConnectionModel>
        {
            Branch(loop, "Body", cond),
            Branch(cond, "True", trueBranch),
            Branch(cond, "False", falseBranch),
            Branch(loop, "Completed", done),
        };

        await _engine.ExecuteAsync(nodes, conns, CancellationToken.None, _ => { });

        Assert.Equal(0, trueModel.Executions);
        Assert.Equal(2, falseModel.Executions);
        Assert.Equal(NodeStatus.Idle, trueBranch.Status);
    }

    // ── 循环体多节点 ──

    /// <summary>循环体含多个节点：每轮所有节点都执行。</summary>
    [Fact]
    public async Task LoopBody_MultipleNodes_AllExecuteEachIteration()
    {
        var (loop, loopModel) = New<LoopNode>("L");
        loopModel.Iterations = 3;
        var (a, aModel) = New<CountingNode>("A");
        var (b, bModel) = New<CountingNode>("B");
        var (c, cModel) = New<CountingNode>("C");
        var (done, _) = New<CountingNode>("D");

        var nodes = new List<NodeViewModel> { loop, a, b, c, done };
        var conns = new List<ConnectionModel>
        {
            Branch(loop, "Body", a),
            Flow(a, b),
            Flow(b, c),
            Branch(loop, "Completed", done),
        };

        await _engine.ExecuteAsync(nodes, conns, CancellationToken.None, _ => { });

        Assert.Equal(3, aModel.Executions);
        Assert.Equal(3, bModel.Executions);
        Assert.Equal(3, cModel.Executions);
    }

    // ── 循环内错误路由退出 ──

    /// <summary>循环体内节点失败后通过 Error 端口退出，环路终止。</summary>
    [Fact]
    public async Task LoopBody_Fails_RoutesErrorAndTerminates()
    {
        var (loop, loopModel) = New<LoopNode>("L");
        loopModel.Iterations = 100;
        var (body, bodyModel) = New<FaultToleranceTests.FlakyNode>("B");
        bodyModel.FailTimes = 1;
        bodyModel.RetryCount = 0;
        bodyModel.ErrorPolicy = NodeErrorPolicy.RouteToErrorPort;
        bodyModel.AddCustomOutputPort("Error");
        var (handler, handlerModel) = New<CountingNode>("H");

        var nodes = new List<NodeViewModel> { loop, body, handler };
        var conns = new List<ConnectionModel>
        {
            Branch(loop, "Body", body),
            new()
            {
                SourceNodeId = body.Id,
                SourcePortId = body.Model.OutputPorts.First(p => p.Name == "Error").Id,
                TargetNodeId = handler.Id,
                TargetPortId = $"{handler.Id}_Next_IN",
            },
        };

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await _engine.ExecuteAsync(nodes, conns, CancellationToken.None, _ => { });
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 5000, "失败后环路应立即终止");
        Assert.Equal(NodeStatus.Error, body.Status);
        Assert.Equal(1, handlerModel.Executions);
    }

    // ── 循环计数为 1 ──

    /// <summary>Iterations=1：Body 执行 1 次后走 Completed。</summary>
    [Fact]
    public async Task Loop_SingleIteration_BodyOnceThenCompleted()
    {
        var (loop, loopModel) = New<LoopNode>("L");
        loopModel.Iterations = 1;
        var (body, bodyModel) = New<CountingNode>("B");
        var (done, doneModel) = New<CountingNode>("D");

        var nodes = new List<NodeViewModel> { loop, body, done };
        var conns = new List<ConnectionModel>
        {
            Branch(loop, "Body", body),
            Branch(loop, "Completed", done),
        };

        await _engine.ExecuteAsync(nodes, conns, CancellationToken.None, _ => { });

        Assert.Equal(1, bodyModel.Executions);
        Assert.Equal(1, doneModel.Executions);
    }

    // ── 循环取消 ──

    /// <summary>大循环通过 CancellationToken 安全终止。</summary>
    [Fact]
    public async Task LargeLoop_Cancelled_Safely()
    {
        var (loop, loopModel) = New<LoopNode>("L");
        loopModel.Iterations = 100_000;
        var (body, bodyModel) = New<CountingNode>("B");

        var nodes = new List<NodeViewModel> { loop, body };
        var conns = new List<ConnectionModel>
        {
            Branch(loop, "Body", body),
        };

        using var cts = new CancellationTokenSource(500);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _engine.ExecuteAsync(nodes, conns, cts.Token, _ => { }));

        Assert.True(bodyModel.Executions >= 1, $"循环体应至少执行 1 次（实际 {bodyModel.Executions}）");
    }

    // ── 循环体为空（无 Body 出线） ──

    /// <summary>LoopNode 的 Body 端口未连线：无循环体回边，Loop 仅执行一次激活 Body（无后继），Completed 不触发。</summary>
    [Fact]
    public async Task Loop_BodyNotConnected_LoopRunsOnceNoCompletion()
    {
        var (loop, loopModel) = New<LoopNode>("L");
        loopModel.Iterations = 3;
        var (done, doneModel) = New<CountingNode>("D");

        var nodes = new List<NodeViewModel> { loop, done };
        var conns = new List<ConnectionModel>
        {
            Branch(loop, "Completed", done),
        };

        await _engine.ExecuteAsync(nodes, conns, CancellationToken.None, _ => { });

        // 无 Body 连线 → 无循环体末端 → 不合成回边 → Loop 仅被调度一次
        // 第一次：CurrentIndex=1 ≤ 3，激活 Body（无后继），无法重入
        // Completed 永不激活 → done 不执行
        Assert.Equal(1, loopModel.Count);
        Assert.Equal(0, doneModel.Executions);
    }

    // ── 辅助方法 ──

    /// <summary>从 source 的指定端口名连到 target 的 Next_IN。</summary>
    private static ConnectionModel Branch(NodeViewModel source, string portName, NodeViewModel target)
        => new()
        {
            SourceNodeId = source.Id,
            SourcePortId = source.Model.OutputPorts.First(p => p.Name == portName).Id,
            TargetNodeId = target.Id,
            TargetPortId = $"{target.Id}_Next_IN"
        };
}
