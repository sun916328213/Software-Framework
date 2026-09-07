using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using FlowEditor.Core;
using FlowEditor.Interfaces;
using FlowEditor.Models;
using FlowEditor.ViewModels;
using Prism.Ioc;

namespace FlowEditor.Services;

/// <summary>
/// 多流程容器文档服务。
/// 负责多流程文件(.flows)与 <see cref="FlowChartContainerDocumentModel"/> 之间的
/// 序列化/反序列化，并将容器级文档构建/还原为标签页集合。
/// </summary>
public class FlowChartContainerDocumentService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly IFlowChartPersistenceService _persistence;
    private readonly IContainerProvider _containerProvider;

    public FlowChartContainerDocumentService(
        IFlowChartPersistenceService persistence,
        IContainerProvider containerProvider)
    {
        _persistence = persistence;
        _containerProvider = containerProvider;
    }

    /// <summary>将容器文档保存到文件。</summary>
    public void Save(string path, FlowChartContainerDocumentModel containerDoc)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(containerDoc, JsonOptions);
        File.WriteAllText(path, json);
    }

    /// <summary>从文件加载容器文档。</summary>
    public FlowChartContainerDocumentModel Load(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<FlowChartContainerDocumentModel>(json, JsonOptions)
               ?? throw new InvalidDataException("多流程文件内容为空");
    }

    /// <summary>从标签页集合构建容器文档。</summary>
    public FlowChartContainerDocumentModel BuildContainerDocument(IEnumerable<FlowChartViewModel> tabs)
    {
        var containerDoc = new FlowChartContainerDocumentModel();
        foreach (var tab in tabs)
        {
            var doc = _persistence.BuildDocument(tab.Model.TabTitle, tab.Nodes, tab.Connections, tab.Model);
            containerDoc.Flows.Add(doc);
        }
        return containerDoc;
    }

    /// <summary>根据容器文档创建一个新的标签页 ViewModel（含节点与连线还原）。</summary>
    public FlowChartViewModel CreateTabFromFlow(FlowChartDocumentModel flowDoc)
    {
        var vm = _containerProvider.Resolve<FlowChartViewModel>();

        // 还原标题与全局配置
        vm.Model.TabTitle = flowDoc.Name;
        vm.Model.ZoomLevel = flowDoc.ZoomLevel;
        vm.Model.IsModified = false;
        vm.Model.StatusText = "已加载";

        // 重建节点
        foreach (var nodeDoc in flowDoc.Nodes)
        {
            var node = _persistence.RebuildNode(nodeDoc);
            vm.LoadNodeModel(node);
        }

        // 还原连线
        foreach (var connDoc in flowDoc.Connections)
        {
            vm.Connections.Add(new ConnectionModel
            {
                Id = connDoc.Id,
                SourceNodeId = connDoc.SourceNodeId,
                SourcePortId = connDoc.SourcePortId,
                TargetNodeId = connDoc.TargetNodeId,
                TargetPortId = connDoc.TargetPortId
            });
        }

        return vm;
    }

    /// <summary>构建单个流程文档（用于 ToolBlock 内部数据保存等场景）。</summary>
    public FlowChartDocumentModel BuildFlowDocument(FlowChartViewModel tab)
        => _persistence.BuildDocument(tab.Model.TabTitle, tab.Nodes, tab.Connections, tab.Model);

    /// <summary>将一个流程文档加载到指定标签页（用于 ToolBlock 内部数据加载）。</summary>
    public void LoadFlowToTab(FlowChartViewModel tab, FlowChartDocumentModel flowDoc)
    {
        tab.Model.TabTitle = flowDoc.Name;

        foreach (var nodeDoc in flowDoc.Nodes)
        {
            var node = _persistence.RebuildNode(nodeDoc);
            tab.LoadNodeModel(node);
        }

        foreach (var connDoc in flowDoc.Connections)
        {
            tab.Connections.Add(new ConnectionModel
            {
                Id = connDoc.Id,
                SourceNodeId = connDoc.SourceNodeId,
                SourcePortId = connDoc.SourcePortId,
                TargetNodeId = connDoc.TargetNodeId,
                TargetPortId = connDoc.TargetPortId
            });
        }

        tab.Model.ZoomLevel = flowDoc.ZoomLevel;
        tab.Model.IsModified = false;
    }
}
