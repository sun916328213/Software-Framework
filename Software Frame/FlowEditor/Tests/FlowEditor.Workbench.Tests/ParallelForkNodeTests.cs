// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ParallelForkNodeTests、ControlledNode。成员说明可结合下方具体实现阅读。
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
/// ParallelForkNodeTests 的类型定义。FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public class ParallelForkNodeTests
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
    /// 测试相关入口 DefaultNode_HasTwoIndependentFlowOutputs；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Equal、Select、All、Single；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void DefaultNode_HasTwoIndependentFlowOutputs()
    {
        var fork = new ParallelForkNode();

        Assert.Equal(["分支1", "分支2"], fork.OutputPorts.Select(port => port.Name));
        Assert.All(fork.OutputPorts, port => Assert.Equal(PortType.Flow, port.DataType));
        Assert.Single(fork.InputPorts);
        Assert.Equal("Next_IN", fork.InputPorts[0].Name);
    }

    /// <summary>
    /// 测试相关入口 ForkAndAndMerge_StartTogetherAndJoinAfterAllBranches；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 New、Flow、FromFork、ToMerge、ExecuteAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 Validator_RejectsForkWithoutCommonAndMerge；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 New、Flow、FromFork、Analyze、Contains；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 Toolbox_UsesForkAndKeepsOldContainerForFileCompatibilityOnly；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Single、GetCustomAttributes、Empty、IsType、CreateInstance；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Toolbox_UsesForkAndKeepsOldContainerForFileCompatibilityOnly()
    {
        Assert.Single(typeof(ParallelForkNode)
            .GetCustomAttributes(typeof(FlowToolAttribute), false));
        Assert.Empty(typeof(ParallelContainerNode)
            .GetCustomAttributes(typeof(FlowToolAttribute), false));
        Assert.IsType<ParallelContainerNode>(Activator.CreateInstance(typeof(ParallelContainerNode)));
    }

    /// <summary>
    /// 测试相关入口 FromFork；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 First；具体条件和先后次序见方法体。
    /// </summary>
    private static ConnectionModel FromFork(
        NodeViewModel fork, string outputName, NodeViewModel target) => new()
    {
        SourceNodeId = fork.Id,
        SourcePortId = fork.OutputPorts.First(port => port.Name == outputName).Id,
        TargetNodeId = target.Id,
        TargetPortId = target.InputPorts.First(port => port.DataType == PortType.Flow).Id
    };

    /// <summary>
    /// 测试相关入口 ToMerge；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 First；具体条件和先后次序见方法体。
    /// </summary>
    private static ConnectionModel ToMerge(
        NodeViewModel source, NodeViewModel merge, string inputName) => new()
    {
        SourceNodeId = source.Id,
        SourcePortId = source.OutputPorts.First(port => port.DataType == PortType.Flow).Id,
        TargetNodeId = merge.Id,
        TargetPortId = merge.InputPorts.First(port => port.Name == inputName).Id
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
