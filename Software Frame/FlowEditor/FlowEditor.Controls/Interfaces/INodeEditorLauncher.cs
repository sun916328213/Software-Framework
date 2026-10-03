// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：INodeEditorLauncher、INodeEditorHandler、INodeNotificationSink。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、FlowEditor.Controls.Nodes、FlowEditor.Core.Nodes。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using FlowEditor.Controls.Nodes;
using FlowEditor.Core.Nodes;

namespace FlowEditor.Controls.Interfaces;

/// <summary>节点请求打开专用编辑器的 seam。</summary>
public interface INodeEditorLauncher
{
    /// <summary>为指定节点打开宿主提供的编辑器。</summary>
    /// <param name="node">需要编辑的节点。</param>
    void Open(NodeModel node);
}

/// <summary>扩展节点程序集提供的专用编辑 adapter；未识别的节点交给原内建编辑入口。</summary>
public interface INodeEditorHandler
{
    /// <summary>识别并打开节点编辑器；不支持该节点时返回 false。</summary>
    bool TryOpen(NodeModel node);
}

/// <summary>节点向宿主发布只读信息提示的 seam。</summary>
public interface INodeNotificationSink
{
    /// <summary>显示一条不需要用户决策的信息。</summary>
    /// <param name="title">提示标题。</param>
    /// <param name="message">提示内容。</param>
    void ShowInformation(string title, string message);
}
