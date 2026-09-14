using System.Windows;
using FlowEditor.Interfaces;
using Microsoft.Win32;

namespace FlowEditor.Services;

/// <summary>
/// 对话框服务实现。
/// 基于 WPF 标准 MessageBox 与文件对话框，统一封装消息提示与文件选择，
/// 供 ViewModel 层调用，避免业务层直接耦合 WPF UI。
/// </summary>
public class DialogService : IDialogService
{
    /// <summary>获取当前主窗口作为对话框所有者（可能为 null，在单元测试中）。</summary>
    private static Window? OwnerWindow
    {
        get
        {
            try { return Application.Current?.MainWindow; }
            catch { return null; }
        }
    }

    /// <inheritdoc />
    public string? ShowOpenFileDialog(string filter, string? defaultFileName = null)
    {
        var dlg = new OpenFileDialog { Filter = filter };
        if (!string.IsNullOrEmpty(defaultFileName))
            dlg.FileName = defaultFileName;
        return dlg.ShowDialog(OwnerWindow) == true ? dlg.FileName : null;
    }

    /// <inheritdoc />
    public string? ShowSaveFileDialog(string filter, string defaultFileName, string defaultExt)
    {
        var dlg = new SaveFileDialog
        {
            Filter = filter,
            FileName = defaultFileName,
            DefaultExt = defaultExt
        };
        return dlg.ShowDialog(OwnerWindow) == true ? dlg.FileName : null;
    }

    /// <summary>以当前主窗口为所有者显示指定样式的消息框。</summary>
    private static void ShowMessage(
        string message,
        string title,
        MessageBoxButton buttons,
        MessageBoxImage icon)
    {
        MessageBox.Show(OwnerWindow, message, title, buttons, icon);
    }

    /// <inheritdoc />
    public bool? ShowConfirm(string message, string title)
    {
        return MessageBox.Show(OwnerWindow, message, title,
            MessageBoxButton.YesNoCancel, MessageBoxImage.Question) switch
        {
            MessageBoxResult.Yes => true,
            MessageBoxResult.No => false,
            _ => null
        };
    }

    /// <inheritdoc />
    public bool ConfirmYesNo(string message, string title)
    {
        return MessageBox.Show(OwnerWindow, message, title,
            MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    }

    /// <inheritdoc />
    public void ShowInfo(string message, string title = "提示")
    {
        ShowMessage(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    /// <inheritdoc />
    public void ShowError(string message, string title = "错误")
    {
        ShowMessage(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
