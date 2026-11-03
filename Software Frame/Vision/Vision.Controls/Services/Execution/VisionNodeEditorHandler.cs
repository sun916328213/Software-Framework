using FlowEditor.Controls.Interfaces;
using FlowEditor.Controls.Nodes;
using Vision.FlowNode;

namespace Vision.Controls.Services;

/// <summary>通用启动器只把请求交回该节点所属宿主，不依赖全局选中节点或创建算法。</summary>
internal sealed class VisionNodeEditorHandler : INodeEditorHandler
{
    public bool TryOpen(NodeModel node)
    {
        if (node is not VisionToolNodeBase vision) return false;
        vision.RequestEditor();
        return true;
    }
}
