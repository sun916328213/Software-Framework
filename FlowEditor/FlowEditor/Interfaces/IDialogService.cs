using System.Windows;

namespace FlowEditor.Interfaces;

/// <summary>
/// 对话框服务接口。
/// 抽象消息框与文件对话框，使 ViewModel/Service 层不直接依赖 WPF 对话框控件，
/// 便于单元测试与 UI 层解耦。
/// </summary>
public interface IDialogService
{
    /// <summary>打开文件对话框，返回选中的文件路径（取消返回 null）。</summary>
    string? ShowOpenFileDialog(string filter, string? defaultFileName = null);

    /// <summary>保存文件对话框，返回目标文件路径（取消返回 null）。</summary>
    string? ShowSaveFileDialog(string filter, string defaultFileName, string defaultExt);

    /// <summary>显示消息框。</summary>
    void ShowMessage(string message, string title, MessageBoxButton buttons, MessageBoxImage icon);

    /// <summary>
    /// 三键确认框（是/否/取消），返回 true=是，false=否，null=取消。
    /// </summary>
    bool? ShowConfirm(string message, string title);

    /// <summary>两键确认框（是/否），返回是否确认。</summary>
    bool ConfirmYesNo(string message, string title);

    /// <summary>显示信息框。</summary>
    void ShowInfo(string message, string title = "提示");

    /// <summary>显示警告框。</summary>
    void ShowWarning(string message, string title = "警告");

    /// <summary>显示错误框。</summary>
    void ShowError(string message, string title = "错误");
}
