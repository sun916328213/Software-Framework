// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：BranchMergeNodeTests、ControlledNode、CancelFlowNode。成员说明可结合下方具体实现阅读。
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
/// BranchMergeNodeTests 的类型定义。FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public class BranchMergeNodeTests
{
    /// <summary>
    /// ControlledNode 的类型定义。FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：NodeModel。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class ControlledNode : NodeModel
    {
        private readonly TaskCompletionSource _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 测试相关入口 ExecuteAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 TrySetResult、WaitAsync；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        public override async Task ExecuteAsync(CancellationToken ct)
        {
            Started.TrySetResult();
            await _release.Task.WaitAsync(ct);
        }

        /// <summary>
        /// 测试相关入口 Complete；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 TrySetResult；具体条件和先后次序见方法体。
        /// </summary>
        public void Complete() => _release.TrySetResult();
    }

    /// <summary>
    /// CancelFlowNode 的类型定义。FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：NodeModel。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class CancelFlowNode(
        CancellationTokenSource cancellation,
        Task? beforeCancellation = null) : NodeModel
    {
        public int Executions;

        /// <summary>记录执行，并在可选同步点到达后取消测试流程。</summary>
        public override async Task ExecuteAsync(CancellationToken ct)
        {
            Interlocked.Increment(ref Executions);
            if (beforeCancellation != null)
                await beforeCancellation.WaitAsync(ct);
            cancellation.Cancel();
        }
    }

    /// <summary>
    /// 测试相关入口 DefaultNode_ProvidesTwoNamedFlowInputsAndOneFlowOutput；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Equal、Select、All、Single；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 InputCount_AddsAndRemovesFlowInputs；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Contains、DoesNotContain；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void InputCount_AddsAndRemovesFlowInputs()
    {
        var merge = new BranchMergeNode { InputCount = 3 };
        Assert.Contains(merge.InputPorts, port => port.Name == "分支3" && port.DataType == PortType.Flow);

        merge.InputCount = 2;

        Assert.DoesNotContain(merge.InputPorts, port => port.Name == "分支3");
    }

    /// <summary>
    /// 测试相关入口 SelectedSwitchBranch_ThroughMerge_ContinuesToCommonSuccessor；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 New、Branch、ToMerge、Flow、ExecuteAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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

        await new TestFlowChartExecutionEngine().ExecuteAsync(
            nodes, connections, CancellationToken.None, _ => { });

        Assert.Equal(1, delay1Model.Executions);
        Assert.Equal(0, delay2Model.Executions);
        Assert.Equal(NodeStatus.Success, merge.Status);
        Assert.Equal(1, afterModel.Executions);
    }

    /// <summary>
    /// 测试相关入口 AndMerge_SelectedOneOfTwoBranches_DoesNotContinue；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 New、ExecuteAsync、Branch、ToMerge、Flow；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task AndMerge_SelectedOneOfTwoBranches_DoesNotContinue()
    {
        var dispatch = new NodeViewModel(new SwitchNode { InputValue = 1, RulesText = "1=OUT1;2=OUT2" });
        var (branch1, _) = New<CountingNode>("B1");
        var (branch2, _) = New<CountingNode>("B2");
        var merge = new NodeViewModel(new BranchMergeNode { MergeOperator = BranchMergeOperator.AND });
        var (after, afterModel) = New<CountingNode>("After");

        await new TestFlowChartExecutionEngine().ExecuteAsync(
            [dispatch, branch1, branch2, merge, after],
            [Branch(dispatch, "OUT1", branch1), Branch(dispatch, "OUT2", branch2),
             ToMerge(branch1, merge, "分支1"), ToMerge(branch2, merge, "分支2"), Flow(merge, after)],
            CancellationToken.None, _ => { });

        Assert.Equal(NodeStatus.Idle, merge.Status);
        Assert.Equal(0, afterModel.Executions);
    }

    /// <summary>
    /// 测试相关入口 OrMerge_ContinuesAfterFirstActiveBranchCompletes；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 New、ExecuteAsync、Flow、ToMerge、WaitAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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
        var engineTask = new TestFlowChartExecutionEngine().ExecuteAsync(
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

    /// <summary>
    /// 测试相关入口 OrMerge_WithBackEdge_ExecutesAndStartsNextCycle；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 New、Flow、Branch、ToMerge、FromMilliseconds；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
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
            await new TestFlowChartExecutionEngine().ExecuteAsync(nodes, connections, cts.Token, _ => { });
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

    /// <summary>
    /// 测试相关入口 Cycle_EntryFork_StartsBothBranchesBeforeEitherBranchCompletes；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 New、Flow、FromSeconds、ExecuteAsync、WaitAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Cycle_EntryFork_StartsBothBranchesBeforeEitherBranchCompletes()
    {
        var (start, _) = New<CountingNode>("Start");
        var firstModel = new ControlledNode { Name = "ConditionLike" };
        var secondModel = new ControlledNode { Name = "Delay" };
        var first = new NodeViewModel(firstModel);
        var second = new NodeViewModel(secondModel);
        var (returnNode, _) = New<CountingNode>("Return");
        var connections = new List<ConnectionModel>
        {
            Flow(start, first),
            Flow(start, second),
            Flow(first, returnNode),
            Flow(returnNode, first)
        };
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var engineTask = new TestFlowChartExecutionEngine().ExecuteAsync(
            [start, first, second, returnNode], connections, cts.Token, _ => { });

        await Task.WhenAll(firstModel.Started.Task, secondModel.Started.Task)
            .WaitAsync(TimeSpan.FromMilliseconds(500));

        cts.Cancel();
        firstModel.Complete();
        secondModel.Complete();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => engineTask);
    }

    /// <summary>
    /// 测试相关入口 Cycle_ShortBranchAdvancesWhileParallelLongBranchIsStillRunning；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 New、FromSeconds、ExecuteAsync、Flow、ThrowsAnyAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Cycle_ShortBranchAdvancesWhileParallelLongBranchIsStillRunning()
    {
        var (start, _) = New<CountingNode>("Start");
        var (condition, conditionModel) = New<CountingNode>("Condition");
        var delayModel = new ControlledNode { Name = "Delay" };
        var delay = new NodeViewModel(delayModel);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var downstreamModel = new CancelFlowNode(cts, delayModel.Started.Task)
        {
            Name = "ShortBranchDownstream"
        };
        var downstream = new NodeViewModel(downstreamModel);
        var engineTask = new TestFlowChartExecutionEngine().ExecuteAsync(
            [start, condition, delay, downstream],
            [Flow(start, condition), Flow(start, delay),
             Flow(condition, downstream), Flow(downstream, condition)],
            cts.Token, _ => { });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            engineTask.WaitAsync(TimeSpan.FromSeconds(2)));

        Assert.True(delayModel.Started.Task.IsCompleted, "长分支应已与条件分支同时启动");
        Assert.True(conditionModel.Executions >= 1);
        Assert.Equal(1, downstreamModel.Executions);
    }

    /// <summary>
    /// 测试相关入口 AndMerge_FirstArrivalMarksRunning_ButCompletesOnlyAfterAllBranches；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 New、FromSeconds、ExecuteAsync、Flow、ToMerge；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task AndMerge_FirstArrivalMarksRunning_ButCompletesOnlyAfterAllBranches()
    {
        var (start, _) = New<CountingNode>("Start");
        var firstModel = new ControlledNode { Name = "First" };
        var secondModel = new ControlledNode { Name = "Second" };
        var first = new NodeViewModel(firstModel);
        var second = new NodeViewModel(secondModel);
        var merge = new NodeViewModel(new BranchMergeNode
        {
            Name = "Merge",
            MergeOperator = BranchMergeOperator.AND
        });
        var logs = new List<string>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var afterModel = new CancelFlowNode(cts) { Name = "After" };
        var after = new NodeViewModel(afterModel);
        var engineTask = new TestFlowChartExecutionEngine().ExecuteAsync(
            [start, first, second, merge, after],
            [Flow(start, first), Flow(start, second),
             ToMerge(first, merge, "分支1"), ToMerge(second, merge, "分支2"),
             Flow(merge, after), Flow(after, start)],
            cts.Token, logs.Add);

        await Task.WhenAll(firstModel.Started.Task, secondModel.Started.Task)
            .WaitAsync(TimeSpan.FromSeconds(2));
        firstModel.Complete();
        await WaitUntilAsync(() => merge.Status == NodeStatus.Running);

        Assert.Equal(1, merge.Count);
        Assert.Equal(0, Volatile.Read(ref afterModel.Executions));
        Assert.False(engineTask.IsCompleted);

        secondModel.Complete();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => engineTask);

        Assert.Equal(NodeStatus.Success, merge.Status);
        Assert.Equal(1, merge.Count);
        Assert.Equal(1, afterModel.Executions);
    }

    /// <summary>
    /// 测试相关入口 ConditionNode_RemainsLoadableButIsHiddenFromToolbox；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Single、Cast、GetCustomAttributes、False、IsType；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ConditionNode_RemainsLoadableButIsHiddenFromToolbox()
    {
        var attribute = typeof(ConditionNode).GetCustomAttributes(typeof(FlowToolAttribute), false)
            .Cast<FlowToolAttribute>().Single();
        Assert.False(attribute.VisibleInToolbox);
        Assert.IsType<ConditionNode>(Activator.CreateInstance(typeof(ConditionNode)));
    }

    /// <summary>
    /// 测试相关入口 ReflectionRestore_PreservesDynamicFlowInputTypes；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 ToDocument、Single、All、Equal、IsType；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ReflectionRestore_PreservesDynamicFlowInputTypes()
    {
        var source = new BranchMergeNode
        {
            Name = "三选一汇聚",
            InputCount = 3,
            MergeOperator = BranchMergeOperator.AND
        };
        var mapper = new FlowDocumentMapper();
        var document = mapper.ToDocument(
            "汇聚测试", [source], [], new FlowChartModel());
        var descriptor = Assert.Single(document.Nodes);
        Assert.All(descriptor.CustomInputPorts,
            port => Assert.Equal(PortType.Flow, port.DataType));

        var restored = Assert.IsType<BranchMergeNode>(new ReflectionNodeFactory().Create(descriptor));

        Assert.Equal(3, restored.InputCount);
        Assert.Equal(BranchMergeOperator.AND, restored.MergeOperator);
        Assert.Equal(3, restored.InputPorts.Count);
        Assert.All(restored.InputPorts, port => Assert.Equal(PortType.Flow, port.DataType));
        Assert.All(restored.InputPorts, port => Assert.Equal(restored.Id, port.OwnerNodeId));
    }

    /// <summary>
    /// 测试相关入口 Branch；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 First；具体条件和先后次序见方法体。
    /// </summary>
    private static ConnectionModel Branch(NodeViewModel source, string portName, NodeViewModel target)
        => new()
        {
            SourceNodeId = source.Id,
            SourcePortId = source.Model.OutputPorts.First(port => port.Name == portName).Id,
            TargetNodeId = target.Id,
            TargetPortId = $"{target.Id}_Next_IN"
        };

    /// <summary>
    /// 测试相关入口 ToMerge；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 First；具体条件和先后次序见方法体。
    /// </summary>
    private static ConnectionModel ToMerge(
        NodeViewModel source, NodeViewModel merge, string mergePortName)
        => new()
        {
            SourceNodeId = source.Id,
            SourcePortId = $"{source.Id}_Next_Out",
            TargetNodeId = merge.Id,
            TargetPortId = merge.Model.InputPorts.First(port => port.Name == mergePortName).Id
        };

    /// <summary>
    /// 测试相关入口 WaitUntilAsync；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 FromSeconds、condition、Delay；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (!condition())
            await Task.Delay(10, cts.Token);
    }
}
