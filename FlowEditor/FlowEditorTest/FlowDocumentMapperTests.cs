using System.Text.Json;
using FlowEditor.Core;
using FlowEditor.Models;
using FlowEditor.Nodes;
using FlowEditor.Nodes.FlowControl;
using Xunit;

namespace FlowEditorTest;

/// <summary>验证流程文档 mapper 的节点、端口、连线与 JSON 兼容映射。</summary>
public sealed class FlowDocumentMapperTests
{
    /// <summary>验证完整流程映射保留格式版本、画布状态、节点配置和连线标识。</summary>
    [Fact]
    public void ToDocument_MapsCompleteFlowState()
    {
        var node = new BranchMergeNode { Name = "汇聚", InputCount = 3 };
        var connection = new ConnectionModel
        {
            Id = "connection-1",
            SourceNodeId = "source",
            SourcePortId = "source-port",
            TargetNodeId = node.Id,
            TargetPortId = node.InputPorts[0].Id
        };
        var model = new FlowChartModel { ZoomLevel = 0.75 };

        var document = new FlowDocumentMapper().ToDocument(
            "mapper-test", [node], [connection], model);

        Assert.Equal("1.0", document.Version);
        Assert.Equal("mapper-test", document.Name);
        Assert.Equal(0.75, document.ZoomLevel);
        var nodeDocument = Assert.Single(document.Nodes);
        Assert.Equal(node.Id, nodeDocument.Id);
        Assert.Equal(3, nodeDocument.CustomInputPorts.Count);
        Assert.All(nodeDocument.CustomInputPorts, port => Assert.Equal(PortType.Flow, port.DataType));
        Assert.True(nodeDocument.Properties.ContainsKey(nameof(BranchMergeNode.InputCount)));
        Assert.Equal("connection-1", Assert.Single(document.Connections).Id);
    }

    /// <summary>验证节点还原只通过注入的节点 factory seam 完成。</summary>
    [Fact]
    public void ToNode_DelegatesMaterializationToNodeFactory()
    {
        var expected = new StartNode();
        var factory = new RecordingNodeFactory(expected);
        var mapper = new FlowDocumentMapper(factory);
        var descriptor = new NodeDocumentModel { Id = "node-1" };

        var actual = mapper.ToNode(descriptor);

        Assert.Same(expected, actual);
        Assert.Same(descriptor, factory.LastDescriptor);
    }

    /// <summary>验证共享 JSON 约束继续输出 camelCase 字段和字符串枚举。</summary>
    [Fact]
    public void SharedJsonOptions_PreserveExistingDocumentFormat()
    {
        var node = new BranchMergeNode { ErrorPolicy = NodeErrorPolicy.ContinueOnError };
        var document = new FlowDocumentMapper().ToDocument(
            "json-test", [node], [], new FlowChartModel());

        var json = JsonSerializer.Serialize(document, FlowDocumentJson.Options);

        Assert.Contains("\"zoomLevel\"", json);
        Assert.Contains("\"errorPolicy\": \"ContinueOnError\"", json);
        Assert.DoesNotContain("\"ZoomLevel\"", json);
    }

    /// <summary>记录节点描述符并返回预设节点的测试 factory。</summary>
    private sealed class RecordingNodeFactory : INodeFactory
    {
        private readonly NodeModel _node;

        /// <summary>创建记录型节点 factory。</summary>
        /// <param name="node">每次创建时返回的节点。</param>
        public RecordingNodeFactory(NodeModel node)
        {
            _node = node;
        }

        /// <summary>最近一次收到的节点描述符。</summary>
        public NodeDocumentModel? LastDescriptor { get; private set; }

        /// <inheritdoc />
        public NodeModel Create(Type nodeType) => _node;

        /// <inheritdoc />
        public NodeModel Create(NodeDocumentModel descriptor)
        {
            LastDescriptor = descriptor;
            return _node;
        }
    }
}
