// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：ExportService。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、System.IO、System.Text、System.Windows、System.Windows.Controls、System.Windows.Media。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FlowEditor.Controls.Interfaces;
using Microsoft.Win32;

namespace FlowEditor.Controls.Services;

/// <summary>
/// 流程图导出服务。
/// 支持将画布视图导出为 PNG、SVG 高清图片文件。
/// </summary>
public class ExportService : IExportService
{
    /// <summary>按画布内容区域裁剪并以指定倍率导出透明背景 PNG。</summary>
    public void ExportToPng(FrameworkElement element, string filePath, Rect contentBounds, double scale)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (contentBounds.IsEmpty || contentBounds.Width <= 0 || contentBounds.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(contentBounds), "导出区域必须具有有效尺寸。");

        scale = Math.Clamp(scale, 0.5, 4.0);
        element.UpdateLayout();
        var pixelWidth = Math.Max(1, (int)Math.Ceiling(contentBounds.Width * scale));
        var pixelHeight = Math.Max(1, (int)Math.Ceiling(contentBounds.Height * scale));

        // 画布点阵仅用于编辑，不写入 PNG；节点自身背景仍按各自 XAML 样式渲染。
        Brush? originalBackground = null;
        if (element is Panel panel)
        {
            originalBackground = panel.Background;
            panel.Background = Brushes.Transparent;
        }

        try
        {
            var visual = new DrawingVisual();
            using (var drawing = visual.RenderOpen())
            {
                var brush = new VisualBrush(element)
                {
                    Stretch = Stretch.None,
                    AlignmentX = AlignmentX.Left,
                    AlignmentY = AlignmentY.Top,
                    ViewboxUnits = BrushMappingMode.Absolute,
                    Viewbox = contentBounds
                };
                drawing.DrawRectangle(brush, null,
                    new Rect(0, 0, contentBounds.Width, contentBounds.Height));
            }

            var bitmap = new RenderTargetBitmap(
                pixelWidth, pixelHeight, 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(filePath);
            encoder.Save(stream);
        }
        finally
        {
            if (element is Panel restorePanel)
                restorePanel.Background = originalBackground;
        }
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

    /// <inheritdoc />
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 ExportToPngWithDialog。
    /// 正文中的关键协作调用包括 ShowDialog、ExportToPng；具体条件和先后次序见方法体。
    /// </remarks>
    public bool ExportToPngWithDialog(
        FrameworkElement element, Rect contentBounds, double scale = 2.0)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "PNG 图片 (*.png)|*.png",
            FileName = "FlowChart.png",
            DefaultExt = ".png"
        };
        if (dlg.ShowDialog() != true) return false;
        ExportToPng(element, dlg.FileName, contentBounds, scale);
        return true;
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
