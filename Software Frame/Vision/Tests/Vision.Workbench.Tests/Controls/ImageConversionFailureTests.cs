using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Vision.Controls.View;
using Vision.Core.Imaging;
using Vision.Core.Results;
using PixelFormat = Vision.Core.Imaging.PixelFormat;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
public sealed class ImageConversionFailureTests(WorkbenchAppFixture host)
{
    [Fact]
    public void ConversionFailure_ClearsOldInputAndOutput_IsVisibleAfterReload_AndRecoversOnNewInput()
    {
        host.Invoke(() =>
        {
            var image = VisionImage.FromBuffer(2, 2, PixelFormat.Mono8, [255, 255, 255, 255]);
            var borrowedResult = new CropResult { OutputImage = image };
            var view = new VisionImageViewport();
            var source = new { Image = image, Result = borrowedResult };
            var inputBinding = new Binding("Image") { Source = source }; var resultBinding = new Binding("Result") { Source = source };
            view.SetBinding(VisionImageViewport.ImageProperty, inputBinding); view.SetBinding(VisionImageViewport.ResultProperty, resultBinding);
            var window = new Window { Content = view, Width = 380, Height = 180, Left = -10000, ShowInTaskbar = false };
            try
            {
                window.Show();
                view.ReportImageError("非完整域不能转换为不含域掩膜的公共图像");
                Assert.Null(view.Image); Assert.Null(view.Result); Assert.Contains("非完整域", view.Status);
                Assert.Same(inputBinding, BindingOperations.GetBinding(view, VisionImageViewport.ImageProperty));
                Assert.Same(resultBinding, BindingOperations.GetBinding(view, VisionImageViewport.ResultProperty));
                window.Content = null; window.Content = view;
                view.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                var errorPixels = Render(view); SaveError(errorPixels);
                Assert.Contains("非完整域", view.Status); Assert.All(image.Pixels, b => Assert.Equal(255, b));
                Assert.Same(image, borrowedResult.OutputImage);
                view.ReportImageError(null);
                Assert.False(errorPixels.SequenceEqual(Render(view))); // 真实错误文字不同于默认占位，不只检查Status字符串。
                view.SetCurrentValue(VisionImageViewport.ImageProperty, image); Assert.DoesNotContain("非完整域", view.Status);
                Assert.Same(image, view.Image);
                view.ReportImageError("第二次失败"); view.ReportImageError(null);
                Assert.Null(view.Image); Assert.DoesNotContain("失败", view.Status);
            }
            finally { window.Close(); }
        });
    }
    private static void SaveError(byte[] pixels)
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "domain-acceptance"); Directory.CreateDirectory(folder);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(380, 180, 96, 96, PixelFormats.Pbgra32, null, pixels, 380 * 4)));
        using var file = File.Create(Path.Combine(folder, "conversion-error.png")); encoder.Save(file);
    }
    private static byte[] Render(FrameworkElement view)
    {
        view.UpdateLayout(); view.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        var bitmap = new RenderTargetBitmap(380, 180, 96, 96, PixelFormats.Pbgra32); bitmap.Render(view);
        var pixels = new byte[380 * 180 * 4]; bitmap.CopyPixels(pixels, 380 * 4, 0); return pixels;
    }
}
