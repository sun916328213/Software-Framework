using FlowEditor.Core;
using FlowEditor.Models;
using FlowEditor.Nodes;
using FlowEditor.Nodes.FlowControl;
using FlowEditor.ViewModels;
using Xunit;
using static FlowEditorTest.TestGraph;

namespace FlowEditorTest;

/// <summary>结构化循环与条件分发、分支汇聚组合后的执行验收。</summary>
public class LoopDispatchMergeTests
{
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
