using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FlowEditor.Interfaces;
using Microsoft.Win32;

namespace FlowEditor.Services;

/// <summary>
/// 流程图导出服务。
/// 支持将画布视图导出为 PNG、SVG 高清图片文件。
/// </summary>
public class ExportService : IExportService
{
    /// <summary>
    /// 将 FrameworkElement 导出为 PNG 图片。
    /// </summary>
    public void ExportToPng(FrameworkElement element, string filePath)
    {
        // 确保元素已渲染
        element.Measure(new Size(element.ActualWidth, element.ActualHeight));
        element.Arrange(new Rect(new Size(element.ActualWidth, element.ActualHeight)));
        element.UpdateLayout();

        var renderBitmap = new RenderTargetBitmap(
            (int)element.ActualWidth,
            (int)element.ActualHeight,
            96, 96,
            PixelFormats.Pbgra32);
        renderBitmap.Render(element);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(renderBitmap));

        using var stream = File.Create(filePath);
        encoder.Save(stream);
    }

    /// <summary>
    /// 将 FrameworkElement 导出为 SVG 文件（简化版，使用矩形占位 + 尺寸元信息）。
    /// 完整节点/连线矢量导出建议接入专业库。
    /// </summary>
    public void ExportToSvg(FrameworkElement element, string filePath)
    {
        var width = element.ActualWidth;
        var height = element.ActualHeight;

        var svgContent = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<svg xmlns=""http://www.w3.org/2000/svg"" width=""{width}"" height=""{height}"" viewBox=""0 0 {width} {height}"">
  <rect width=""100%"" height=""100%"" fill=""#ffffff"" />
  <!-- SVG 导出为简化版，完整 SVG 请使用专业库 -->
</svg>";

        File.WriteAllText(filePath, svgContent, Encoding.UTF8);
    }

    /// <summary>
    /// 弹出保存对话框并导出 PNG。
    /// </summary>
    public bool ExportToPngWithDialog(FrameworkElement element)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "PNG 图片 (*.png)|*.png",
            FileName = "FlowChart.png",
            DefaultExt = ".png"
        };
        if (dlg.ShowDialog() == true)
        {
            ExportToPng(element, dlg.FileName);
            return true;
        }
        return false;
    }

    /// <summary>
    /// 弹出保存对话框并导出 SVG。
    /// </summary>
    public bool ExportToSvgWithDialog(FrameworkElement element)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "SVG 矢量图 (*.svg)|*.svg",
            FileName = "FlowChart.svg",
            DefaultExt = ".svg"
        };
        if (dlg.ShowDialog() == true)
        {
            ExportToSvg(element, dlg.FileName);
            return true;
        }
        return false;
    }
}
