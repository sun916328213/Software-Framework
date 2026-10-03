// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：IInternalFlowContainerNode。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、FlowEditor.Controls.Nodes、FlowEditor.Core.Nodes、FlowEditor.Controls.Models、FlowEditor.Core.Models。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using FlowEditor.Controls.Nodes;
using FlowEditor.Core.Nodes;
using FlowEditor.Controls.Models;
using FlowEditor.Core.Models;

namespace FlowEditor.Controls.Nodes.FlowControl;

/// <summary>拥有一个或多个可编辑内部流程的节点。</summary>
public interface IInternalFlowContainerNode
{
    /// <summary>可序列化的内部流程文档集合。</summary>
    List<FlowChartDocumentModel>? InternalData { get; set; }

    /// <summary>内部流程编辑窗口标题。</summary>
    string InternalEditorTitle { get; }

    /// <summary>内部编辑器工具箱需要排除的工具名称。</summary>
    IReadOnlyCollection<string> ExcludedInternalTools { get; }

    /// <summary>允许保留的最少内部流程数量。</summary>
    int MinimumInternalFlows { get; }

    /// <summary>允许创建的最多内部流程数量。</summary>
    int MaximumInternalFlows { get; }

    /// <summary>新建内部流程的默认名称前缀。</summary>
    string InternalFlowNamePrefix { get; }
}
