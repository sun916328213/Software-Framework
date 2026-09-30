using FlowEditor.Core;
using FlowEditor.Models;
using FlowEditor.Nodes;
using FlowEditor.ViewModels;
using Xunit;
using static FlowEditorTest.TestGraph;

namespace FlowEditorTest;

public class ExecutionGraphScopeTests
{
    [Fact]
    public void Create_ExcludedNodeAndItsConnections_AreNotExecutable()
    {
        var start = New<ValueNode>("Start").Vm;
        var parked = New<ValueNode>("Parked").Vm;
        var active = New<ValueNode>("Active").Vm;
        parked.IsExcludedFromExecution = true;

        var graph = ExecutionGraphScope.Create(
            new List<NodeModel> { start.Model, parked.Model, active.Model },
            new List<ConnectionModel> { Flow(start, parked), Flow(start, active), Flow(parked, active) });

        Assert.Equal([start.Model, active.Model], graph.Nodes);
        var connection = Assert.Single(graph.Connections);
        Assert.Equal(start.Id, connection.SourceNodeId);
        Assert.Equal(active.Id, connection.TargetNodeId);
    }

    [Fact]
    public void Create_SeparatesTopLevelAndContainerLaneScopes()
    {
        var top = New<ValueNode>("Top").Vm;
        var laneOne = New<ValueNode>("LaneOne").Vm;
        var laneTwo = New<ValueNode>("LaneTwo").Vm;
        laneOne.ParentExecutionContainerId = "container";
        laneOne.ExecutionLaneId = "lane-1";
        laneTwo.ParentExecutionContainerId = "container";
        laneTwo.ExecutionLaneId = "lane-2";

        var nodes = new[] { top.Model, laneOne.Model, laneTwo.Model };
        var topScope = ExecutionGraphScope.Create(nodes, []);
        var firstLane = ExecutionGraphScope.Create(nodes, [], "container", "lane-1");

        Assert.Equal([top.Model], topScope.Nodes);
        Assert.Equal([laneOne.Model], firstLane.Nodes);
    }
}
