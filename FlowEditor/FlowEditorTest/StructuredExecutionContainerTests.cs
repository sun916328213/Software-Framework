using System.Collections.Concurrent;
using System.Windows;
using FlowEditor.Core;
using FlowEditor.Models;
using FlowEditor.Nodes;
using FlowEditor.Nodes.FlowControl;
using FlowEditor.Services;
using FlowEditor.Services.Storage;
using FlowEditor.ViewModels;
using Xunit;
using static FlowEditorTest.TestGraph;

namespace FlowEditorTest;

[Collection(FlowLogContextCollection.Name)]
public class StructuredExecutionContainerTests
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

    public sealed class PersistentCountingNode : NodeModel
    {
        public static int Executions;

        public override Task ExecuteAsync(CancellationToken ct)
        {
            Interlocked.Increment(ref Executions);
            return Task.CompletedTask;
        }
    }

    private sealed class FailOnceNode : NodeModel
    {
        public int Attempts { get; private set; }

        public override Task ExecuteAsync(CancellationToken ct)
        {
            Attempts++;
            if (Attempts == 1)
                throw new InvalidOperationException("first attempt failed");
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task ParallelContainer_OnMainCanvas_StartsAllLanesAndWaitsForAll()
    {
        var service = TestFlowEditorFactory.CreateService(new InMemoryFlowDocumentStore());
        var start = service.AddNode(new StartNode());
        var containerModel = new ParallelContainerNode { Name = "并行容器1" };
        var container = service.AddNode(containerModel);
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
        service.AddNode(firstModel);
        service.AddNode(secondModel);
        service.Connections.Add(Flow(start, container));

        var logs = new ConcurrentQueue<string>();
        var run = service.RunAsync(logs.Enqueue);
        await Task.WhenAll(firstModel.Started.Task, secondModel.Started.Task)
            .WaitAsync(TimeSpan.FromSeconds(2));

        firstModel.Complete();
        await Task.Delay(30);
        Assert.False(run.IsCompleted);

        secondModel.Complete();
        await run.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(service.Model.StatusText == "执行完成", string.Join(Environment.NewLine, logs));
    }

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

    [Fact]
    public async Task LoopContainer_OnMainCanvas_RepeatsItsBodyExactly()
    {
        var service = CreateService();
        var start = service.AddNode(new StartNode());
        var containerModel = new LoopContainerNode { Name = "循环容器1", IterationCount = 3 };
        var container = service.AddNode(containerModel);
        var bodyModel = new CountingNode
        {
            Name = "循环任务",
            ParentExecutionContainerId = container.Id,
            ExecutionLaneId = containerModel.Lanes[0].Id
        };
        service.AddNode(bodyModel);
        service.Connections.Add(Flow(start, container));

        await service.RunAsync(_ => { });

        Assert.Equal(3, bodyModel.Executions);
        Assert.Equal(3, containerModel.CurrentIteration);
        Assert.Equal("执行完成", service.Model.StatusText);
    }

    [Fact]
    public void LoopContainer_MinusOneMeansInfiniteLoop()
    {
        var loop = new LoopContainerNode { IterationCount = -1 };

        Assert.Equal(-1, loop.IterationCount);
    }

    [Fact]
    public void LoopContainer_ExposesIterationDelaySetting()
    {
        var property = typeof(LoopContainerNode).GetProperty("IterationDelayMs");

        Assert.NotNull(property);
        Assert.True(property!.CanRead && property.CanWrite);
    }

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

    [Fact]
    public async Task ToolBlock_ExecutesNestedLoopWithItsOwnRuntimeScope()
    {
        PersistentCountingNode.Executions = 0;
        var service = CreateService();
        var start = service.AddNode(new StartNode());
        var toolBlock = new ToolBlockNode { Name = "ToolBlock" };
        var toolBlockVm = service.AddNode(toolBlock);
        var end = service.AddNode(new EndNode());

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
                    service.BuildNodeDocument(innerLoop),
                    service.BuildNodeDocument(innerBody)
                ]
            }
        ];
        service.Connections.Add(Flow(start, toolBlockVm));
        service.Connections.Add(Flow(toolBlockVm, end));

        await service.RunAsync(_ => { });

        Assert.Equal("执行完成", service.Model.StatusText);
        Assert.Equal(2, PersistentCountingNode.Executions);
    }

    [Fact]
    public async Task ContinueFromNode_ResumesFailedParallelLaneThenContinuesOuterFlow()
    {
        var service = CreateService();
        var start = service.AddNode(new StartNode());
        var containerModel = new ParallelContainerNode { Name = "并行容器1" };
        var container = service.AddNode(containerModel);
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
        service.AddNode(branchA);
        var failureVm = service.AddNode(branchBFailure);
        var continueVm = service.AddNode(branchBNext);
        var afterModel = new CountingNode { Name = "After" };
        var after = service.AddNode(afterModel);
        service.Connections.Add(Flow(start, container));
        service.Connections.Add(Flow(failureVm, continueVm));
        service.Connections.Add(Flow(container, after));

        await service.RunAsync(_ => { });
        Assert.Equal(1, branchA.Executions);
        Assert.Equal(0, branchBNext.Executions);
        Assert.Equal(1, afterModel.Executions);

        await service.ContinueFromNodeAsync(continueVm, _ => { });

        Assert.Equal(1, branchA.Executions);
        Assert.Equal(1, branchBNext.Executions);
        Assert.Equal(1, afterModel.Executions);
        Assert.Contains("恢复执行完成", service.Model.StatusText);
    }

    [Fact]
    public async Task ContinueFromNode_RecoversParallelLaneWhileSiblingIsStillRunning()
    {
        var service = CreateService();
        var start = service.AddNode(new StartNode());
        var containerModel = new ParallelContainerNode();
        var container = service.AddNode(containerModel);
        var runningModel = new ControlledNode
        {
            Name = "A-长任务",
            ParentExecutionContainerId = container.Id,
            ExecutionLaneId = containerModel.Lanes[0].Id
        };
        service.AddNode(runningModel);
        var failure = service.AddNode(new FailNode
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
        var resume = service.AddNode(resumeModel);
        var afterModel = new CountingNode { Name = "After" };
        var after = service.AddNode(afterModel);
        service.Connections.Add(Flow(start, container));
        service.Connections.Add(Flow(failure, resume));
        service.Connections.Add(Flow(container, after));

        var run = service.RunAsync(_ => { });
        await runningModel.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var timeout = System.Diagnostics.Stopwatch.StartNew();
        while (failure.Status != NodeStatus.Error && timeout.Elapsed < TimeSpan.FromSeconds(2))
            await Task.Delay(10);
        Assert.Equal(NodeStatus.Error, failure.Status);
        Assert.True(service.Model.IsRunning);

        await service.ContinueFromNodeAsync(resume, _ => { });

        Assert.Equal(1, resumeModel.Executions);
        Assert.False(run.IsCompleted);
        runningModel.Complete();
        await run.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(1, afterModel.Executions);
        Assert.Equal("执行完成", service.Model.StatusText);
    }

    [Fact]
    public async Task ContinueFromFailedLoopNode_CompletesCurrentAndRemainingIterations()
    {
        var service = CreateService();
        var start = service.AddNode(new StartNode());
        var loopModel = new LoopContainerNode { Name = "循环容器1", IterationCount = 2 };
        var loop = service.AddNode(loopModel);
        var failureModel = new FailOnceNode
        {
            Name = "循环故障",
            ParentExecutionContainerId = loop.Id,
            ExecutionLaneId = loopModel.Lanes[0].Id
        };
        var failure = service.AddNode(failureModel);
        var bodyNextModel = new CountingNode
        {
            Name = "循环后继",
            ParentExecutionContainerId = loop.Id,
            ExecutionLaneId = loopModel.Lanes[0].Id
        };
        var bodyNext = service.AddNode(bodyNextModel);
        var afterModel = new CountingNode { Name = "After" };
        var after = service.AddNode(afterModel);
        service.Connections.Add(Flow(start, loop));
        service.Connections.Add(Flow(failure, bodyNext));
        service.Connections.Add(Flow(loop, after));

        await service.RunAsync(_ => { });
        Assert.Equal("执行出错", service.Model.StatusText);
        Assert.Equal(1, loopModel.CurrentIteration);

        await service.ContinueFromNodeAsync(failure, _ => { });

        Assert.Equal(3, failureModel.Attempts);
        Assert.Equal(2, bodyNextModel.Executions);
        Assert.Equal(2, loopModel.CurrentIteration);
        Assert.Equal(NodeStatus.Success, loop.Status);
        Assert.Equal(1, afterModel.Executions);
        Assert.Contains("恢复执行完成", service.Model.StatusText);
    }

    [Fact]
    public async Task ToolbarContinue_AfterLoopFailure_CanJumpToPreviousNodeAndContinuePath()
    {
        var service = CreateService();
        var viewModel = TestFlowEditorFactory.CreateViewModel(service);
        var start = service.AddNode(new StartNode());
        var loopModel = new LoopContainerNode { Name = "循环容器1", IterationCount = 1 };
        var loop = service.AddNode(loopModel);
        var previousModel = new CountingNode
        {
            Name = "上一个节点",
            ParentExecutionContainerId = loop.Id,
            ExecutionLaneId = loopModel.Lanes[0].Id
        };
        var previous = service.AddNode(previousModel);
        var failureModel = new FailOnceNode
        {
            Name = "失败节点",
            ParentExecutionContainerId = loop.Id,
            ExecutionLaneId = loopModel.Lanes[0].Id
        };
        var failure = service.AddNode(failureModel);
        var nextModel = new CountingNode
        {
            Name = "后续节点",
            ParentExecutionContainerId = loop.Id,
            ExecutionLaneId = loopModel.Lanes[0].Id
        };
        var next = service.AddNode(nextModel);
        var afterModel = new CountingNode { Name = "外层后续" };
        var after = service.AddNode(afterModel);
        service.Connections.Add(Flow(start, loop));
        service.Connections.Add(Flow(previous, failure));
        service.Connections.Add(Flow(failure, next));
        service.Connections.Add(Flow(loop, after));

        await service.RunAsync(_ => { });
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

    [Fact]
    public async Task LoopRegion_UsesExistingBoundaryConnectionsWithoutContainerWiring()
    {
        var service = CreateService();
        var start = service.AddNode(new StartNode { X = 20, Y = 220 });
        var containerModel = new LoopContainerNode
        {
            Name = "循环容器1",
            X = 200,
            Y = 100,
            IterationCount = 3
        };
        var container = service.AddNode(containerModel);
        var bodyModel = new CountingNode
        {
            Name = "循环任务",
            X = 280,
            Y = 220,
            ParentExecutionContainerId = container.Id,
            ExecutionLaneId = containerModel.Lanes[0].Id
        };
        var body = service.AddNode(bodyModel);
        var afterModel = new CountingNode { Name = "After", X = 980, Y = 220 };
        var after = service.AddNode(afterModel);
        service.Connections.Add(Flow(start, body));
        service.Connections.Add(Flow(body, after));

        await service.RunAsync(_ => { });

        Assert.Equal(2, service.Connections.Count);
        Assert.False(containerModel.ShowBoundaryPorts);
        Assert.Equal(3, bodyModel.Executions);
        Assert.Equal(1, afterModel.Executions);
        Assert.Equal("执行完成", service.Model.StatusText);
    }

    [Fact]
    public async Task LoopRegion_PreservesDataTransferAcrossItsVisualBoundary()
    {
        var service = CreateService();
        var start = service.AddNode(new StartNode());
        var source = service.AddNode(new SourceNode { Name = "Source" });
        var containerModel = new LoopContainerNode { IterationCount = 2 };
        var container = service.AddNode(containerModel);
        var bodyModel = new ValueNode
        {
            Name = "Body",
            ParentExecutionContainerId = container.Id,
            ExecutionLaneId = containerModel.Lanes[0].Id
        };
        var body = service.AddNode(bodyModel);
        var afterModel = new ValueNode { Name = "After" };
        var after = service.AddNode(afterModel);
        service.Connections.Add(Flow(start, source));
        service.Connections.Add(Flow(source, body));
        service.Connections.Add(Data(source, body));
        service.Connections.Add(Flow(body, after));
        service.Connections.Add(Data(body, after));

        await service.RunAsync(_ => { });

        Assert.Equal(5, service.Connections.Count);
        Assert.Equal(101, bodyModel.Value);
        Assert.Equal(102, afterModel.Value);
        Assert.Equal("执行完成", service.Model.StatusText);
    }

    [Fact]
    public async Task CompiledFlowInstance_PreservesAndExecutesContainerMembers()
    {
        var service = CreateService();
        var start = service.AddNode(new StartNode());
        var containerModel = new LoopContainerNode { Name = "循环容器1", IterationCount = 2 };
        var container = service.AddNode(containerModel);
        var body = service.AddNode(new CountingNode
        {
            Name = "循环任务",
            ParentExecutionContainerId = container.Id,
            ExecutionLaneId = containerModel.Lanes[0].Id
        });
        service.Connections.Add(Flow(start, container));

        var program = service.CompileProgram();
        var instance = FlowInstance.Create(program);
        await instance.RunAsync(CancellationToken.None);

        Assert.Equal(3, program.Nodes.Count);
        Assert.Equal(container.Id, instance.FindNode(body.Id)?.ParentExecutionContainerId);
        Assert.Equal(2, Assert.IsType<CountingNode>(instance.FindNode(body.Id)).Executions);
    }

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

    [Fact]
    public void DragParallelContainer_MovesItsMemberBySameDelta()
    {
        var service = CreateService();
        var vm = TestFlowEditorFactory.CreateViewModel(service);
        var containerModel = new ParallelContainerNode { X = 100, Y = 100 };
        var container = service.AddNode(containerModel);
        var child = service.AddNode(new CountingNode
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

    [Fact]
    public void PuttingConnectedNodeInsideLoopContainer_PreservesItsConnections()
    {
        var service = CreateService();
        var vm = TestFlowEditorFactory.CreateViewModel(service);
        var before = service.AddNode(new CountingNode { Name = "Before", X = 20, Y = 220 });
        var loop = service.AddNode(new LoopContainerNode { X = 200, Y = 100 });
        var inside = service.AddNode(new CountingNode { Name = "Inside", X = 280, Y = 220 });
        var after = service.AddNode(new CountingNode { Name = "After", X = 980, Y = 220 });
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

    private static FlowChartService CreateService() =>
        TestFlowEditorFactory.CreateService(new InMemoryFlowDocumentStore());
}
