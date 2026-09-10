using FlowEditor.Core;
using FlowEditor.Models;
using FlowEditor.Nodes;
using FlowEditor.Services;
using WPFCustomControls.PropertyGrid.Models;
using Xunit;
using static FlowEditorTest.TestGraph;

namespace FlowEditorTest;

public class PortBindingConsistencyTests
{
    private class PropertyNode : NodeModel
    {
        [NodeProperty(DisplayName = "属性A")]
        public string A { get; set; } = string.Empty;

        [NodeProperty(DisplayName = "属性B")]
        public string B { get; set; } = string.Empty;

        public override Task ExecuteAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private class CommunicationBaseNode : NodeModel
    {
        [NodeProperty(DisplayName = "设备名称")]
        public string DeviceName { get; set; } = "TCP1";

        public override Task ExecuteAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class TcpConnectNode : CommunicationBaseNode;

    [Fact]
    public void DisablePropertyOutput_RemovesItsConnection_AndReplacementPortCanReconnect()
    {
        var service = new FlowChartService(null!, new TestFlowChartExecutionEngine(), null!);
        var sourceModel = new PropertyNode { Name = "工具1" };
        var targetModel = new PropertyNode { Name = "工具2" };
        var source = service.AddNode(sourceModel);
        var target = service.AddNode(targetModel);
        sourceModel.SetPortConfig(nameof(PropertyNode.A), false, true);
        targetModel.SetPortConfig(nameof(PropertyNode.A), true, false);
        var sourceA = source.OutputPorts.Single(port => port.PropertyName == nameof(PropertyNode.A));
        var targetA = target.InputPorts.Single(port => port.PropertyName == nameof(PropertyNode.A));
        service.Connections.Add(new ConnectionModel
        {
            SourceNodeId = source.Id,
            SourcePortId = sourceA.Id,
            TargetNodeId = target.Id,
            TargetPortId = targetA.Id
        });

        sourceModel.SetPortConfig(nameof(PropertyNode.A), false, false);

        Assert.Empty(service.Connections);
        sourceModel.SetPortConfig(nameof(PropertyNode.B), false, true);
        var sourceB = source.OutputPorts.Single(port => port.PropertyName == nameof(PropertyNode.B));
        var plan = new ConnectionService().PlanConnection(
            sourceB, targetA, service.Nodes.Select(node => node.Model).ToList(), service.Connections);
        Assert.True(plan.Success, plan.Error);
    }

    [Fact]
    public void BindingUpstreamNodes_ContainsOnlyTransitivePredecessors()
    {
        var service = new FlowChartService(null!, new TestFlowChartExecutionEngine(), null!);
        var before = service.AddNode(new CountingNode { Name = "前置工具" });
        var current = service.AddNode(new PropertyNode { Name = "当前工具" });
        var after = service.AddNode(new CountingNode { Name = "后续工具" });
        var unrelated = service.AddNode(new CountingNode { Name = "无关工具" });
        service.Connections.Add(Flow(before, current));
        service.Connections.Add(Flow(current, after));

        var upstream = ((IDataBindingProvider)current.Model).GetUpstreamFlowNodes();

        var item = Assert.Single(upstream);
        Assert.Equal(before.Id, item.NodeId);
        Assert.DoesNotContain(upstream, node => node.NodeId == after.Id || node.NodeId == unrelated.Id);
    }

    [Fact]
    public void BindableOutputProperties_IncludePropertiesDeclaredOnToolBaseClasses()
    {
        var node = new TcpConnectNode();

        var properties = node.GetBindableProperties();

        Assert.Contains(nameof(CommunicationBaseNode.DeviceName), properties);
    }

    [Fact]
    public void RemovePropertyPort_ClearsInlineCheckStateAndRaisesConfigChanged()
    {
        var node = new PropertyNode();
        node.SetPortConfig(nameof(PropertyNode.A), isInput: true, isOutput: false);
        var port = node.InputPorts.Single(item => item.PropertyName == nameof(PropertyNode.A));
        var changeCount = 0;
        node.ConfigChanged += () => changeCount++;

        Assert.True(node.RemoveCustomPort(port.Id));

        Assert.Equal((false, false), node.GetPortConfig(nameof(PropertyNode.A)));
        Assert.Equal(1, changeCount);
    }
}
