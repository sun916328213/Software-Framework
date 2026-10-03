// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：IFlowChartService。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、System.Collections.ObjectModel、FlowEditor.Controls.Models、FlowEditor.Core.Models、FlowEditor.Controls.Nodes、FlowEditor.Core.Nodes。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using System.Collections.ObjectModel;
using FlowEditor.Controls.Models;
using FlowEditor.Core.Models;
using FlowEditor.Controls.Nodes;
using FlowEditor.Core.Nodes;

namespace FlowEditor.Controls.Interfaces;

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
