using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Vision.Controls.Models;
using Vision.Controls.View;
using Vision.Controls.ViewModels;
using Vision.Core.Imaging;
using ImageFormat = Vision.Core.Imaging.PixelFormat;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
public sealed class VisionImagePanelTests(WorkbenchAppFixture host)
{
    [Fact]
    public void DisplayPanel_HasNoToolbar_AndContextMenuUsesTheSameViewport()
    {
        host.Invoke(() =>
        {
            var image = VisionImage.FromBuffer(20, 20, ImageFormat.Mono8, new byte[400]);
            using var session = new VisionToolEditorSession(new VisionToolEditorModel { InputImage = image });
            var panel = new VisionImagePanel { DataContext = session };
            var window = Show(panel);
            try
            {
                Assert.Empty(Children(panel).OfType<VisionImageToolbarView>());
                var viewport = panel.ImageViewport;
                Assert.Same(image, viewport.Image);
                var items = viewport.ContextMenu.Items.OfType<MenuItem>().ToArray();
                Assert.Equal(new[] { "放大", "缩小", "实际像素 1:1", "适应图像", "平移", "选择 / 编辑", "另存为图像…" }, items.Select(item => item.Header));
                Click(items[2]); Assert.Equal(1, Scale(viewport), 8);
                Click(items[0]); Assert.Equal(1.2, Scale(viewport), 8);
                Click(items[1]); Assert.Equal(1, Scale(viewport), 8);
                Click(items[4]); Assert.Equal("平移", viewport.Mode);
                Click(items[5]); Assert.Equal("选择", viewport.Mode);
                Click(items[3]);
                Assert.True((bool)typeof(VisionImageViewport).GetField("_fit", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(viewport)!);
                Assert.Same(image, session.InputImage); Assert.Empty(session.RoiEditor.Rois);
                window.Content = null; Drain(); window.Content = panel; Drain();
                Assert.Same(image, viewport.Image); Assert.True(session.IsSessionActive);
            }
            finally { window.Close(); Drain(); }
        });
    }

    [Theory]
    [InlineData(".png")]
    [InlineData(".tif")]
    public void SaveImage_WritesCurrentOutputPixelsAtOriginalSize_WithoutZoomOrOverlays(string extension)
    {
        host.Invoke(() =>
        {
            var path = Path.Combine(Path.GetTempPath(), $"vision-export-{Guid.NewGuid():N}{extension}");
            var input = VisionImage.FromBuffer(20, 20, ImageFormat.Mono8, new byte[400]);
            byte[] pixels = [0, 0, 255, 255, 0, 128, 52, 18, 1, 0, 254, 255];
            var output = VisionImage.FromBuffer(3, 2, ImageFormat.Mono16, pixels);
            using var result = new CropResult { Success = true, OutputImage = output };
            using var session = new VisionToolEditorSession(new VisionToolEditorModel { InputImage = input });
            var panel = new VisionImagePanel { DataContext = session };
            var window = Show(panel);
            try
            {
                panel.ImageViewport.Result = result; panel.ImageViewport.ShowOutput = true;
                panel.ImageViewport.ActualPixels(); panel.ImageViewport.ZoomIn(); Drain();
                panel.SaveImage(path);
                using var stream = File.OpenRead(path);
                var saved = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
                Assert.Equal(3, saved.PixelWidth); Assert.Equal(2, saved.PixelHeight);
                Assert.Equal(16, saved.Format.BitsPerPixel);
                var actual = new byte[pixels.Length]; saved.CopyPixels(actual, 6, 0);
                Assert.Equal(pixels, actual);
                Assert.Same(input, session.InputImage); Assert.Same(result, panel.ImageViewport.Result);
            }
            finally { window.Close(); Drain(); File.Delete(path); }
        });
    }

    [Fact]
    public void SaveImage_WithoutPixels_DoesNotCreateAFile()
    {
        host.Invoke(() =>
        {
            var panel = new VisionImagePanel();
            var path = Path.Combine(Path.GetTempPath(), $"vision-empty-{Guid.NewGuid():N}.png");
            Assert.Throws<InvalidOperationException>(() => panel.SaveImage(path));
            Assert.False(File.Exists(path));
        });
    }
    private static double Scale(VisionImageViewport viewport) => (double)typeof(VisionImageViewport)
        .GetField("_scale", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(viewport)!;
    private static void Click(MenuItem item) { item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Drain(); }
    private static Window Show(FrameworkElement content)
    {
        var window = new Window { Content = content, Width = 600, Height = 420, Left = -10000, Top = -10000, ShowInTaskbar = false };
        window.Show(); Drain(); return window;
    }
    private static void Drain() => Application.Current.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    private static IEnumerable<DependencyObject> Children(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var nested in Children(child)) yield return nested;
        }
    }
}
