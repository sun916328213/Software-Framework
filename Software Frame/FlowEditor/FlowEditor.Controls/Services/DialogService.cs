// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：DialogService。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、System.Windows、FlowEditor.Controls.Interfaces、Microsoft.Win32。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using System.Windows;
using FlowEditor.Controls.Interfaces;
using Microsoft.Win32;

namespace FlowEditor.Controls.Services;

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
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 ShowOpenFileDialog。
    /// 正文中的关键协作调用包括 IsNullOrEmpty、ShowDialog；具体条件和先后次序见方法体。
    /// </remarks>
    public string? ShowOpenFileDialog(string filter, string? defaultFileName = null)
    {
        var dlg = new OpenFileDialog { Filter = filter };
        if (!string.IsNullOrEmpty(defaultFileName))
            dlg.FileName = defaultFileName;
        return dlg.ShowDialog(OwnerWindow) == true ? dlg.FileName : null;
    }

    /// <inheritdoc />
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 ShowSaveFileDialog。
    /// 正文中的关键协作调用包括 ShowDialog；具体条件和先后次序见方法体。
    /// </remarks>
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
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 ShowConfirm。
    /// 正文中的关键协作调用包括 Show；具体条件和先后次序见方法体。
    /// </remarks>
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
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 ConfirmYesNo。
    /// 正文中的关键协作调用包括 Show；具体条件和先后次序见方法体。
    /// </remarks>
    public bool ConfirmYesNo(string message, string title)
    {
        return MessageBox.Show(OwnerWindow, message, title,
            MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    }

    /// <inheritdoc />
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 ShowInfo。
    /// 正文中的关键协作调用包括 ShowMessage；具体条件和先后次序见方法体。
    /// </remarks>
    public void ShowInfo(string message, string title = "提示")
    {
        ShowMessage(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    /// <inheritdoc />
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 ShowError。
    /// 正文中的关键协作调用包括 ShowMessage；具体条件和先后次序见方法体。
    /// </remarks>
    public void ShowError(string message, string title = "错误")
    {
        ShowMessage(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
