// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：PngExportTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.IO、System.Windows、System.Windows.Controls、System.Windows.Media、System.Windows.Media.Imaging、FlowEditor.Controls.Services。导入不代表所有类型都会在运行时加载。
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FlowEditor.Controls.Services;
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;
using FlowEditor.Controls.ViewModels;
using Xunit;

namespace FlowEditor.Workbench.Tests;

/// <summary>验证逻辑画布范围和 PNG 位图编码的裁剪、缩放及透明通道。</summary>
public class PngExportTests
{
    /// <summary>确保导出范围始终使用完整逻辑画布而不是当前滚动视口。</summary>
    [Fact]
    public void CanvasExportBounds_AlwaysUseWholeLogicalCanvas()
    {
        var service = TestFlowEditorFactory.CreateService();
        service.Model.CanvasWidth = 4200;
        service.Model.CanvasHeight = 3100;
        var vm = TestFlowEditorFactory.CreateViewModel(service);

        Assert.Equal(new Rect(0, 0, 4200, 3100), vm.GetCanvasExportBounds());
    }

    /// <summary>确保远端内容按指定边界裁剪，并生成具有透明通道的 2 倍分辨率 PNG。</summary>
    [Fact]
    public void ExportToPng_ContentBoundsAndScale_ProduceCroppedHighResolutionImage()
    {
        var file = Path.Combine(Path.GetTempPath(), $"flow-export-{Guid.NewGuid():N}.png");
        Exception? failure = null;
        var pixelWidth = 0;
        var pixelHeight = 0;
        var hasOpaquePixel = false;
        var thread = new Thread(() =>
        {
            try
            {
                var canvas = new Canvas { Width = 4000, Height = 3000, Background = Brushes.Transparent };
                var marker = new System.Windows.Shapes.Rectangle { Width = 12, Height = 12, Fill = Brushes.Red };
                Canvas.SetLeft(marker, 3520);
                Canvas.SetTop(marker, 2520);
                canvas.Children.Add(marker);
                canvas.Measure(new Size(4000, 3000));
                canvas.Arrange(new Rect(0, 0, 4000, 3000));
                new ExportService().ExportToPng(canvas, file, new Rect(3500, 2500, 50, 40), 2.0);
                using var stream = File.OpenRead(file);
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
                pixelWidth = image.PixelWidth;
                pixelHeight = image.PixelHeight;
                var converted = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
                var pixels = new byte[converted.PixelWidth * converted.PixelHeight * 4];
                converted.CopyPixels(pixels, converted.PixelWidth * 4, 0);
                hasOpaquePixel = pixels.Where((_, index) => index % 4 == 3).Any(alpha => alpha > 0);
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        try
        {
            Assert.Null(failure);
            Assert.Equal(100, pixelWidth);
            Assert.Equal(80, pixelHeight);
            Assert.True(hasOpaquePixel, "远端节点内容应完整出现在裁剪后的 PNG 中");
        }
        finally
        {
            if (File.Exists(file)) File.Delete(file);
        }
    }
}
