using System.Text.Json;
using System.IO;
using System.Security.Cryptography;
using FlowEditor.Core;
using FlowEditor.Models;
using FlowEditor.Nodes;
using FlowEditor.Nodes.FlowControl;
using FlowEditor.Services.Storage;
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

    /// <summary>验证自有日志等级沿用旧名称，历史日志节点文档可无损还原。</summary>
    [Fact]
    public void LogNodeLevel_RoundTripsLegacyEnumNames()
    {
        var mapper = new FlowDocumentMapper();
        var original = new LogNode { Level = FlowLogLevel.SysError };
        var document = mapper.ToDocument("log-level", [original], [], new FlowChartModel());
        var json = JsonSerializer.Serialize(document, FlowDocumentJson.Options);
        var restoredDocument = JsonSerializer.Deserialize<FlowChartDocumentModel>(
            json,
            FlowDocumentJson.Options);

        Assert.Contains("SysError", json, StringComparison.Ordinal);
        var restored = Assert.IsType<LogNode>(mapper.ToNode(Assert.Single(restoredDocument!.Nodes)));
        Assert.Equal(FlowLogLevel.SysError, restored.Level);
    }

    /// <summary>验证真实旧流程中的缺失节点、配置及连线端口在加载和回写后仍可保留。</summary>
    [Fact]
    public void HistoricalFlow_PreservesMissingNodesAndConnectionPorts()
    {
        var fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "CompatibilityFixtures",
            "InitializeFlow.legacy.flow");
        Assert.True(File.Exists(fixturePath), $"历史流程夹具不存在: {fixturePath}");
        Assert.Equal(
            "20D0BEBF9604E20E6100FF4103C09B04AA2C1E21B6BEDBE3D7546A94BDB3BAD1",
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(fixturePath))));

        var original = new JsonFlowDocumentStore().Load<FlowChartDocumentModel>(fixturePath);
        var factory = new ReflectionNodeFactory { AllowMissingNodes = true };
        var mapper = new FlowDocumentMapper(factory);
        var nodes = original.Nodes.Select(mapper.ToNode).ToList();
        var nodesById = nodes.ToDictionary(node => node.Id);

        Assert.Equal(8, nodes.Count);
        Assert.Equal(5, nodes.OfType<MissingNode>().Count());
        Assert.Equal(7, original.Connections.Count);
        foreach (var connection in original.Connections)
        {
            Assert.Contains(nodesById[connection.SourceNodeId].OutputPorts,
                port => port.Id == connection.SourcePortId);
            Assert.Contains(nodesById[connection.TargetNodeId].InputPorts,
                port => port.Id == connection.TargetPortId);
        }

        var roundTripped = mapper.ToDocument(
            original.Name,
            nodes,
            original.Connections.Select(mapper.ToConnection),
            new FlowChartModel { ZoomLevel = original.ZoomLevel });
        Assert.Equal(original.Nodes.Select((node, index) => nodes[index] is MissingNode
                ? node.TypeName
                : nodes[index].GetType().AssemblyQualifiedName),
            roundTripped.Nodes.Select(node => node.TypeName));
        Assert.Equal(original.Connections.Select(connection => connection.Id),
            roundTripped.Connections.Select(connection => connection.Id));

        foreach (var oldNode in original.Nodes.Where(node => node.TypeName.Contains(".Communication.MC.")))
        {
            var savedNode = Assert.Single(roundTripped.Nodes, node => node.Id == oldNode.Id);
            Assert.Equal(JsonSerializer.Serialize(oldNode.Properties),
                JsonSerializer.Serialize(savedNode.Properties));
            Assert.Equal(oldNode.PortIdMap, savedNode.PortIdMap);
        }
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
