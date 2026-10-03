// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：IExportService。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、System.Windows、System.Windows.Media。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using System.Windows;
using System.Windows.Media;

namespace FlowEditor.Controls.Interfaces;

/// <summary>
/// 流程图导出服务接口。
/// 为表现层提供带保存路径选择的 PNG、SVG 导出意图。
/// </summary>
public interface IExportService
{
    /// <summary>选择路径后，按内容区域裁剪并高清导出。</summary>
    bool ExportToPngWithDialog(FrameworkElement element, Rect contentBounds, double scale = 2.0);

    /// <summary>弹出保存对话框并导出 SVG。</summary>
    bool ExportToSvgWithDialog(FrameworkElement element);
}
