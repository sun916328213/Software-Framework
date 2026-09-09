using FlowEditor.Core;
using FlowEditor.Models;
using FlowEditor.Nodes;
using FlowEditor.Nodes.FlowControl;
using FlowEditor.ViewModels;
using Xunit;
using static FlowEditorTest.TestGraph;

namespace FlowEditorTest;

public class BranchMergeNodeTests
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
    public void DefaultNode_ProvidesTwoNamedFlowInputsAndOneFlowOutput()
    {
        var merge = new BranchMergeNode();

        Assert.Equal(["分支1", "分支2"], merge.InputPorts.Select(port => port.Name));
        Assert.All(merge.InputPorts, port => Assert.Equal(PortType.Flow, port.DataType));
        var output = Assert.Single(merge.OutputPorts);
        Assert.Equal("Next_Out", output.Name);
        Assert.Equal(PortType.Flow, output.DataType);
    }

    [Fact]
    public void InputCount_AddsAndRemovesFlowInputs()
    {
        var merge = new BranchMergeNode { InputCount = 3 };
        Assert.Contains(merge.InputPorts, port => port.Name == "分支3" && port.DataType == PortType.Flow);

        merge.InputCount = 2;

        Assert.DoesNotContain(merge.InputPorts, port => port.Name == "分支3");
    }

    [Fact]
    public async Task SelectedSwitchBranch_ThroughMerge_ContinuesToCommonSuccessor()
    {
        var dispatch = new NodeViewModel(new SwitchNode
        {
            InputValue = 1,
            RulesText = "1=OUT1;2=OUT2"
        });
        var (delay1, delay1Model) = New<CountingNode>("Delay1");
        var (delay2, delay2Model) = New<CountingNode>("Delay2");
        var mergeModel = new BranchMergeNode();
        var merge = new NodeViewModel(mergeModel);
        var (after, afterModel) = New<CountingNode>("After");
        var nodes = new List<NodeViewModel> { dispatch, delay1, delay2, merge, after };
        var connections = new List<ConnectionModel>
        {
            Branch(dispatch, "OUT1", delay1),
            Branch(dispatch, "OUT2", delay2),
            ToMerge(delay1, merge, "分支1"),
            ToMerge(delay2, merge, "分支2"),
            Flow(merge, after)
        };

        await new FlowChartExecutionEngine().ExecuteAsync(
            nodes, connections, CancellationToken.None, _ => { });

        Assert.Equal(1, delay1Model.Executions);
        Assert.Equal(0, delay2Model.Executions);
        Assert.Equal(NodeStatus.Success, merge.Status);
        Assert.Equal(1, afterModel.Executions);
    }

    [Fact]
    public async Task AndMerge_SelectedOneOfTwoBranches_DoesNotContinue()
    {
        var dispatch = new NodeViewModel(new SwitchNode { InputValue = 1, RulesText = "1=OUT1;2=OUT2" });
        var (branch1, _) = New<CountingNode>("B1");
        var (branch2, _) = New<CountingNode>("B2");
        var merge = new NodeViewModel(new BranchMergeNode { MergeOperator = BranchMergeOperator.AND });
        var (after, afterModel) = New<CountingNode>("After");

        await new FlowChartExecutionEngine().ExecuteAsync(
            [dispatch, branch1, branch2, merge, after],
            [Branch(dispatch, "OUT1", branch1), Branch(dispatch, "OUT2", branch2),
             ToMerge(branch1, merge, "分支1"), ToMerge(branch2, merge, "分支2"), Flow(merge, after)],
            CancellationToken.None, _ => { });

        Assert.Equal(NodeStatus.Idle, merge.Status);
        Assert.Equal(0, afterModel.Executions);
    }

    [Fact]
    public async Task OrMerge_ContinuesAfterFirstActiveBranchCompletes()
    {
        var (start, _) = New<CountingNode>("Start");
        var firstModel = new ControlledNode { Name = "First" };
        var secondModel = new ControlledNode { Name = "Second" };
        var first = new NodeViewModel(firstModel);
        var second = new NodeViewModel(secondModel);
        var merge = new NodeViewModel(new BranchMergeNode { MergeOperator = BranchMergeOperator.OR });
        var (after, afterModel) = New<CountingNode>("After");
        var engineTask = new FlowChartExecutionEngine().ExecuteAsync(
            [start, first, second, merge, after],
            [Flow(start, first), Flow(start, second),
             ToMerge(first, merge, "分支1"), ToMerge(second, merge, "分支2"), Flow(merge, after)],
            CancellationToken.None, _ => { });

        await Task.WhenAll(firstModel.Started.Task, secondModel.Started.Task).WaitAsync(TimeSpan.FromSeconds(2));
        try
        {
            firstModel.Complete();
            await WaitUntilAsync(() => Volatile.Read(ref afterModel.Executions) == 1);
            Assert.False(engineTask.IsCompleted);
        }
        finally
        {
            secondModel.Complete();
        }
        await engineTask;
        Assert.Equal(1, afterModel.Executions);
    }

    [Fact]
    public async Task OrMerge_WithBackEdge_ExecutesAndStartsNextCycle()
    {
        var (start, _) = New<CountingNode>("Start");
        var dispatch = new NodeViewModel(new SwitchNode
        {
            Name = "Dispatch",
            InputValue = 1,
            RulesText = "1=OUT1;2=OUT2"
        });
        var (branch1, branch1Model) = New<CountingNode>("Branch1");
        var (branch2, branch2Model) = New<CountingNode>("Branch2");
        var mergeModel = new BranchMergeNode { MergeOperator = BranchMergeOperator.OR };
        var merge = new NodeViewModel(mergeModel);
        var nodes = new List<NodeViewModel> { start, dispatch, branch1, branch2, merge };
        var connections = new List<ConnectionModel>
        {
            Flow(start, dispatch),
            Branch(dispatch, "OUT1", branch1),
            Branch(dispatch, "OUT2", branch2),
            ToMerge(branch1, merge, "分支1"),
            ToMerge(branch2, merge, "分支2"),
            new()
            {
                SourceNodeId = merge.Id,
                SourcePortId = $"{merge.Id}_Next_Out",
                TargetNodeId = dispatch.Id,
                TargetPortId = $"{dispatch.Id}_Next_IN"
            }
        };
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));

        try
        {
            await new FlowChartExecutionEngine().ExecuteAsync(nodes, connections, cts.Token, _ => { });
        }
        catch (OperationCanceledException)
        {
            // 正确实现会持续循环，直到测试主动停止。
        }

        Assert.True(merge.Count >= 1, "OR 汇聚在回路中至少应执行一次");
        Assert.True(dispatch.Count >= 2, "汇聚输出回边应启动下一轮条件分发");
        Assert.True(branch1Model.Executions >= 1);
        Assert.Equal(0, branch2Model.Executions);
    }

    [Fact]
    public void ConditionNode_RemainsLoadableButIsHiddenFromToolbox()
    {
        var attribute = typeof(ConditionNode).GetCustomAttributes(typeof(FlowToolAttribute), false)
            .Cast<FlowToolAttribute>().Single();
        Assert.False(attribute.VisibleInToolbox);
        Assert.IsType<ConditionNode>(Activator.CreateInstance(typeof(ConditionNode)));
    }

    [Fact]
    public void ReflectionRestore_PreservesDynamicFlowInputTypes()
    {
        var source = new BranchMergeNode
        {
            Name = "三选一汇聚",
            InputCount = 3,
            MergeOperator = BranchMergeOperator.AND
        };
        var persistence = new FlowChartPersistenceService(null!);
        var document = persistence.BuildDocument(
            "汇聚测试", [new NodeViewModel(source)], [], new FlowChartModel());
        var descriptor = Assert.Single(document.Nodes);
        Assert.All(descriptor.CustomInputPorts,
            port => Assert.Equal(PortType.Flow, port.DataType));

        var restored = Assert.IsType<BranchMergeNode>(ReflectionNodeFactory.Instance.Create(descriptor));

        Assert.Equal(3, restored.InputCount);
        Assert.Equal(BranchMergeOperator.AND, restored.MergeOperator);
        Assert.Equal(3, restored.InputPorts.Count);
        Assert.All(restored.InputPorts, port => Assert.Equal(PortType.Flow, port.DataType));
        Assert.All(restored.InputPorts, port => Assert.Equal(restored.Id, port.OwnerNodeId));
    }

    private static ConnectionModel Branch(NodeViewModel source, string portName, NodeViewModel target)
        => new()
        {
            SourceNodeId = source.Id,
            SourcePortId = source.Model.OutputPorts.First(port => port.Name == portName).Id,
            TargetNodeId = target.Id,
            TargetPortId = $"{target.Id}_Next_IN"
        };

    private static ConnectionModel ToMerge(
        NodeViewModel source, NodeViewModel merge, string mergePortName)
        => new()
        {
            SourceNodeId = source.Id,
            SourcePortId = $"{source.Id}_Next_Out",
            TargetNodeId = merge.Id,
            TargetPortId = merge.Model.InputPorts.First(port => port.Name == mergePortName).Id
        };

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (!condition())
            await Task.Delay(10, cts.Token);
    }
}
