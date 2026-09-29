using System.Windows;

namespace ScriptEditor.Dialogs;

/// <summary>
/// 符号重命名对话框（Controls 层 UI 交互）。
/// </summary>
/// <remarks>
/// P4 产物：原来 <c>ScriptEditorViewModel.RenameSymbolAsync</c> 直接引用
/// <c>Microsoft.VisualBasic.Interaction.InputBox</c> 与 <c>Application.Dispatcher</c>，
/// 现收敛到此，ViewModel 只调用服务，不再自行实现 UI 交互。
/// </remarks>
public static class SymbolRenameDialog
{
    /// <summary>弹出重命名输入框，返回新名称（取消/为空返回 null）。</summary>
    public static Task<string?> PromptAsync(Window? owner, string currentName)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
            return Task.FromResult(PromptCore(currentName));

        return dispatcher.InvokeAsync(() => PromptCore(currentName)).Task;
    }

    private static string? PromptCore(string currentName)
    {
        var input = Microsoft.VisualBasic.Interaction.InputBox(
            "请输入新名称:", "重命名符号", currentName, -1, -1);
        return string.IsNullOrEmpty(input) || input == currentName ? null : input;
    }
}
