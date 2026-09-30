using FlowEditor.Core;
using FlowEditor.Models;
using FlowEditor.Nodes;
using FlowEditor.Nodes.FlowControl;
using FlowEditor.ViewModels;
using Xunit;
using static FlowEditorTest.TestGraph;

namespace FlowEditorTest;

public class ParallelForkNodeTests
{
    private sealed class ControlledNode : NodeModel
    {
        private readonly TaskCompletionSource _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async Task ExecuteAsync(CancellationToken ct)
        {
            Started.TrySetResult();
            await _release.Task.WaitAsync(ct);
        }

        public void Complete() => _release.TrySetResult();
    }

    [Fact]
    public void DefaultNode_HasTwoIndependentFlowOutputs()
    {
        var fork = new ParallelForkNode();

        Assert.Equal(["分支1", "分支2"], fork.OutputPorts.Select(port => port.Name));
        Assert.All(fork.OutputPorts, port => Assert.Equal(PortType.Flow, port.DataType));
        Assert.Single(fork.InputPorts);
        Assert.Equal("Next_IN", fork.InputPorts[0].Name);
    }

    [Fact]
    public async Task ForkAndAndMerge_StartTogetherAndJoinAfterAllBranches()
    {
        var start = new NodeViewModel(new StartNode());
        var fork = new NodeViewModel(new ParallelForkNode());
        var firstModel = new ControlledNode { Name = "First" };
        var secondModel = new ControlledNode { Name = "Second" };
        var first = new NodeViewModel(firstModel);
        var second = new NodeViewModel(secondModel);
        var merge = new NodeViewModel(new BranchMergeNode { MergeOperator = BranchMergeOperator.AND });
        var (after, afterModel) = New<CountingNode>("After");
        var connections = new List<ConnectionModel>
        {
            Flow(start, fork),
            FromFork(fork, "分支1", first),
            FromFork(fork, "分支2", second),
            ToMerge(first, merge, "分支1"),
            ToMerge(second, merge, "分支2"),
            Flow(merge, after)
        };

        var run = new TestFlowChartExecutionEngine().ExecuteAsync(
            [start, fork, first, second, merge, after],
            connections,
            CancellationToken.None,
            _ => { });
        await Task.WhenAll(firstModel.Started.Task, secondModel.Started.Task)
            .WaitAsync(TimeSpan.FromSeconds(2));

        firstModel.Complete();
        await WaitUntilAsync(() => merge.Status == NodeStatus.Running);
        Assert.Equal(0, Volatile.Read(ref afterModel.Executions));
        Assert.False(run.IsCompleted);

        secondModel.Complete();
        await run.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(NodeStatus.Success, merge.Status);
        Assert.Equal(1, afterModel.Executions);
    }

    [Fact]
    public void Validator_RejectsForkWithoutCommonAndMerge()
    {
        var start = new NodeViewModel(new StartNode());
        var fork = new NodeViewModel(new ParallelForkNode { Name = "并行分叉1" });
        var (first, _) = New<CountingNode>("First");
        var (second, _) = New<CountingNode>("Second");
        var connections = new List<ConnectionModel>
        {
            Flow(start, fork),
            FromFork(fork, "分支1", first),
            FromFork(fork, "分支2", second)
        };

        var report = FlowValidator.Analyze(
            [start.Model, fork.Model, first.Model, second.Model], connections);

        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Code == "E-PAR-JOIN-MISSING");
    }

    [Fact]
    public void Toolbox_UsesForkAndKeepsOldContainerForFileCompatibilityOnly()
    {
        Assert.Single(typeof(ParallelForkNode)
            .GetCustomAttributes(typeof(FlowToolAttribute), false));
        Assert.Empty(typeof(ParallelContainerNode)
            .GetCustomAttributes(typeof(FlowToolAttribute), false));
        Assert.IsType<ParallelContainerNode>(Activator.CreateInstance(typeof(ParallelContainerNode)));
    }

    private static ConnectionModel FromFork(
        NodeViewModel fork, string outputName, NodeViewModel target) => new()
    {
        SourceNodeId = fork.Id,
        SourcePortId = fork.OutputPorts.First(port => port.Name == outputName).Id,
        TargetNodeId = target.Id,
        TargetPortId = target.InputPorts.First(port => port.DataType == PortType.Flow).Id
    };

    private static ConnectionModel ToMerge(
        NodeViewModel source, NodeViewModel merge, string inputName) => new()
    {
        SourceNodeId = source.Id,
        SourcePortId = source.OutputPorts.First(port => port.DataType == PortType.Flow).Id,
        TargetNodeId = merge.Id,
        TargetPortId = merge.InputPorts.First(port => port.Name == inputName).Id
    };

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (!condition())
            await Task.Delay(10, cts.Token);
    }
}
