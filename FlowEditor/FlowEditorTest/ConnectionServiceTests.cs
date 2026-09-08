using FlowEditor.Models;
using FlowEditor.Nodes;
using FlowEditor.Services;
using Xunit;
using static FlowEditorTest.TestGraph;

namespace FlowEditorTest;

public class ConnectionServiceTests
{
    private readonly ConnectionService _service = new();

    [Fact]
    public void PlanConnection_RejectsIncompatibleDataTypes()
    {
        var source = PortModel.CreateOutput("value", PortType.Integer, "source");
        var target = PortModel.CreateInput("value", PortType.String, "target");
        var connections = new List<ConnectionModel>();

        var result = _service.PlanConnection(source, target, Array.Empty<NodeModel>(), connections);

        Assert.False(result.Success);
        Assert.Contains("类型不兼容", result.Error);
        Assert.Empty(connections);
    }

    [Fact]
    public void PlanConnection_ReturnsPlanWithoutMutatingExistingConnections()
    {
        var source = PortModel.CreateOutput("value", PortType.Integer, "source");
        var target = PortModel.CreateInput("value", PortType.Integer, "target");
        var connections = new List<ConnectionModel>();

        var result = _service.PlanConnection(source, target, Array.Empty<NodeModel>(), connections);

        Assert.True(result.Success);
        Assert.Empty(connections);
        Assert.Same(result.PrimaryConnection, Assert.Single(result.Connections));
    }

    [Fact]
    public void PlanConnection_FlowConnectionIncludesMatchingTypedBindings()
    {
        var (source, sourceModel) = New<ValueNode>("source");
        var (target, targetModel) = New<ValueNode>("target");
        var sourceFlow = source.OutputPorts.First(p => p.DataType == PortType.Flow);
        var targetFlow = target.InputPorts.First(p => p.DataType == PortType.Flow);
        var connections = new List<ConnectionModel>();

        var result = _service.PlanConnection(
            sourceFlow,
            targetFlow,
            new NodeModel[] { source.Model, target.Model },
            connections);

        Assert.True(result.Success);
        Assert.Equal(1, result.AutoBoundCount);
        Assert.Empty(connections);
        Assert.Equal(2, result.Connections.Count);
        Assert.Equal(PortType.Integer, sourceModel.OutputPorts.First(p => p.Name == "Value").DataType);
        Assert.Equal(PortType.Integer, targetModel.InputPorts.First(p => p.Name == "Value").DataType);
    }

    [Theory]
    [InlineData(typeof(bool), PortType.Boolean)]
    [InlineData(typeof(int), PortType.Integer)]
    [InlineData(typeof(double), PortType.Double)]
    [InlineData(typeof(string), PortType.String)]
    public void PortTypeClassifier_MapsClrTypes(Type clrType, PortType expected)
        => Assert.Equal(expected, PortTypeClassifier.FromClrType(clrType));
}
