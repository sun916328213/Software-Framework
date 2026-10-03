// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：NullNodeInteraction。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、FlowEditor.Controls.Interfaces、FlowEditor.Controls.Nodes、FlowEditor.Core.Nodes。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using FlowEditor.Controls.Interfaces;
using FlowEditor.Controls.Nodes;
using FlowEditor.Core.Nodes;

namespace FlowEditor.Controls.Services;

/// <summary>
/// 无头宿主的节点交互 adapter。它保留节点动作的可调用性，但不创建 UI，
/// 适用于控制台、后台运行实例和不验证窗口的测试。
/// </summary>
internal sealed class NullNodeInteraction : INodeEditorLauncher, INodeNotificationSink
{
    /// <summary>共享的无状态实例。</summary>
    public static NullNodeInteraction Instance { get; } = new();

    /// <summary>防止外部创建多余实例。</summary>
    private NullNodeInteraction()
    {
    }

    /// <inheritdoc />
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 Open。
    /// 正文中的关键协作调用包括 ThrowIfNull；具体条件和先后次序见方法体。
    /// </remarks>
    public void Open(NodeModel node)
    {
        ArgumentNullException.ThrowIfNull(node);
    }

    /// <inheritdoc />
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 ShowInformation。
    /// </remarks>
    public void ShowInformation(string title, string message)
    {
    }
}
