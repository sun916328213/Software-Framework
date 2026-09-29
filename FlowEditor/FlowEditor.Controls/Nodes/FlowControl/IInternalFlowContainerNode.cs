using FlowEditor.Models;

namespace FlowEditor.Nodes.FlowControl;

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
