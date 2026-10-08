// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ParallelFlowRunnerTests、ControlledNode。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls.Core、FlowEditor.Core、FlowEditor.Runtime、FlowEditor.Controls.Models、FlowEditor.Core.Models、FlowEditor.Controls.Nodes。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;
using FlowEditor.Controls.Models;
using FlowEditor.Core.Models;
using FlowEditor.Controls.Nodes;
using FlowEditor.Core.Nodes;
using Xunit;

namespace FlowEditor.Workbench.Tests;

/// <summary>
/// ParallelFlowRunnerTests 的类型定义。FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public class ParallelFlowRunnerTests
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
    /// 测试相关入口 ExecuteAsync_StartsAllLanes_AndWaitsForEveryLane；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 ExecuteAsync、WaitAsync、WhenAll、FromSeconds、Complete；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_StartsAllLanes_AndWaitsForEveryLane()
    {
        var firstModel = new ControlledNode { Name = "分支1" };
        var secondModel = new ControlledNode { Name = "分支2" };
        var lanes = new[]
        {
            new ParallelFlowLane<NodeModel>("lane-1", "分支1", [firstModel], []),
            new ParallelFlowLane<NodeModel>("lane-2", "分支2", [secondModel], [])
        };

        var execution = ParallelFlowRunner.ExecuteAsync(
            lanes, CancellationToken.None, _ => { });

        await Task.WhenAll(firstModel.Started.Task, secondModel.Started.Task)
            .WaitAsync(TimeSpan.FromSeconds(2));

        firstModel.Complete();
        await Task.Delay(30);
        Assert.False(execution.IsCompleted);

        secondModel.Complete();
        await execution.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(NodeStatus.Success, firstModel.Status);
        Assert.Equal(NodeStatus.Success, secondModel.Status);
    }

    /// <summary>
    /// 测试相关入口 ExecuteAsync_RejectsFewerThanTwoLanes；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 ThrowsAsync、ExecuteAsync、Contains；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_RejectsFewerThanTwoLanes()
    {
        var node = new CountingNode { Name = "Only" };
        var lanes = new[]
        {
            new ParallelFlowLane<NodeModel>("lane-1", "分支1", [node], [])
        };

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ParallelFlowRunner.ExecuteAsync(lanes, CancellationToken.None, _ => { }));

        Assert.Contains("至少需要两个分支", error.Message);
    }

    /// <summary>
    /// 测试相关入口 ExecuteAsync_RejectsEmptyLane；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 ThrowsAsync、ExecuteAsync、Contains；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_RejectsEmptyLane()
    {
        var node = new CountingNode { Name = "First" };
        var lanes = new[]
        {
            new ParallelFlowLane<NodeModel>("lane-1", "分支1", [node], []),
            new ParallelFlowLane<NodeModel>("lane-2", "分支2", [], [])
        };

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ParallelFlowRunner.ExecuteAsync(lanes, CancellationToken.None, _ => { }));

        Assert.Contains("分支2", error.Message);
        Assert.Contains("没有节点", error.Message);
    }
}
