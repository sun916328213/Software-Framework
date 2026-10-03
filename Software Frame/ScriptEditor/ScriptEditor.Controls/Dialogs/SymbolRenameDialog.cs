// 【中文阅读说明】
// ScriptEditor 模块：脚本持久化模型、运行时编译执行和编辑器协作；脚本 ID、引用及断点属于需要保留的状态。
// 本文件定义：SymbolRenameDialog。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：ScriptEditor.Controls、System.Windows。导入不代表所有类型都会在运行时加载。
using ScriptEditor.Controls;
using System.Windows;

namespace ScriptEditor.Controls.Dialogs;

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

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 PromptCore。
    /// 正文中的关键协作调用包括 InputBox、IsNullOrEmpty；具体条件和先后次序见方法体。
    /// </summary>
    private static string? PromptCore(string currentName)
    {
        var input = Microsoft.VisualBasic.Interaction.InputBox(
            "请输入新名称:", "重命名符号", currentName, -1, -1);
        return string.IsNullOrEmpty(input) || input == currentName ? null : input;
    }
}
