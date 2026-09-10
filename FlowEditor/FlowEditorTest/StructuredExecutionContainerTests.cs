using System.Collections.Concurrent;
using System.Windows;
using FlowEditor.Core;
using FlowEditor.Models;
using FlowEditor.Nodes;
using FlowEditor.Nodes.FlowControl;
using FlowEditor.Services;
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

    [Fact]
    public async Task ParallelContainer_OnMainCanvas_StartsAllLanesAndWaitsForAll()
    {
        var service = new FlowChartService(
            new FlowChartPersistenceService(null!), new TestFlowChartExecutionEngine(), null!);
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
        var vm = new FlowChartViewModel(service, null!, null!, null!, new ConnectionService());
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
        var vm = new FlowChartViewModel(service, null!, null!, null!, new ConnectionService());
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
        new(new FlowChartPersistenceService(null!), new TestFlowChartExecutionEngine(), null!);
}
