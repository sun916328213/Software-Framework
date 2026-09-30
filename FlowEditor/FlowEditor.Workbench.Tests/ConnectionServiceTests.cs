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

    [Fact]
    public void PlanConnection_RejectsSecondTargetFromSameFlowOutput()
    {
        var (source, _) = New<CountingNode>("source");
        var (first, _) = New<CountingNode>("first");
        var (second, _) = New<CountingNode>("second");
        var existing = new List<ConnectionModel> { Flow(source, first) };

        var result = _service.PlanConnection(
            source.OutputPorts.Single(),
            second.InputPorts.Single(),
            [source.Model, first.Model, second.Model],
            existing);

        Assert.False(result.Success);
        Assert.Contains("并行分叉", result.Error);
    }

    [Fact]
    public void PlanConnection_RejectsSecondSourceIntoSameFlowInput()
    {
        var (first, _) = New<CountingNode>("first");
        var (second, _) = New<CountingNode>("second");
        var (target, _) = New<CountingNode>("target");
        var existing = new List<ConnectionModel> { Flow(first, target) };

        var result = _service.PlanConnection(
            second.OutputPorts.Single(),
            target.InputPorts.Single(),
            [first.Model, second.Model, target.Model],
            existing);

        Assert.False(result.Success);
        Assert.Contains("分支汇聚", result.Error);
    }

    [Fact]
    public void PlanConnection_DataOutputMayFanOutToDifferentInputs()
    {
        var (source, _) = New<SourceNode>("source");
        var (first, _) = New<ValueNode>("first");
        var (second, _) = New<ValueNode>("second");
        var output = source.OutputPorts.Single(port => port.Name == "Value");
        var firstInput = first.InputPorts.Single(port => port.Name == "Value");
        var secondInput = second.InputPorts.Single(port => port.Name == "Value");
        var existing = new List<ConnectionModel>
        {
            new()
            {
                SourceNodeId = source.Id,
                SourcePortId = output.Id,
                TargetNodeId = first.Id,
                TargetPortId = firstInput.Id
            }
        };

        var result = _service.PlanConnection(
            output, secondInput, [source.Model, first.Model, second.Model], existing);

        Assert.True(result.Success, result.Error);
    }

    [Theory]
    [InlineData(typeof(bool), PortType.Boolean)]
    [InlineData(typeof(int), PortType.Integer)]
    [InlineData(typeof(double), PortType.Double)]
    [InlineData(typeof(string), PortType.String)]
    public void PortTypeClassifier_MapsClrTypes(Type clrType, PortType expected)
        => Assert.Equal(expected, PortTypeClassifier.FromClrType(clrType));
}
