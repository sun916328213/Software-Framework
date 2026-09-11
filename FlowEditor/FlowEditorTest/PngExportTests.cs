using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FlowEditor.Services;
using FlowEditor.Core;
using FlowEditor.ViewModels;
using Xunit;

namespace FlowEditorTest;

public class PngExportTests
{
    [Fact]
    public void CanvasExportBounds_AlwaysUseWholeLogicalCanvas()
    {
        var service = TestFlowEditorFactory.CreateService();
        service.Model.CanvasWidth = 4200;
        service.Model.CanvasHeight = 3100;
        var vm = TestFlowEditorFactory.CreateViewModel(service);

        Assert.Equal(new Rect(0, 0, 4200, 3100), vm.GetCanvasExportBounds());
    }

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
