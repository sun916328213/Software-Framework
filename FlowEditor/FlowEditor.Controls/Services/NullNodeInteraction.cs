using FlowEditor.Interfaces;
using FlowEditor.Nodes;

namespace FlowEditor.Services;

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
    public void Open(NodeModel node)
    {
        ArgumentNullException.ThrowIfNull(node);
    }

    /// <inheritdoc />
    public void ShowInformation(string title, string message)
    {
    }
}
