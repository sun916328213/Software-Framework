using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using FlowEditor.Interfaces;
using FlowEditor.Models;
using FlowEditor.Nodes;
using FlowEditor.ViewModels;
using Prism.Ioc;

namespace FlowEditor.Core;

/// <summary>
/// 流程图持久化服务。
/// 负责流程图文档模型与 .flow 文件之间的序列化/反序列化。
/// 通过反射还原节点类型实例，支持完整 1:1 还原。
/// </summary>
public class FlowChartPersistenceService : IFlowChartPersistenceService
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly IContainerProvider _containerProvider;

    public FlowChartPersistenceService(IContainerProvider containerProvider)
    {
        _containerProvider = containerProvider;
    }

    /// <summary>保存流程图文档到 .flow 文件。</summary>
    public void Save(string path, FlowChartDocumentModel doc)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(doc, _jsonOptions);
        File.WriteAllText(path, json);
    }

    /// <summary>从 .flow 文件加载流程图文档。</summary>
    public FlowChartDocumentModel Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("流程图文件不存在", path);
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<FlowChartDocumentModel>(json, _jsonOptions)
               ?? throw new InvalidDataException("流程图文件内容为空");
    }

    /// <summary>构建流程图文档模型（从当前画布状态）。</summary>
    public FlowChartDocumentModel BuildDocument(
        string name,
        IEnumerable<NodeViewModel> nodes,
        IEnumerable<ConnectionModel> connections,
        FlowChartModel model)
    {
        var doc = new FlowChartDocumentModel
        {
            Name = name,
            Version = "1.0",
            ZoomLevel = model.ZoomLevel,
            Nodes = new List<NodeDocumentModel>(),
            Connections = connections.Select(c => new ConnectionDocumentModel
            {
                Id = c.Id,
                SourceNodeId = c.SourceNodeId,
                SourcePortId = c.SourcePortId,
                TargetNodeId = c.TargetNodeId,
                TargetPortId = c.TargetPortId
            }).ToList()
        };

        foreach (var nodeVm in nodes)
        {
            var nodeModel = nodeVm.Model;
            var nodeDoc = new NodeDocumentModel
            {
                Id = nodeModel.Id,
                TypeName = nodeModel.GetType().AssemblyQualifiedName ?? nodeModel.GetType().FullName ?? "",
                Name = nodeModel.Name,
                X = nodeModel.X,
                Y = nodeModel.Y,
                IsInEnabled = nodeModel.IsInEnabled,
                IsOutEnabled = nodeModel.IsOutEnabled,
                Permission = nodeModel.Permission,
                Properties = SerializeNodeProperties(nodeModel),
                PortIdMap = BuildPortIdMap(nodeModel)
            };

            // 序列化自定义端口
            foreach (var port in nodeModel.CustomInputPorts)
            {
                nodeDoc.CustomInputPorts.Add(new CustomPortDocument
                {
                    Id = port.Id,
                    Name = port.Name,
                    BoundProperty = port.PropertyName
                });
            }
            foreach (var port in nodeModel.CustomOutputPorts)
            {
                nodeDoc.CustomOutputPorts.Add(new CustomPortDocument
                {
                    Id = port.Id,
                    Name = port.Name,
                    BoundProperty = port.PropertyName
                });
            }

            doc.Nodes.Add(nodeDoc);
        }
        return doc;
    }

    /// <summary>从节点模型构建节点文档（用于 ToolBlock 内部节点序列化）。</summary>
    public NodeDocumentModel BuildNodeDocument(NodeModel nodeModel)
    {
        var nodeDoc = new NodeDocumentModel
        {
            Id = nodeModel.Id,
            TypeName = nodeModel.GetType().AssemblyQualifiedName ?? nodeModel.GetType().FullName ?? "",
            Name = nodeModel.Name,
            X = nodeModel.X,
            Y = nodeModel.Y,
            IsInEnabled = nodeModel.IsInEnabled,
            IsOutEnabled = nodeModel.IsOutEnabled,
            Permission = nodeModel.Permission,
            Properties = SerializeNodeProperties(nodeModel),
            PortIdMap = BuildPortIdMap(nodeModel)
        };

        foreach (var port in nodeModel.CustomInputPorts)
        {
            nodeDoc.CustomInputPorts.Add(new CustomPortDocument
            {
                Id = port.Id,
                Name = port.Name,
                BoundProperty = port.PropertyName
            });
        }
        foreach (var port in nodeModel.CustomOutputPorts)
        {
            nodeDoc.CustomOutputPorts.Add(new CustomPortDocument
            {
                Id = port.Id,
                Name = port.Name,
                BoundProperty = port.PropertyName
            });
        }

        return nodeDoc;
    }

    /// <summary>从文档模型还原节点实例。</summary>
    public NodeModel RebuildNode(NodeDocumentModel doc)
    {
        var type = ResolveType(doc.TypeName)
                   ?? throw new TypeLoadException($"无法找到节点类型: {doc.TypeName}");
        if (!typeof(NodeModel).IsAssignableFrom(type))
            throw new InvalidOperationException($"类型 {type.Name} 不是有效的节点类型");

        var node = (NodeModel)_containerProvider.Resolve(type);

        // 还原节点 ID（关键！连线依赖此 ID）
        node.Id = doc.Id;

        // 还原基本属性
        node.Name = doc.Name;
        node.X = doc.X;
        node.Y = doc.Y;
        node.IsInEnabled = doc.IsInEnabled;
        node.IsOutEnabled = doc.IsOutEnabled;
        node.Permission = doc.Permission;

        // 还原自定义端口（使用保存的 ID）
        foreach (var portDoc in doc.CustomInputPorts)
        {
            var port = node.AddCustomInputPort(portDoc.Name);
            port.Id = portDoc.Id; // 恢复原始端口 ID
        }
        foreach (var portDoc in doc.CustomOutputPorts)
        {
            var port = node.AddCustomOutputPort(portDoc.Name, portDoc.BoundProperty);
            port.Id = portDoc.Id; // 恢复原始端口 ID
        }

        // 还原附加属性
        DeserializeNodeProperties(node, doc.Properties);

        return node;
    }

    private static Type? ResolveType(string typeName)
    {
        var type = TryResolve(typeName);

        // 旧版兼容：节点迁移到 FlowNodes 程序集前的 .flow 文件使用 FlowEditor.Nodes.* 类型名
        if (type == null && typeName.Contains("FlowEditor.Nodes."))
            type = TryResolve(typeName.Replace("FlowEditor.Nodes.", "FlowNodes."));

        return type;
    }

    private static Type? TryResolve(string typeName)
    {
        var type = Type.GetType(typeName);
        if (type != null) return type;
        var shortName = typeName.Split(',')[0].Trim();
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                type = asm.GetType(shortName);
                if (type != null) return type;
            }
            catch { }
        }
        return null;
    }

    private static Dictionary<string, object?> SerializeNodeProperties(NodeModel node)
    {
        var props = new Dictionary<string, object?>();
        var type = node.GetType();
        var baseProps = typeof(NodeModel).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name).ToHashSet();
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (baseProps.Contains(prop.Name)) continue;
            if (!prop.CanRead || !prop.CanWrite) continue;
            if (prop.GetIndexParameters().Length > 0) continue;
            if (prop.GetCustomAttribute<JsonIgnoreAttribute>() != null) continue;
            try { props[prop.Name] = prop.GetValue(node); } catch { }
        }
        return props;
    }

    private static void DeserializeNodeProperties(NodeModel node, Dictionary<string, object?> properties)
    {
        var type = node.GetType();
        foreach (var (key, value) in properties)
        {
            var prop = type.GetProperty(key, BindingFlags.Public | BindingFlags.Instance);
            if (prop == null || !prop.CanWrite) continue;
            try
            {
                if (value is JsonElement je)
                {
                    var converted = JsonSerializer.Deserialize(je.GetRawText(), prop.PropertyType, _jsonOptions);
                    prop.SetValue(node, converted);
                }
                else prop.SetValue(node, value);
            }
            catch { }
        }
    }

    private static Dictionary<string, string> BuildPortIdMap(NodeModel node)
    {
        var map = new Dictionary<string, string>();
        foreach (var p in node.InputPorts) map[$"IN:{p.Name}"] = p.Id;
        foreach (var p in node.OutputPorts) map[$"OUT:{p.Name}"] = p.Id;
        return map;
    }
}
