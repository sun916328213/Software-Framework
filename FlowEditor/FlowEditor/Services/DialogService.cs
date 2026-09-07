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

    public string? ShowOpenFileDialog(string filter, string? defaultFileName = null)
    {
        var dlg = new OpenFileDialog { Filter = filter };
        if (!string.IsNullOrEmpty(defaultFileName))
            dlg.FileName = defaultFileName;
        return dlg.ShowDialog(OwnerWindow) == true ? dlg.FileName : null;
    }

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

    public void ShowMessage(string message, string title, MessageBoxButton buttons, MessageBoxImage icon)
    {
        MessageBox.Show(OwnerWindow, message, title, buttons, icon);
    }

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

    public bool ConfirmYesNo(string message, string title)
    {
        return MessageBox.Show(OwnerWindow, message, title,
            MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    }

    public void ShowInfo(string message, string title = "提示")
    {
        ShowMessage(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public void ShowWarning(string message, string title = "警告")
    {
        ShowMessage(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    public void ShowError(string message, string title = "错误")
    {
        ShowMessage(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
