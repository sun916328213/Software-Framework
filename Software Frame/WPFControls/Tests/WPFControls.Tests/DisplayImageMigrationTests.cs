using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Vision.Controls.View;
using Vision.Core.Imaging;
using WPFControls.ProgramControl.Views.Display;
using Xunit;
using ImageFormat = Vision.Core.Imaging.PixelFormat;

namespace WPFControls.Tests;

[CollectionDefinition("Display migration", DisableParallelization = true)]
public sealed class DisplayMigrationCollection { }

[Collection("Display migration")]
public sealed class DisplayImageMigrationTests
{
    [Fact]
    public void DisplayPages_UseSharedViewer_AndCaptureTheViewportWithoutNativeWindows() => OnSta(() =>
    {
        // 只加载宿主实际使用的资源，不启动Prism、设备或生产脚本。Application只创建一次。
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        try
        {
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/WPFControls;component/Shared/Resources/Resources.xaml", UriKind.Relative) });
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/WPFCustomControls;component/Resources/Resources.xaml", UriKind.Relative) });
            foreach (var kind in new[] { "single", "dp", "group" }) CheckDisplayPage(kind);
            CheckViewportCaptureAndClear();
        }
        finally { app.Shutdown(); }
    });

    private static void CheckDisplayPage(string kind)
    {
        var (page, display) = Create(kind);
        // 托管显示页不再声明不可用的HALCON窗口句柄，也不注册同名依赖属性。
        Assert.Null(page.GetType().GetProperty("Handle"));
        Assert.Null(page.GetType().GetField("HandleProperty"));
        Assert.Null(System.ComponentModel.DependencyPropertyDescriptor.FromName("Handle", page.GetType(), page.GetType()));
        var caller = new object();
        page.DataContext = caller;
        var image = VisionImage.FromBuffer(3, 2, ImageFormat.Rgb24,
            [255, 0, 0, 0, 255, 0, 0, 0, 255, 255, 255, 255, 0, 0, 0, 128, 128, 128]);
        display.ImageViewport.Image = image;
        var window = new Window { Content = page, Width = 360, Height = 160, Left = -10000, Top = -10000, ShowInTaskbar = false };
        try
        {
            window.Show(); Drain();
            Assert.Same(caller, page.DataContext);
            Assert.Null(display.DataContext);
            Assert.Same(image, display.ImageViewport.Image);
            Assert.False(display.ImageViewport.IsRoiEditingEnabled);
            Assert.InRange(display.ImageViewport.ActualHeight, 1, page.ActualHeight);
            Assert.Empty(Children(page).OfType<VisionImageToolbarView>());
            Assert.Empty(Children(page).OfType<VisionToolEditorView>());
            Assert.DoesNotContain(Children(page), child => child.GetType().Name.Contains("HSmartWindow", StringComparison.Ordinal));
            var items = display.ImageViewport.ContextMenu.Items.OfType<MenuItem>().ToArray();
            Assert.Contains(items, item => Equals(item.Header, "另存为图像…"));
            Assert.Single(items, item => Equals(item.Header, "平移")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Assert.Equal("平移", display.ImageViewport.Mode);
            window.Content = null; Drain(); window.Content = page; Drain();
            Assert.Same(image, display.ImageViewport.Image);
        }
        finally { window.Close(); Drain(); }
    }

    private static void CheckViewportCaptureAndClear()
    {
        var page = new DisplaySingleView_DP();
        Assert.Null(page.CaptureViewport());
        Assert.Null(page.GetDisplayImage()); // 未布局时不得调用HALCON。
        var binding = new Binding("Image") { Source = new { Image = (object?)null } };
        page.SetBinding(DisplaySingleView_DP.DisplayImageProperty, binding);
        var image = VisionImage.FromBuffer(2, 2, ImageFormat.Mono8, [255, 255, 255, 255]);
        page.ImageDisplay.ImageViewport.Image = image;
        var window = new Window { Content = page, Width = 360, Height = 240, Left = -10000, Top = -10000, ShowInTaskbar = false };
        try
        {
            window.Show(); Drain();
            var viewport = page.ImageDisplay.ImageViewport;
            viewport.ActualPixels(); Drain();
            var captured = Assert.IsAssignableFrom<BitmapSource>(page.CaptureViewport());
            var dpi = VisualTreeHelper.GetDpi(viewport);
            Assert.Equal((int)Math.Ceiling(viewport.ActualWidth * dpi.DpiScaleX), captured.PixelWidth);
            Assert.Equal((int)Math.Ceiling(viewport.ActualHeight * dpi.DpiScaleY), captured.PixelHeight);
            Assert.True(captured.PixelWidth > image.Width);
            Assert.True(captured.IsFrozen);
            // 1:1的2×2白图之外保留画布背景，不是将输入拉伸成截图。
            var pixels = new byte[captured.PixelWidth * captured.PixelHeight * 3];
            captured.CopyPixels(pixels, captured.PixelWidth * 3, 0);
            Assert.InRange(pixels.Count(value => value > 240), 1, 100);
            viewport.ReportImageError("公共图像域转换失败");
            Assert.Contains("域转换失败", viewport.Status);
            page.Clear(); Drain();
            Assert.DoesNotContain("失败", viewport.Status);
            Assert.Null(viewport.Image);
            Assert.Same(binding, BindingOperations.GetBinding(page, DisplaySingleView_DP.DisplayImageProperty));
            Assert.All(image.Pixels, value => Assert.Equal(255, value));
            captured = page.CaptureViewport();
            captured.CopyPixels(pixels, captured.PixelWidth * 3, 0);
            Assert.DoesNotContain(pixels, value => value > 240);
        }
        finally { window.Close(); Drain(); }
    }

    private static (UserControl Page, VisionImagePanel Display) Create(string kind)
    {
        if (kind == "single") { var page = new DisplaySingleView(); return (page, page.ImageDisplay); }
        if (kind == "group") { var page = new DisplaySingleGroupView(); return (page, page.ImageDisplay); }
        var dp = new DisplaySingleView_DP(); return (dp, dp.ImageDisplay);
    }
    private static IEnumerable<DependencyObject> Children(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var nested in Children(child)) yield return nested;
        }
    }
    private static void Drain() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    private static void OnSta(Action action)
    {
        ExceptionDispatchInfo? fault = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception error) { fault = ExceptionDispatchInfo.Capture(error); }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "Display migration STA test timed out.");
        fault?.Throw();
    }
}
