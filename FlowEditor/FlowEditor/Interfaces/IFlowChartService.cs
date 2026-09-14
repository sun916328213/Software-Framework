using System.Collections.ObjectModel;
using FlowEditor.Models;
using FlowEditor.Nodes;

namespace FlowEditor.Interfaces;

/// <summary>
/// 节点模型访问所属流程所需的最小上下文。
/// 仅包含节点生命周期内的查询、通知和文档映射能力，不承载执行、文件、输出或视口命令。
/// </summary>
public interface IFlowChartService
{
    /// <summary>流程图运行时模型。</summary>
    FlowChartModel Model { get; }

    /// <summary>按画布顺序枚举所有节点模型。</summary>
    IEnumerable<NodeModel> Nodes { get; }

    /// <summary>所有连线。</summary>
    ObservableCollection<ConnectionModel> Connections { get; }

    /// <summary>宿主提供的节点专用编辑器启动器。</summary>
    INodeEditorLauncher NodeEditorLauncher { get; }

    /// <summary>宿主提供的节点信息提示通道。</summary>
    INodeNotificationSink NodeNotifications { get; }

    /// <summary>查找节点。</summary>
    NodeModel? FindNode(string nodeId);

    /// <summary>从节点文档重建节点模型（用于 ToolBlock 内部节点还原）。</summary>
    NodeModel RebuildNodeFromDocument(NodeDocumentModel doc);

}
