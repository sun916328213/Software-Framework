using System.Windows;
using System.Windows.Media;

namespace FlowEditor.Interfaces;

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
