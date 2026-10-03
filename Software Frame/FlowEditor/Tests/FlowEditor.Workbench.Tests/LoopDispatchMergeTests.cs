// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：LoopDispatchMergeTests。成员说明可结合下方具体实现阅读。
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

/// <summary>结构化循环与条件分发、分支汇聚组合后的执行验收。</summary>
public class LoopDispatchMergeTests
{
    /// <summary>
    /// 验证 Loop_WithDispatchAndOrMerge_RunsExactIterationsThenCompletes 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 New、Flow、FromPort、ExecuteAsync、Equal；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(100)]
    public async Task Loop_WithDispatchAndOrMerge_RunsExactIterationsThenCompletes(int iterations)
    {
        var (start, _) = New<CountingNode>("Start");
        var loopModel = new LoopNode { Name = "Loop", Iterations = iterations };
        var loop = new NodeViewModel(loopModel);
        var dispatch = new NodeViewModel(new SwitchNode
        {
            Name = "Dispatch",
            InputValue = 1,
            RulesText = "1=OUT1;2=OUT2"
        });
        var (branch1, branch1Model) = New<CountingNode>("Branch1");
        var (branch2, branch2Model) = New<CountingNode>("Branch2");
        var merge = new NodeViewModel(new BranchMergeNode { MergeOperator = BranchMergeOperator.OR });
        var (done, doneModel) = New<CountingNode>("Done");
        var nodes = new List<NodeViewModel> { start, loop, dispatch, branch1, branch2, merge, done };
        var connections = new List<ConnectionModel>
        {
            Flow(start, loop),
            FromPort(loop, "Body", dispatch, "Next_IN"),
            FromPort(dispatch, "OUT1", branch1, "Next_IN"),
            FromPort(dispatch, "OUT2", branch2, "Next_IN"),
            FromPort(branch1, "Next_Out", merge, "分支1"),
            FromPort(branch2, "Next_Out", merge, "分支2"),
            // merge 作为循环体末端保持悬空，由 LoopNormalizer 自动合成回边。
            FromPort(loop, "Completed", done, "Next_IN")
        };

        await new TestFlowChartExecutionEngine().ExecuteAsync(
            nodes, connections, CancellationToken.None, _ => { });

        Assert.Equal(iterations + 1, loop.Count); // Body N次 + Completed 1次
        Assert.Equal(iterations, dispatch.Count);
        Assert.Equal(iterations, branch1Model.Executions);
        Assert.Equal(0, branch2Model.Executions);
        Assert.Equal(iterations, merge.Count);
        Assert.Equal(1, doneModel.Executions);
        Assert.Equal(0, loopModel.CurrentIndex);
    }

    /// <summary>
    /// 验证 LoopIndex_DrivesDispatch_EachIterationExecutesExactlyOneBranch 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 New、FromPort、ExecuteAsync、Equal；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task LoopIndex_DrivesDispatch_EachIterationExecutesExactlyOneBranch()
    {
        var loopModel = new LoopNode { Name = "Loop", Iterations = 3 };
        var loop = new NodeViewModel(loopModel);
        var dispatch = new NodeViewModel(new SwitchNode
        {
            Name = "Dispatch",
            RulesText = "1=OUT1;2=OUT2;3=OUT1"
        });
        var (branch1, branch1Model) = New<CountingNode>("Branch1");
        var (branch2, branch2Model) = New<CountingNode>("Branch2");
        var merge = new NodeViewModel(new BranchMergeNode { MergeOperator = BranchMergeOperator.OR });
        var (done, doneModel) = New<CountingNode>("Done");
        var nodes = new List<NodeViewModel> { loop, dispatch, branch1, branch2, merge, done };
        var connections = new List<ConnectionModel>
        {
            FromPort(loop, "Body", dispatch, "Next_IN"),
            FromPort(loop, "当前索引", dispatch, "输入值"),
            FromPort(dispatch, "OUT1", branch1, "Next_IN"),
            FromPort(dispatch, "OUT2", branch2, "Next_IN"),
            FromPort(branch1, "Next_Out", merge, "分支1"),
            FromPort(branch2, "Next_Out", merge, "分支2"),
            FromPort(loop, "Completed", done, "Next_IN")
        };

        await new TestFlowChartExecutionEngine().ExecuteAsync(
            nodes, connections, CancellationToken.None, _ => { });

        Assert.Equal(3, dispatch.Count);
        Assert.Equal(2, branch1Model.Executions);
        Assert.Equal(1, branch2Model.Executions);
        Assert.Equal(3, merge.Count);
        Assert.Equal(1, doneModel.Executions);
    }

    /// <summary>
    /// 验证 LoopDispatch_UnmatchedLaterIteration_ReportsErrorAndNeverCompletesLoop 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 New、FromPort、ThrowsAsync、ExecuteAsync、Contains；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task LoopDispatch_UnmatchedLaterIteration_ReportsErrorAndNeverCompletesLoop()
    {
        var loopModel = new LoopNode { Name = "Loop", Iterations = 3 };
        var loop = new NodeViewModel(loopModel);
        var dispatch = new NodeViewModel(new SwitchNode
        {
            Name = "Dispatch",
            RulesText = "1=OUT1"
        });
        var (branch, branchModel) = New<CountingNode>("Branch");
        var merge = new NodeViewModel(new BranchMergeNode { MergeOperator = BranchMergeOperator.OR });
        var (done, doneModel) = New<CountingNode>("Done");
        var nodes = new List<NodeViewModel> { loop, dispatch, branch, merge, done };
        var connections = new List<ConnectionModel>
        {
            FromPort(loop, "Body", dispatch, "Next_IN"),
            FromPort(loop, "当前索引", dispatch, "输入值"),
            FromPort(dispatch, "OUT1", branch, "Next_IN"),
            FromPort(branch, "Next_Out", merge, "分支1"),
            FromPort(loop, "Completed", done, "Next_IN")
        };

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new TestFlowChartExecutionEngine().ExecuteAsync(
                nodes, connections, CancellationToken.None, _ => { }));

        Assert.Contains("输入值 [2] 没有匹配的分发规则", error.ToString());
        Assert.Equal(2, dispatch.Count);
        Assert.Equal(NodeStatus.Error, dispatch.Status);
        Assert.Equal(1, branchModel.Executions);
        Assert.Equal(1, merge.Count);
        Assert.Equal(0, doneModel.Executions);
    }

    /// <summary>
    /// 验证 FromPort 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 First；具体条件和先后次序见方法体。
    /// </summary>
    private static ConnectionModel FromPort(
        NodeViewModel source,
        string sourcePortName,
        NodeViewModel target,
        string targetPortName)
        => new()
        {
            SourceNodeId = source.Id,
            SourcePortId = source.OutputPorts.First(port => port.Name == sourcePortName).Id,
            TargetNodeId = target.Id,
            TargetPortId = target.InputPorts.First(port => port.Name == targetPortName).Id
        };
}
