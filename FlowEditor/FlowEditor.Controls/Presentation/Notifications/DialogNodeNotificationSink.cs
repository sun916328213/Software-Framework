using FlowEditor.Interfaces;

namespace FlowEditor.Presentation.Notifications;

/// <summary>通过宿主对话框模块显示节点信息的 adapter。</summary>
public sealed class DialogNodeNotificationSink : INodeNotificationSink
{
    private readonly IDialogService _dialogs;

    /// <summary>创建节点信息提示 adapter。</summary>
    /// <param name="dialogs">宿主对话框模块。</param>
    public DialogNodeNotificationSink(IDialogService dialogs)
    {
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
    }

    /// <inheritdoc />
    public void ShowInformation(string title, string message)
        => _dialogs.ShowInfo(message, title);
}
