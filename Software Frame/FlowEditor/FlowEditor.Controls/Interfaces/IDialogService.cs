// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：IDialogService。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
namespace FlowEditor.Controls.Interfaces;

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

    /// <summary>
    /// 三键确认框（是/否/取消），返回 true=是，false=否，null=取消。
    /// </summary>
    bool? ShowConfirm(string message, string title);

    /// <summary>两键确认框（是/否），返回是否确认。</summary>
    bool ConfirmYesNo(string message, string title);

    /// <summary>显示信息框。</summary>
    void ShowInfo(string message, string title = "提示");

    /// <summary>显示错误框。</summary>
    void ShowError(string message, string title = "错误");
}
