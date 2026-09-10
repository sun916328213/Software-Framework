using FlowEditor.Models;

namespace FlowEditor.Nodes.FlowControl;

/// <summary>拥有一个或多个可编辑内部流程的节点。</summary>
public interface IInternalFlowContainerNode
{
    List<FlowChartDocumentModel>? InternalData { get; set; }

    string InternalEditorTitle { get; }

    IReadOnlyCollection<string> ExcludedInternalTools { get; }

    int MinimumInternalFlows { get; }

    int MaximumInternalFlows { get; }

    string InternalFlowNamePrefix { get; }
}
