using FlowEditor.Models;
using FlowEditor.Nodes;

namespace FlowEditor.Interfaces;

/// <summary>
/// 流程图持久化服务接口。
/// 负责流程图文档模型与 .flow 文件之间的序列化/反序列化，支持 1:1 还原。
/// </summary>
public interface IFlowChartPersistenceService
{
    /// <summary>保存流程图文档到 .flow 文件。</summary>
    void Save(string path, FlowChartDocumentModel doc);

    /// <summary>从 .flow 文件加载流程图文档。</summary>
    FlowChartDocumentModel Load(string path);

    /// <summary>构建流程图文档模型（从当前画布状态）。</summary>
    FlowChartDocumentModel BuildDocument(
        string name,
        IEnumerable<ViewModels.NodeViewModel> nodes,
        IEnumerable<ConnectionModel> connections,
        FlowChartModel model);

    /// <summary>从节点模型构建节点文档。</summary>
    NodeDocumentModel BuildNodeDocument(NodeModel node);

    /// <summary>从文档模型还原节点实例。</summary>
    NodeModel RebuildNode(NodeDocumentModel doc);
}
