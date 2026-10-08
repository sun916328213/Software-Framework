// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：LoopFlowRunnerTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls.Core、FlowEditor.Core、FlowEditor.Runtime、FlowEditor.Controls.Nodes、FlowEditor.Core.Nodes、Xunit。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;
using FlowEditor.Controls.Nodes;
using FlowEditor.Core.Nodes;
using Xunit;

namespace FlowEditor.Workbench.Tests;

/// <summary>
/// LoopFlowRunnerTests 的类型定义。FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public class LoopFlowRunnerTests
{
    /// <summary>
    /// 测试相关入口 ExecuteAsync_RunsBodyExactlyConfiguredNumberOfTimes；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 ExecuteAsync、Equal；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_RunsBodyExactlyConfiguredNumberOfTimes()
    {
        var model = new CountingNode { Name = "循环体节点" };
        var body = new ParallelFlowLane<NodeModel>(
            "body", "循环体", [model], []);

        await LoopFlowRunner.ExecuteAsync(
            body, 3, CancellationToken.None, _ => { });

        Assert.Equal(3, model.Executions);
        Assert.Equal(3, model.Count);
        Assert.Equal(1, model.CurrentRunCount);
    }

    /// <summary>
    /// 测试相关入口 ExecuteAsync_RejectsEmptyBody；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 ThrowsAsync、ExecuteAsync、Contains；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_RejectsEmptyBody()
    {
        var body = new ParallelFlowLane<NodeModel>(
            "body", "循环体", [], []);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            LoopFlowRunner.ExecuteAsync(body, 2, CancellationToken.None, _ => { }));

        Assert.Contains("没有节点", error.Message);
    }

    /// <summary>
    /// 测试相关入口 ExecuteAsync_EmitsConciseIterationAndNodeLogs；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 ExecuteAsync、Equal、Count、StartsWith、DoesNotContain；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_EmitsConciseIterationAndNodeLogs()
    {
        var node = new CountingNode { Name = "任务" };
        var body = new ParallelFlowLane<NodeModel>("body", "循环体", [node], []);
        var logs = new List<string>();

        await LoopFlowRunner.ExecuteAsync(body, 2, CancellationToken.None, logs.Add);

        Assert.Equal(4, logs.Count(message => message.StartsWith("[循环]")));
        Assert.DoesNotContain(logs, message => message.StartsWith("[引擎]"));
        Assert.DoesNotContain(logs, message => message.Contains("[循环:", StringComparison.Ordinal));
        Assert.Contains(logs, message => message.Contains("[任务] [开始]", StringComparison.Ordinal));
        Assert.Contains(logs, message => message.Contains("[任务] [完成]", StringComparison.Ordinal));
    }

    /// <summary>
    /// 测试相关入口 ExecuteAsync_MinusOneRunsUntilCancellation；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 ThrowsAnyAsync、ExecuteAsync、Cancel、Equal；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_MinusOneRunsUntilCancellation()
    {
        var node = new CountingNode { Name = "无限循环体" };
        var body = new ParallelFlowLane<NodeModel>("body", "循环体", [node], []);
        using var cts = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            LoopFlowRunner.ExecuteAsync(
                body,
                -1,
                cts.Token,
                _ => { },
                onIterationStarted: (iteration, _) =>
                {
                    if (iteration == 3)
                        cts.Cancel();
                },
                iterationDelayMs: 20));

        Assert.Equal(2, node.Executions);
    }
}
