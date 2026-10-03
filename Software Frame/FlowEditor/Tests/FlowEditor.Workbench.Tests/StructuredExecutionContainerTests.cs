// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：StructuredExecutionContainerTests、ControlledNode、PersistentCountingNode、PersistentFailOnceNode、FailOnceNode。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Collections.Concurrent、System.Windows、FlowEditor.Controls.Core、FlowEditor.Core、FlowEditor.Runtime、FlowEditor.Controls.Models。导入不代表所有类型都会在运行时加载。
using System.Collections.Concurrent;
using System.Windows;
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;
using FlowEditor.Controls.Models;
using FlowEditor.Core.Models;
using FlowEditor.Controls.Nodes;
using FlowEditor.Core.Nodes;
using FlowEditor.Controls.Nodes.FlowControl;
using FlowEditor.Controls.Services;
using FlowEditor.Controls.Services.Storage;
using FlowEditor.Controls.ViewModels;
using Xunit;
using static FlowEditor.Workbench.Tests.TestGraph;

namespace FlowEditor.Workbench.Tests;

/// <summary>
/// StructuredExecutionContainerTests 的类型定义。FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
[Collection(FlowLogContextCollection.Name)]
public class StructuredExecutionContainerTests
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
        /// 验证 ExecuteAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 TrySetResult、WaitAsync；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        public override async Task ExecuteAsync(CancellationToken ct)
        {
            Started.TrySetResult();
            await _release.Task.WaitAsync(ct);
        }

        /// <summary>
        /// 验证 Complete 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 TrySetResult；具体条件和先后次序见方法体。
        /// </summary>
        public void Complete() => _release.TrySetResult();
    }

    /// <summary>
    /// PersistentCountingNode 的类型定义。FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：NodeModel。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public sealed class PersistentCountingNode : NodeModel
    {
        public static int Executions;

        /// <summary>
        /// 验证 ExecuteAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Increment；具体条件和先后次序见方法体。
        /// </summary>
        public override Task ExecuteAsync(CancellationToken ct)
        {
            Interlocked.Increment(ref Executions);
            return Task.CompletedTask;
        }
    }

    /// <summary>跨 ToolBlock 文档重建记录尝试次数及最后实例的首次失败节点。</summary>
    public sealed class PersistentFailOnceNode : NodeModel
    {
        /// <summary>全部重建实例累计执行次数。</summary>
        public static int Attempts { get; set; }

        /// <summary>最近一次由文档重建的节点实例。</summary>
        public static PersistentFailOnceNode? LastInstance { get; private set; }

        /// <summary>创建节点并记录当前重建实例。</summary>
        public PersistentFailOnceNode()
        {
            LastInstance = this;
        }

        /// <inheritdoc />
        /// <remarks>
        /// 验证 ExecuteAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </remarks>
        public override Task ExecuteAsync(CancellationToken ct)
        {
            Attempts++;
            if (Attempts == 1)
                throw new InvalidOperationException("first tool block attempt failed");
            return Task.CompletedTask;
        }

        /// <summary>清空跨实例测试状态。</summary>
        public static void Reset()
        {
            Attempts = 0;
            LastInstance = null;
        }
    }

    /// <summary>
    /// FailOnceNode 的类型定义。FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：NodeModel。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class FailOnceNode : NodeModel
    {
        public int Attempts { get; private set; }

        /// <summary>
        /// 验证 ExecuteAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public override Task ExecuteAsync(CancellationToken ct)
        {
            Attempts++;
            if (Attempts == 1)
                throw new InvalidOperationException("first attempt failed");
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 验证 ParallelContainer_OnMainCanvas_StartsAllLanesAndWaitsForAll 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 CreateService、AddNode、Add、Flow、RunAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task ParallelContainer_OnMainCanvas_StartsAllLanesAndWaitsForAll()
    {
        var service = TestFlowEditorFactory.CreateService(new InMemoryFlowDocumentStore());
        var start = service.Canvas.AddNode(new StartNode());
        var containerModel = new ParallelContainerNode { Name = "并行容器1" };
        var container = service.Canvas.AddNode(containerModel);
        var firstModel = new ControlledNode
        {
            Name = "分支1任务",
            ParentExecutionContainerId = container.Id,
            ExecutionLaneId = containerModel.Lanes[0].Id
        };
        var secondModel = new ControlledNode
        {
            Name = "分支2任务",
            ParentExecutionContainerId = container.Id,
            ExecutionLaneId = containerModel.Lanes[1].Id
        };
        service.Canvas.AddNode(firstModel);
        service.Canvas.AddNode(secondModel);
        service.Connections.Add(Flow(start, container));

        var logs = new ConcurrentQueue<string>();
        var run = service.Execution.RunAsync(logs.Enqueue);
        await Task.WhenAll(firstModel.Started.Task, secondModel.Started.Task)
            .WaitAsync(TimeSpan.FromSeconds(2));

        firstModel.Complete();
        await Task.Delay(30);
        Assert.False(run.IsCompleted);

        secondModel.Complete();
        await run.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(service.Model.StatusText == "执行完成", string.Join(Environment.NewLine, logs));
    }

    /// <summary>
    /// 验证 ParallelContainer_FailedLane_DoesNotCancelRunningSiblingLane 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 ExecuteAsync、WaitAsync、FromSeconds、Delay、False；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task ParallelContainer_FailedLane_DoesNotCancelRunningSiblingLane()
    {
        var running = new ControlledNode { Name = "A" };
        var failed = new FailNode { Name = "B" };
        var run = ParallelFlowRunner.ExecuteAsync(
            [
                new ParallelFlowLane<NodeModel>("a", "A", [running], []),
                new ParallelFlowLane<NodeModel>("b", "B", [failed], [])
            ],
            CancellationToken.None,
            _ => { });

        await running.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await Task.Delay(100);

        Assert.False(run.IsCompleted, "B 支路失败后，正在执行的 A 支路不应被取消。");
        running.Complete();
        await run;
        Assert.Equal(NodeStatus.Success, running.Status);
        Assert.Equal(NodeStatus.Error, failed.Status);
    }

    /// <summary>
    /// 验证 LoopContainer_OnMainCanvas_RepeatsItsBodyExactly 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 CreateService、AddNode、Add、Flow、RunAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task LoopContainer_OnMainCanvas_RepeatsItsBodyExactly()
    {
        var service = CreateService();
        var start = service.Canvas.AddNode(new StartNode());
        var containerModel = new LoopContainerNode { Name = "循环容器1", IterationCount = 3 };
        var container = service.Canvas.AddNode(containerModel);
        var bodyModel = new CountingNode
        {
            Name = "循环任务",
            ParentExecutionContainerId = container.Id,
            ExecutionLaneId = containerModel.Lanes[0].Id
        };
        service.Canvas.AddNode(bodyModel);
        service.Connections.Add(Flow(start, container));

        await service.Execution.RunAsync(_ => { });

        Assert.Equal(3, bodyModel.Executions);
        Assert.Equal(3, containerModel.CurrentIteration);
        Assert.Equal("执行完成", service.Model.StatusText);
    }

    /// <summary>
    /// 验证 LoopContainer_MinusOneMeansInfiniteLoop 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void LoopContainer_MinusOneMeansInfiniteLoop()
    {
        var loop = new LoopContainerNode { IterationCount = -1 };

        Assert.Equal(-1, loop.IterationCount);
    }

    /// <summary>
    /// 验证 LoopContainer_ExposesIterationDelaySetting 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 GetProperty、NotNull、True；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void LoopContainer_ExposesIterationDelaySetting()
    {
        var property = typeof(LoopContainerNode).GetProperty("IterationDelayMs");

        Assert.NotNull(property);
        Assert.True(property!.CanRead && property.CanWrite);
    }

    /// <summary>
    /// 验证 LoopRunner_WaitsConfiguredDelayBetweenIterations 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 StartNew、ExecuteAsync、Equal、True；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task LoopRunner_WaitsConfiguredDelayBetweenIterations()
    {
        var body = new CountingNode();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        await LoopFlowRunner.ExecuteAsync(
            new ParallelFlowLane<NodeModel>("body", "循环体", [body], []),
            2,
            CancellationToken.None,
            _ => { },
            iterationDelayMs: 80);

        Assert.Equal(2, body.Executions);
        Assert.True(sw.ElapsedMilliseconds >= 70, $"实际耗时 {sw.ElapsedMilliseconds}ms");
    }

    /// <summary>验证停止命令能立即打断无限循环容器的轮间长延时。</summary>
    [Fact]
    public async Task LoopContainer_InfiniteDelay_StopInterruptsWaitingPromptly()
    {
        var service = CreateService();
        var start = service.Canvas.AddNode(new StartNode());
        var loopModel = new LoopContainerNode
        {
            Name = "无限循环容器",
            IterationCount = -1,
            IterationDelayMs = 10_000
        };
        var loop = service.Canvas.AddNode(loopModel);
        var bodyModel = new CountingNode
        {
            Name = "循环任务",
            ParentExecutionContainerId = loop.Id,
            ExecutionLaneId = loopModel.Lanes[0].Id
        };
        service.Canvas.AddNode(bodyModel);
        service.Connections.Add(Flow(start, loop));

        var run = service.Execution.RunAsync(_ => { });
        await WaitUntilAsync(() => bodyModel.Executions == 1, TimeSpan.FromSeconds(2));
        await Task.Delay(30);

        var stopWatch = System.Diagnostics.Stopwatch.StartNew();
        service.Execution.Stop();
        await run.WaitAsync(TimeSpan.FromSeconds(2));
        stopWatch.Stop();

        Assert.Equal(1, bodyModel.Executions);
        Assert.Equal(1, loopModel.CurrentIteration);
        Assert.True(stopWatch.Elapsed < TimeSpan.FromSeconds(1),
            $"停止耗时 {stopWatch.Elapsed.TotalMilliseconds:F0}ms，轮间延时未被及时取消。");
        Assert.False(service.Model.IsRunning);
        Assert.Equal("已取消", service.Model.StatusText);
    }

    /// <summary>
    /// 验证 ToolBlock_ExecutesNestedLoopWithItsOwnRuntimeScope 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 CreateService、AddNode、BuildNodeDocument、Add、Flow；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task ToolBlock_ExecutesNestedLoopWithItsOwnRuntimeScope()
    {
        PersistentCountingNode.Executions = 0;
        var service = CreateService();
        var start = service.Canvas.AddNode(new StartNode());
        var toolBlock = new ToolBlockNode { Name = "ToolBlock" };
        var toolBlockVm = service.Canvas.AddNode(toolBlock);
        var end = service.Canvas.AddNode(new EndNode());

        var innerLoop = new LoopContainerNode { Name = "循环容器_1", IterationCount = 2 };
        var innerBody = new PersistentCountingNode
        {
            Name = "循环任务",
            ParentExecutionContainerId = innerLoop.Id,
            ExecutionLaneId = innerLoop.Lanes[0].Id
        };
        toolBlock.InternalData =
        [
            new FlowChartDocumentModel
            {
                Name = "内部流程",
                Nodes =
                [
                    service.Documents.BuildNodeDocument(innerLoop),
                    service.Documents.BuildNodeDocument(innerBody)
                ]
            }
        ];
        service.Connections.Add(Flow(start, toolBlockVm));
        service.Connections.Add(Flow(toolBlockVm, end));

        await service.Execution.RunAsync(_ => { });

        Assert.Equal("执行完成", service.Model.StatusText);
        Assert.Equal(2, PersistentCountingNode.Executions);
    }

    /// <summary>验证 ToolBlock 内部循环首次失败后，重新执行会重建内部实例并清除内外红色状态。</summary>
    [Fact]
    public async Task ToolBlock_NestedLoopFailure_RerunClearsContainerAndInternalErrorStatus()
    {
        PersistentFailOnceNode.Reset();
        var service = CreateService();
        var start = service.Canvas.AddNode(new StartNode());
        var toolBlock = new ToolBlockNode { Name = "ToolBlock" };
        var toolBlockVm = service.Canvas.AddNode(toolBlock);
        var end = service.Canvas.AddNode(new EndNode());
        var innerLoop = new LoopContainerNode { Name = "循环容器_1", IterationCount = 1 };
        var innerFailure = new PersistentFailOnceNode
        {
            Name = "内部失败节点",
            ParentExecutionContainerId = innerLoop.Id,
            ExecutionLaneId = innerLoop.Lanes[0].Id
        };
        toolBlock.InternalData =
        [
            new FlowChartDocumentModel
            {
                Name = "内部失败流程",
                Nodes =
                [
                    service.Documents.BuildNodeDocument(innerLoop),
                    service.Documents.BuildNodeDocument(innerFailure)
                ]
            }
        ];
        service.Connections.Add(Flow(start, toolBlockVm));
        service.Connections.Add(Flow(toolBlockVm, end));

        await service.Execution.RunAsync(_ => { });

        var failedInternalInstance = Assert.IsType<PersistentFailOnceNode>(
            PersistentFailOnceNode.LastInstance);
        Assert.Equal(NodeStatus.Error, toolBlockVm.Status);
        Assert.Equal(NodeStatus.Error, failedInternalInstance.Status);
        Assert.Equal("执行出错", service.Model.StatusText);

        await service.Execution.RunAsync(_ => { });

        var successfulInternalInstance = Assert.IsType<PersistentFailOnceNode>(
            PersistentFailOnceNode.LastInstance);
        Assert.NotSame(failedInternalInstance, successfulInternalInstance);
        Assert.Equal(2, PersistentFailOnceNode.Attempts);
        Assert.Equal(NodeStatus.Success, successfulInternalInstance.Status);
        Assert.Equal(NodeStatus.Success, toolBlockVm.Status);
        Assert.Equal(NodeStatus.Success, end.Status);
        Assert.Equal("执行完成", service.Model.StatusText);
    }

    /// <summary>
    /// 验证 ContinueFromNode_ResumesFailedParallelLaneThenContinuesOuterFlow 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 CreateService、AddNode、Add、Flow、RunAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task ContinueFromNode_ResumesFailedParallelLaneThenContinuesOuterFlow()
    {
        var service = CreateService();
        var start = service.Canvas.AddNode(new StartNode());
        var containerModel = new ParallelContainerNode { Name = "并行容器1" };
        var container = service.Canvas.AddNode(containerModel);
        var branchA = new CountingNode
        {
            Name = "A",
            ParentExecutionContainerId = container.Id,
            ExecutionLaneId = containerModel.Lanes[0].Id
        };
        var branchBFailure = new FailNode
        {
            Name = "B-故障",
            ParentExecutionContainerId = container.Id,
            ExecutionLaneId = containerModel.Lanes[1].Id
        };
        var branchBNext = new CountingNode
        {
            Name = "B-继续点",
            ParentExecutionContainerId = container.Id,
            ExecutionLaneId = containerModel.Lanes[1].Id
        };
        service.Canvas.AddNode(branchA);
        var failureVm = service.Canvas.AddNode(branchBFailure);
        var continueVm = service.Canvas.AddNode(branchBNext);
        var afterModel = new CountingNode { Name = "After" };
        var after = service.Canvas.AddNode(afterModel);
        service.Connections.Add(Flow(start, container));
        service.Connections.Add(Flow(failureVm, continueVm));
        service.Connections.Add(Flow(container, after));

        await service.Execution.RunAsync(_ => { });
        Assert.Equal(1, branchA.Executions);
        Assert.Equal(0, branchBNext.Executions);
        Assert.Equal(1, afterModel.Executions);

        await service.Execution.ContinueFromNodeAsync(continueVm.Model, _ => { });

        Assert.Equal(1, branchA.Executions);
        Assert.Equal(1, branchBNext.Executions);
        Assert.Equal(1, afterModel.Executions);
        Assert.Contains("恢复执行完成", service.Model.StatusText);
    }

    /// <summary>
    /// 验证 ContinueFromNode_RecoversParallelLaneWhileSiblingIsStillRunning 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 CreateService、AddNode、Add、Flow、RunAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task ContinueFromNode_RecoversParallelLaneWhileSiblingIsStillRunning()
    {
        var service = CreateService();
        var start = service.Canvas.AddNode(new StartNode());
        var containerModel = new ParallelContainerNode();
        var container = service.Canvas.AddNode(containerModel);
        var runningModel = new ControlledNode
        {
            Name = "A-长任务",
            ParentExecutionContainerId = container.Id,
            ExecutionLaneId = containerModel.Lanes[0].Id
        };
        service.Canvas.AddNode(runningModel);
        var failure = service.Canvas.AddNode(new FailNode
        {
            Name = "B-故障",
            ParentExecutionContainerId = container.Id,
            ExecutionLaneId = containerModel.Lanes[1].Id
        });
        var resumeModel = new CountingNode
        {
            Name = "B-恢复点",
            ParentExecutionContainerId = container.Id,
            ExecutionLaneId = containerModel.Lanes[1].Id
        };
        var resume = service.Canvas.AddNode(resumeModel);
        var afterModel = new CountingNode { Name = "After" };
        var after = service.Canvas.AddNode(afterModel);
        service.Connections.Add(Flow(start, container));
        service.Connections.Add(Flow(failure, resume));
        service.Connections.Add(Flow(container, after));

        var run = service.Execution.RunAsync(_ => { });
        await runningModel.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var timeout = System.Diagnostics.Stopwatch.StartNew();
        while (failure.Status != NodeStatus.Error && timeout.Elapsed < TimeSpan.FromSeconds(2))
            await Task.Delay(10);
        Assert.Equal(NodeStatus.Error, failure.Status);
        Assert.True(service.Model.IsRunning);

        await service.Execution.ContinueFromNodeAsync(resume.Model, _ => { });

        Assert.Equal(1, resumeModel.Executions);
        Assert.False(run.IsCompleted);
        runningModel.Complete();
        await run.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(1, afterModel.Executions);
        Assert.Equal("执行完成", service.Model.StatusText);
    }

    /// <summary>
    /// 验证并行支路失败后可从前置、失败或后续节点恢复；已重新执行的失败节点必须清除红色状态，
    /// 已成功的兄弟支路不得重复执行。
    /// </summary>
    /// <param name="resumePoint">恢复入口：previous、failure 或 next。</param>
    /// <param name="expectedPreviousExecutions">前置节点预期执行次数。</param>
    /// <param name="expectedFailureAttempts">失败节点预期尝试次数。</param>
    /// <param name="expectedFailureStatus">恢复后的失败节点状态。</param>
    [Theory]
    [InlineData("previous", 2, 2, NodeStatus.Success)]
    [InlineData("failure", 1, 2, NodeStatus.Success)]
    [InlineData("next", 1, 1, NodeStatus.Error)]
    public async Task ContinueFromNode_FailedParallelLane_SupportsPreviousFailureAndNext(
        string resumePoint,
        int expectedPreviousExecutions,
        int expectedFailureAttempts,
        NodeStatus expectedFailureStatus)
    {
        var service = CreateService();
        var start = service.Canvas.AddNode(new StartNode());
        var containerModel = new ParallelContainerNode { Name = "并行容器1" };
        var container = service.Canvas.AddNode(containerModel);
        var siblingModel = new CountingNode
        {
            Name = "A-正常支路",
            ParentExecutionContainerId = container.Id,
            ExecutionLaneId = containerModel.Lanes[0].Id
        };
        service.Canvas.AddNode(siblingModel);
        var previousModel = new CountingNode
        {
            Name = "B-前置节点",
            ParentExecutionContainerId = container.Id,
            ExecutionLaneId = containerModel.Lanes[1].Id
        };
        var previous = service.Canvas.AddNode(previousModel);
        var failureModel = new FailOnceNode
        {
            Name = "B-失败节点",
            ParentExecutionContainerId = container.Id,
            ExecutionLaneId = containerModel.Lanes[1].Id
        };
        var failure = service.Canvas.AddNode(failureModel);
        var nextModel = new CountingNode
        {
            Name = "B-后续节点",
            ParentExecutionContainerId = container.Id,
            ExecutionLaneId = containerModel.Lanes[1].Id
        };
        var next = service.Canvas.AddNode(nextModel);
        service.Connections.Add(Flow(start, container));
        service.Connections.Add(Flow(previous, failure));
        service.Connections.Add(Flow(failure, next));

        await service.Execution.RunAsync(_ => { });

        Assert.Equal(1, siblingModel.Executions);
        Assert.Equal(1, previousModel.Executions);
        Assert.Equal(1, failureModel.Attempts);
        Assert.Equal(NodeStatus.Error, failure.Status);
        Assert.Equal(0, nextModel.Executions);

        var resumeNode = resumePoint switch
        {
            "previous" => previous.Model,
            "failure" => failure.Model,
            "next" => next.Model,
            _ => throw new ArgumentOutOfRangeException(nameof(resumePoint))
        };
        await service.Execution.ContinueFromNodeAsync(resumeNode, _ => { });

        Assert.Equal(1, siblingModel.Executions);
        Assert.Equal(expectedPreviousExecutions, previousModel.Executions);
        Assert.Equal(expectedFailureAttempts, failureModel.Attempts);
        Assert.Equal(1, nextModel.Executions);
        Assert.Equal(expectedFailureStatus, failure.Status);
        Assert.Equal(NodeStatus.Success, container.Status);
        Assert.Contains("恢复执行完成", service.Model.StatusText);
    }

    /// <summary>
    /// 验证 ContinueFromFailedLoopNode_CompletesCurrentAndRemainingIterations 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 CreateService、AddNode、Add、Flow、RunAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task ContinueFromFailedLoopNode_CompletesCurrentAndRemainingIterations()
    {
        var service = CreateService();
        var start = service.Canvas.AddNode(new StartNode());
        var loopModel = new LoopContainerNode { Name = "循环容器1", IterationCount = 2 };
        var loop = service.Canvas.AddNode(loopModel);
        var failureModel = new FailOnceNode
        {
            Name = "循环故障",
            ParentExecutionContainerId = loop.Id,
            ExecutionLaneId = loopModel.Lanes[0].Id
        };
        var failure = service.Canvas.AddNode(failureModel);
        var bodyNextModel = new CountingNode
        {
            Name = "循环后继",
            ParentExecutionContainerId = loop.Id,
            ExecutionLaneId = loopModel.Lanes[0].Id
        };
        var bodyNext = service.Canvas.AddNode(bodyNextModel);
        var afterModel = new CountingNode { Name = "After" };
        var after = service.Canvas.AddNode(afterModel);
        service.Connections.Add(Flow(start, loop));
        service.Connections.Add(Flow(failure, bodyNext));
        service.Connections.Add(Flow(loop, after));

        await service.Execution.RunAsync(_ => { });
        Assert.Equal("执行出错", service.Model.StatusText);
        Assert.Equal(1, loopModel.CurrentIteration);

        await service.Execution.ContinueFromNodeAsync(failure.Model, _ => { });

        Assert.Equal(3, failureModel.Attempts);
        Assert.Equal(2, bodyNextModel.Executions);
        Assert.Equal(2, loopModel.CurrentIteration);
        Assert.Equal(NodeStatus.Success, loop.Status);
        Assert.Equal(1, afterModel.Executions);
        Assert.Contains("恢复执行完成", service.Model.StatusText);
    }

    /// <summary>
    /// 验证 ToolbarContinue_AfterLoopFailure_CanJumpToPreviousNodeAndContinuePath 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 CreateService、CreateViewModel、AddNode、Add、Flow；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task ToolbarContinue_AfterLoopFailure_CanJumpToPreviousNodeAndContinuePath()
    {
        var service = CreateService();
        var viewModel = TestFlowEditorFactory.CreateViewModel(service);
        var start = service.Canvas.AddNode(new StartNode());
        var loopModel = new LoopContainerNode { Name = "循环容器1", IterationCount = 1 };
        var loop = service.Canvas.AddNode(loopModel);
        var previousModel = new CountingNode
        {
            Name = "上一个节点",
            ParentExecutionContainerId = loop.Id,
            ExecutionLaneId = loopModel.Lanes[0].Id
        };
        var previous = service.Canvas.AddNode(previousModel);
        var failureModel = new FailOnceNode
        {
            Name = "失败节点",
            ParentExecutionContainerId = loop.Id,
            ExecutionLaneId = loopModel.Lanes[0].Id
        };
        var failure = service.Canvas.AddNode(failureModel);
        var nextModel = new CountingNode
        {
            Name = "后续节点",
            ParentExecutionContainerId = loop.Id,
            ExecutionLaneId = loopModel.Lanes[0].Id
        };
        var next = service.Canvas.AddNode(nextModel);
        var afterModel = new CountingNode { Name = "外层后续" };
        var after = service.Canvas.AddNode(afterModel);
        service.Connections.Add(Flow(start, loop));
        service.Connections.Add(Flow(previous, failure));
        service.Connections.Add(Flow(failure, next));
        service.Connections.Add(Flow(loop, after));

        await service.Execution.RunAsync(_ => { });
        Assert.Equal("执行出错", service.Model.StatusText);
        Assert.Equal(NodeStatus.Error, loop.Status);
        viewModel.SelectedNode = previous;

        await viewModel.StepContinueCommand.ExecuteAsync(null);

        Assert.Equal(2, previousModel.Executions);
        Assert.Equal(2, failureModel.Attempts);
        Assert.Equal(1, nextModel.Executions);
        Assert.Equal(1, afterModel.Executions);
        Assert.Equal(NodeStatus.Success, loop.Status);
    }

    /// <summary>
    /// 验证 LoopRegion_UsesExistingBoundaryConnectionsWithoutContainerWiring 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 CreateService、AddNode、Add、Flow、RunAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task LoopRegion_UsesExistingBoundaryConnectionsWithoutContainerWiring()
    {
        var service = CreateService();
        var start = service.Canvas.AddNode(new StartNode { X = 20, Y = 220 });
        var containerModel = new LoopContainerNode
        {
            Name = "循环容器1",
            X = 200,
            Y = 100,
            IterationCount = 3
        };
        var container = service.Canvas.AddNode(containerModel);
        var bodyModel = new CountingNode
        {
            Name = "循环任务",
            X = 280,
            Y = 220,
            ParentExecutionContainerId = container.Id,
            ExecutionLaneId = containerModel.Lanes[0].Id
        };
        var body = service.Canvas.AddNode(bodyModel);
        var afterModel = new CountingNode { Name = "After", X = 980, Y = 220 };
        var after = service.Canvas.AddNode(afterModel);
        service.Connections.Add(Flow(start, body));
        service.Connections.Add(Flow(body, after));

        await service.Execution.RunAsync(_ => { });

        Assert.Equal(2, service.Connections.Count);
        Assert.False(containerModel.ShowBoundaryPorts);
        Assert.Equal(3, bodyModel.Executions);
        Assert.Equal(1, afterModel.Executions);
        Assert.Equal("执行完成", service.Model.StatusText);
    }

    /// <summary>
    /// 验证 LoopRegion_PreservesDataTransferAcrossItsVisualBoundary 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 CreateService、AddNode、Add、Flow、Data；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task LoopRegion_PreservesDataTransferAcrossItsVisualBoundary()
    {
        var service = CreateService();
        var start = service.Canvas.AddNode(new StartNode());
        var source = service.Canvas.AddNode(new SourceNode { Name = "Source" });
        var containerModel = new LoopContainerNode { IterationCount = 2 };
        var container = service.Canvas.AddNode(containerModel);
        var bodyModel = new ValueNode
        {
            Name = "Body",
            ParentExecutionContainerId = container.Id,
            ExecutionLaneId = containerModel.Lanes[0].Id
        };
        var body = service.Canvas.AddNode(bodyModel);
        var afterModel = new ValueNode { Name = "After" };
        var after = service.Canvas.AddNode(afterModel);
        service.Connections.Add(Flow(start, source));
        service.Connections.Add(Flow(source, body));
        service.Connections.Add(Data(source, body));
        service.Connections.Add(Flow(body, after));
        service.Connections.Add(Data(body, after));

        await service.Execution.RunAsync(_ => { });

        Assert.Equal(5, service.Connections.Count);
        Assert.Equal(101, bodyModel.Value);
        Assert.Equal(102, afterModel.Value);
        Assert.Equal("执行完成", service.Model.StatusText);
    }

    /// <summary>
    /// 验证 CompiledFlowInstance_PreservesAndExecutesContainerMembers 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 CreateService、AddNode、Add、Flow、CompileProgram；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task CompiledFlowInstance_PreservesAndExecutesContainerMembers()
    {
        var service = CreateService();
        var start = service.Canvas.AddNode(new StartNode());
        var containerModel = new LoopContainerNode { Name = "循环容器1", IterationCount = 2 };
        var container = service.Canvas.AddNode(containerModel);
        var body = service.Canvas.AddNode(new CountingNode
        {
            Name = "循环任务",
            ParentExecutionContainerId = container.Id,
            ExecutionLaneId = containerModel.Lanes[0].Id
        });
        service.Connections.Add(Flow(start, container));

        var program = service.Execution.CompileProgram();
        var instance = FlowInstance.Create(program);
        await instance.RunAsync(CancellationToken.None);

        Assert.Equal(3, program.Nodes.Count);
        Assert.Equal(container.Id, instance.FindNode(body.Id)?.ParentExecutionContainerId);
        Assert.Equal(2, Assert.IsType<CountingNode>(instance.FindNode(body.Id)).Executions);
    }

    /// <summary>
    /// 验证 Validator_RejectsConnectionAcrossParallelLanes 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Flow、Analyze、Contains；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Validator_RejectsConnectionAcrossParallelLanes()
    {
        var container = new ParallelContainerNode { Name = "并行容器1" };
        var first = new CountingNode
        {
            Name = "A",
            ParentExecutionContainerId = container.Id,
            ExecutionLaneId = container.Lanes[0].Id
        };
        var second = new CountingNode
        {
            Name = "B",
            ParentExecutionContainerId = container.Id,
            ExecutionLaneId = container.Lanes[1].Id
        };
        var connection = Flow(new NodeViewModel(first), new NodeViewModel(second));

        var report = FlowValidator.Analyze([container, first, second], [connection]);

        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Code == "E-SCOPE-CROSS");
    }

    /// <summary>
    /// 验证 ConnectionService_RejectsCrossLaneConnectionBeforeItIsCreated 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 PlanConnection、Single、False、Contains；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ConnectionService_RejectsCrossLaneConnectionBeforeItIsCreated()
    {
        var container = new ParallelContainerNode();
        var first = new CountingNode
        {
            ParentExecutionContainerId = container.Id,
            ExecutionLaneId = container.Lanes[0].Id
        };
        var second = new CountingNode
        {
            ParentExecutionContainerId = container.Id,
            ExecutionLaneId = container.Lanes[1].Id
        };
        var service = new ConnectionService();

        var plan = service.PlanConnection(
            first.OutputPorts.Single(), second.InputPorts.Single(), [container, first, second], []);

        Assert.False(plan.Success);
        Assert.Contains("跨越", plan.Error);
    }

    /// <summary>
    /// 验证 DragParallelContainer_MovesItsMemberBySameDelta 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 CreateService、CreateViewModel、AddNode、SetSelectedNodes、BeginNodeDrag；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void DragParallelContainer_MovesItsMemberBySameDelta()
    {
        var service = CreateService();
        var vm = TestFlowEditorFactory.CreateViewModel(service);
        var containerModel = new ParallelContainerNode { X = 100, Y = 100 };
        var container = service.Canvas.AddNode(containerModel);
        var child = service.Canvas.AddNode(new CountingNode
        {
            Name = "Child",
            X = 160,
            Y = 220,
            ParentExecutionContainerId = container.Id,
            ExecutionLaneId = containerModel.Lanes[0].Id
        });

        vm.SetSelectedNodes([container]);
        vm.BeginNodeDrag(container, new Point(100, 100));
        vm.UpdateNodeDrag(container, new Point(140, 150));
        vm.EndNodeDrag(container);

        Assert.Equal(140, container.X);
        Assert.Equal(150, container.Y);
        Assert.Equal(200, child.X);
        Assert.Equal(270, child.Y);
        Assert.Equal(container.Id, child.Model.ParentExecutionContainerId);
    }

    /// <summary>
    /// 验证 PuttingConnectedNodeInsideLoopContainer_PreservesItsConnections 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 CreateService、CreateViewModel、AddNode、Add、Flow；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void PuttingConnectedNodeInsideLoopContainer_PreservesItsConnections()
    {
        var service = CreateService();
        var vm = TestFlowEditorFactory.CreateViewModel(service);
        var before = service.Canvas.AddNode(new CountingNode { Name = "Before", X = 20, Y = 220 });
        var loop = service.Canvas.AddNode(new LoopContainerNode { X = 200, Y = 100 });
        var inside = service.Canvas.AddNode(new CountingNode { Name = "Inside", X = 280, Y = 220 });
        var after = service.Canvas.AddNode(new CountingNode { Name = "After", X = 980, Y = 220 });
        service.Connections.Add(Flow(before, inside));
        service.Connections.Add(Flow(inside, after));

        vm.RefreshContainerMembership(loop);

        Assert.Equal(loop.Id, inside.Model.ParentExecutionContainerId);
        Assert.Equal(2, service.Connections.Count);
        Assert.Contains(service.Connections,
            connection => connection.SourceNodeId == before.Id && connection.TargetNodeId == inside.Id);
        Assert.Contains(service.Connections,
            connection => connection.SourceNodeId == inside.Id && connection.TargetNodeId == after.Id);
    }

    /// <summary>
    /// 验证 CreateService 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 CreateService；具体条件和先后次序见方法体。
    /// </summary>
    private static FlowChartService CreateService() =>
        TestFlowEditorFactory.CreateService(new InMemoryFlowDocumentStore());

    /// <summary>等待指定异步测试条件成立，超时则让测试明确失败。</summary>
    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
                throw new TimeoutException("等待测试条件成立超时。");
            await Task.Delay(10);
        }
    }
}
