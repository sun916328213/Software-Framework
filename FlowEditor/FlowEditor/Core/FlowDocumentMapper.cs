using System.Reflection;
using System.Text.Json.Serialization;
using FlowEditor.Models;
using FlowEditor.Nodes;

namespace FlowEditor.Core;

/// <summary>
/// 在运行时模型与流程文档 DTO 之间执行纯内存映射，集中隐藏节点派生属性、
/// 动态端口、端口 ID 和连线字段的兼容规则。
/// </summary>
public sealed class FlowDocumentMapper
{
    private readonly INodeFactory _nodeFactory;

    /// <summary>创建流程文档 mapper。</summary>
    /// <param name="nodeFactory">根据节点文档物化运行时节点的 factory。</param>
    public FlowDocumentMapper(INodeFactory? nodeFactory = null)
    {
        _nodeFactory = nodeFactory ?? new ReflectionNodeFactory();
    }

    /// <summary>从当前画布状态构建完整流程文档。</summary>
    /// <param name="name">流程名称。</param>
    /// <param name="nodes">运行时节点集合。</param>
    /// <param name="connections">运行时连线集合。</param>
    /// <param name="model">画布全局状态。</param>
    /// <returns>可持久化的流程文档。</returns>
    public FlowChartDocumentModel ToDocument(
        string name,
        IEnumerable<NodeModel> nodes,
        IEnumerable<ConnectionModel> connections,
        FlowChartModel model)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(connections);
        ArgumentNullException.ThrowIfNull(model);

        return new FlowChartDocumentModel
        {
            Name = name,
            Version = "1.0",
            ZoomLevel = model.ZoomLevel,
            Nodes = nodes.Select(ToNodeDocument).ToList(),
            Connections = connections.Select(ToConnectionDocument).ToList()
        };
    }

    /// <summary>将运行时节点映射为节点文档。</summary>
    /// <param name="node">待映射节点。</param>
    /// <returns>包含派生属性和动态端口的节点文档。</returns>
    public NodeDocumentModel ToNodeDocument(NodeModel node)
    {
        ArgumentNullException.ThrowIfNull(node);
        // P2：缺失占位节点 round-trip 必须保留原始标识与配置，不得用占位类型覆盖。
        var missing = node as MissingNode;
        var typeId = missing?.OriginalNodeTypeId
            ?? NodeTypeRegistry.TryGetId(node.GetType()) ?? string.Empty;
        return new NodeDocumentModel
        {
            Id = node.Id,
            TypeName = missing?.OriginalTypeName
                ?? node.GetType().AssemblyQualifiedName ?? node.GetType().FullName ?? string.Empty,
            NodeTypeId = typeId,
            SchemaVersion = string.IsNullOrWhiteSpace(typeId) ? 1 : 2,
            Name = node.Name,
            X = node.X,
            Y = node.Y,
            IsInEnabled = node.IsInEnabled,
            IsOutEnabled = node.IsOutEnabled,
            Permission = node.Permission,
            IsExcludedFromExecution = node.IsExcludedFromExecution,
            ParentExecutionContainerId = node.ParentExecutionContainerId,
            ExecutionLaneId = node.ExecutionLaneId,
            ErrorPolicy = node.ErrorPolicy,
            RetryCount = node.RetryCount,
            RetryDelayMs = node.RetryDelayMs,
            TimeoutMs = node.TimeoutMs,
            Properties = SerializeNodeProperties(node),
            PortIdMap = BuildPortIdMap(node),
            CustomInputPorts = node.CustomInputPorts.Select(ToCustomPortDocument).ToList(),
            CustomOutputPorts = node.CustomOutputPorts.Select(ToCustomPortDocument).ToList()
        };
    }

    /// <summary>将连线映射为连线文档。</summary>
    /// <param name="connection">待映射连线。</param>
    /// <returns>独立的连线文档。</returns>
    public ConnectionDocumentModel ToConnectionDocument(ConnectionModel connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        return new ConnectionDocumentModel
        {
            Id = connection.Id,
            SourceNodeId = connection.SourceNodeId,
            SourcePortId = connection.SourcePortId,
            TargetNodeId = connection.TargetNodeId,
            TargetPortId = connection.TargetPortId
        };
    }

    /// <summary>通过节点 factory 将节点文档还原为运行时节点。</summary>
    /// <param name="document">节点文档。</param>
    /// <returns>独立的运行时节点实例。</returns>
    public NodeModel ToNode(NodeDocumentModel document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return _nodeFactory.Create(document);
    }

    /// <summary>将连线文档还原为独立的运行时连线。</summary>
    /// <param name="document">连线文档。</param>
    /// <returns>运行时连线。</returns>
    public ConnectionModel ToConnection(ConnectionDocumentModel document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new ConnectionModel
        {
            Id = document.Id,
            SourceNodeId = document.SourceNodeId,
            SourcePortId = document.SourcePortId,
            TargetNodeId = document.TargetNodeId,
            TargetPortId = document.TargetPortId
        };
    }

    /// <summary>将动态端口映射为端口文档。</summary>
    /// <param name="port">待映射端口。</param>
    /// <returns>动态端口文档。</returns>
    private static CustomPortDocument ToCustomPortDocument(PortModel port)
        => new()
        {
            Id = port.Id,
            Name = port.Name,
            BoundProperty = port.PropertyName,
            DataType = port.DataType
        };

    /// <summary>读取节点派生类型可持久化的公开属性。</summary>
    /// <param name="node">待读取节点。</param>
    /// <returns>属性名到当前值的映射。</returns>
    private static Dictionary<string, object?> SerializeNodeProperties(NodeModel node)
    {
        // P2：占位节点直接回写捕获的原始配置，不反射占位类自身的簿记属性。
        if (node is MissingNode missing)
            return new Dictionary<string, object?>(missing.RawProperties);
        var properties = new Dictionary<string, object?>();
        var baseProperties = typeof(NodeModel).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .ToHashSet();

        foreach (var property in node.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (baseProperties.Contains(property.Name) || !property.CanRead || !property.CanWrite ||
                property.GetIndexParameters().Length > 0 ||
                property.GetCustomAttribute<JsonIgnoreAttribute>() != null)
            {
                continue;
            }

            try
            {
                properties[property.Name] = property.GetValue(node);
            }
            catch
            {
                // 单个扩展属性读取失败不应阻止其余节点数据保存。
            }
        }

        return properties;
    }

    /// <summary>构建内建及动态端口的稳定名称到 ID 映射。</summary>
    /// <param name="node">端口所属节点。</param>
    /// <returns>方向与端口名到端口 ID 的映射。</returns>
    private static Dictionary<string, string> BuildPortIdMap(NodeModel node)
    {
        var map = new Dictionary<string, string>();
        foreach (var port in node.InputPorts)
            map[$"IN:{port.Name}"] = port.Id;
        foreach (var port in node.OutputPorts)
            map[$"OUT:{port.Name}"] = port.Id;
        return map;
    }
}
