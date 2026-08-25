using System.Windows;
using System.Windows.Media;

namespace FlowEditor.Interfaces;

/// <summary>
/// 流程图导出服务接口。
/// 支持将画布视图导出为 PNG、SVG 高清图片文件。
/// </summary>
public interface IExportService
{
    /// <summary>将 FrameworkElement 导出为 PNG 图片。</summary>
    void ExportToPng(FrameworkElement element, string filePath);

    /// <summary>将 FrameworkElement 导出为 SVG 文件。</summary>
    void ExportToSvg(FrameworkElement element, string filePath);

    /// <summary>弹出保存对话框并导出 PNG。</summary>
    bool ExportToPngWithDialog(FrameworkElement element);

    /// <summary>弹出保存对话框并导出 SVG。</summary>
    bool ExportToSvgWithDialog(FrameworkElement element);
}
