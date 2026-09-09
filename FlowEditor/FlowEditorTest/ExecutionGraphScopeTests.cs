using FlowEditor.Core;
using FlowEditor.Models;
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
            new List<NodeViewModel> { start, parked, active },
            new List<ConnectionModel> { Flow(start, parked), Flow(start, active), Flow(parked, active) });

        Assert.Equal([start, active], graph.Nodes);
        var connection = Assert.Single(graph.Connections);
        Assert.Equal(start.Id, connection.SourceNodeId);
        Assert.Equal(active.Id, connection.TargetNodeId);
    }
}
