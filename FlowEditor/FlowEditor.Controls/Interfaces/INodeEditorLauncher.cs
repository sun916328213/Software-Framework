using FlowEditor.Nodes;

namespace FlowEditor.Interfaces;

/// <summary>节点请求打开专用编辑器的 seam。</summary>
public interface INodeEditorLauncher
{
    /// <summary>为指定节点打开宿主提供的编辑器。</summary>
    /// <param name="node">需要编辑的节点。</param>
    void Open(NodeModel node);
}

/// <summary>节点向宿主发布只读信息提示的 seam。</summary>
public interface INodeNotificationSink
{
    /// <summary>显示一条不需要用户决策的信息。</summary>
    /// <param name="title">提示标题。</param>
    /// <param name="message">提示内容。</param>
    void ShowInformation(string title, string message);
}
