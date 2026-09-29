using FlowEditor.Core;
using FlowEditor.Interfaces;
using FlowEditor.Models;
using FlowEditor.ViewModels;

namespace FlowEditor.Services;

/// <summary>
/// 多流程容器文档服务。
/// 负责多流程文件(.flows)与 <see cref="FlowChartContainerDocumentModel"/> 之间的
/// 序列化/反序列化，并将容器级文档构建/还原为标签页集合。
/// </summary>
public class FlowChartContainerDocumentService
{
    private readonly FlowDocumentMapper _documentMapper;
    private readonly IFlowDocumentStore _documentStore;
    private readonly IFlowTabFactory _tabFactory;

    /// <summary>创建多流程文档协调模块。</summary>
    /// <param name="documentMapper">运行时模型与文档 DTO 之间的 mapper。</param>
    /// <param name="documentStore">流程文档存储 adapter。</param>
    /// <param name="tabFactory">创建独立流程标签页的 factory。</param>
    public FlowChartContainerDocumentService(
        FlowDocumentMapper documentMapper,
        IFlowDocumentStore documentStore,
        IFlowTabFactory tabFactory)
    {
        _documentMapper = documentMapper ?? throw new ArgumentNullException(nameof(documentMapper));
        _documentStore = documentStore ?? throw new ArgumentNullException(nameof(documentStore));
        _tabFactory = tabFactory ?? throw new ArgumentNullException(nameof(tabFactory));
    }

    /// <summary>将容器文档保存到文件。</summary>
    public void Save(string path, FlowChartContainerDocumentModel containerDoc)
        => _documentStore.Save(path, containerDoc);

    /// <summary>从文件加载容器文档。</summary>
    public FlowChartContainerDocumentModel Load(string path)
        => _documentStore.Load<FlowChartContainerDocumentModel>(path);

    /// <summary>从标签页集合构建容器文档。</summary>
    public FlowChartContainerDocumentModel BuildContainerDocument(IEnumerable<FlowChartViewModel> tabs)
    {
        var containerDoc = new FlowChartContainerDocumentModel();
        foreach (var tab in tabs)
        {
            var doc = _documentMapper.ToDocument(
                tab.Model.TabTitle,
                tab.Nodes.Select(node => node.Model),
                tab.Connections,
                tab.Model);
            containerDoc.Flows.Add(doc);
        }
        return containerDoc;
    }

    /// <summary>根据容器文档创建一个新的标签页 ViewModel（含节点与连线还原）。</summary>
    public FlowChartViewModel CreateTabFromFlow(FlowChartDocumentModel flowDoc)
    {
        var vm = _tabFactory.Create();

        // 还原标题与全局配置
        vm.SetTabTitle(flowDoc.Name, markModified: false);
        vm.Model.ZoomLevel = flowDoc.ZoomLevel;
        vm.Model.IsModified = false;
        vm.Model.StatusText = "已加载";

        // 重建节点
        foreach (var nodeDoc in flowDoc.Nodes)
        {
            var node = _documentMapper.ToNode(nodeDoc);
            vm.LoadNodeModel(node);
        }

        // 还原连线
        foreach (var connDoc in flowDoc.Connections)
        {
            vm.Connections.Add(_documentMapper.ToConnection(connDoc));
        }

        return vm;
    }

    /// <summary>构建单个流程文档（用于 ToolBlock 内部数据保存等场景）。</summary>
    public FlowChartDocumentModel BuildFlowDocument(FlowChartViewModel tab)
        => _documentMapper.ToDocument(
            tab.Model.TabTitle,
            tab.Nodes.Select(node => node.Model),
            tab.Connections,
            tab.Model);

    /// <summary>将一个流程文档加载到指定标签页（用于 ToolBlock 内部数据加载）。</summary>
    public void LoadFlowToTab(FlowChartViewModel tab, FlowChartDocumentModel flowDoc)
    {
        tab.SetTabTitle(flowDoc.Name, markModified: false);

        foreach (var nodeDoc in flowDoc.Nodes)
        {
            var node = _documentMapper.ToNode(nodeDoc);
            tab.LoadNodeModel(node);
        }

        foreach (var connDoc in flowDoc.Connections)
        {
            tab.Connections.Add(_documentMapper.ToConnection(connDoc));
        }

        tab.Model.ZoomLevel = flowDoc.ZoomLevel;
        tab.Model.IsModified = false;
    }
}
