using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Vision.Controls.Services;
using Vision.Controls.View;
using Vision.Controls.ViewModels;
using Vision.Core.Imaging;
using Vision.Core.Results;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
[Trait("Category", "Regression")]
public sealed class BrowserVisualStyleTests(WorkbenchAppFixture host)
{
    [Fact]
    public void CompactToolbar_HasSaveAndSaveAsAndRun_WithoutDuplicateMenusOrDemo()
    {
        host.Invoke(() => WithPage((page, browser) =>
        {
            var toolbar = Assert.IsType<System.Windows.Controls.Grid>(page.FindName("TopMenuBar"));
            Assert.Null(toolbar.FindName("DesktopMenu"));
            Assert.Empty(Descendants(toolbar).OfType<Menu>());
            var buttons = Descendants(toolbar).OfType<Button>().Where(button => button.Command != null).ToArray();
            Assert.Equal(5, buttons.Length);
            Assert.All(buttons, button => Assert.Equal(30, button.ActualHeight));
            Assert.All(buttons.Take(4), button => Assert.Equal(new Thickness(0), button.BorderThickness));
            Assert.Same(browser.ImportProjectCommand, buttons[0].Command);
            Assert.Same(browser.SaveProjectCommand, buttons[1].Command);
            Assert.Same(browser.SaveProjectAsCommand, buttons[2].Command);
            Assert.Same(browser.CurrentCard!.RunCommand, buttons[4].Command);
            browser.LoadImage(); Drain();
            var pixels = Assert.Single(Descendants(page.ImageToolbar).OfType<Button>(), button => System.Windows.Automation.AutomationProperties.GetName(button) == "1:1");
            pixels.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Drain();
            var viewport = page.ImagePanel.ImageViewport;
            Assert.Equal(1.0, (double)typeof(VisionImageViewport).GetField("_scale", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(viewport)!);
            var fit = Assert.Single(Descendants(page.ImageToolbar).OfType<Button>(), button => System.Windows.Automation.AutomationProperties.GetName(button) == "适应窗口");
            fit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Drain();
            Assert.True((bool)typeof(VisionImageViewport).GetField("_fit", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(viewport)!);
        }));
    }

    [Fact]
    public void RegionHeadersAndSelectionTabs_AreCompact_AndRenderAnOfflinePreview()
    {
        host.Invoke(() => WithPage((page, browser) =>
        {
            browser.LoadImage(); browser.RoiEditor.AddRectangle(250, 200, 180, 100);
            ((TabItem)page.DetailsPanel.FindName("RoiTab")).IsSelected = true; Drain();
            var roiView = Assert.Single(Descendants(page).OfType<RoiEditorView>());
            Assert.Equal(FontWeights.Normal, Assert.IsType<TextBox>(roiView.FindName("RoiX")).FontWeight);
            var selector = Assert.IsType<ListBox>(page.ImageToolbar.FindName("ImageViewSelector"));
            Assert.All(selector.Items.Cast<object>(), item =>
            {
                var container = Assert.IsType<ListBoxItem>(selector.ItemContainerGenerator.ContainerFromItem(item));
                if (Equals(container.Tag, Vision.Controls.Models.ImageViewKind.Training))
                    Assert.Equal(browser.SupportsTemplateInput ? Visibility.Visible : Visibility.Collapsed, container.Visibility);
                if (container.IsVisible) Assert.InRange(container.ActualHeight, 28, 36);
            });
            var tabs = Assert.IsType<TabControl>(page.DetailsPanel.FindName("DetailsTabs"));
            Assert.All(tabs.Items.OfType<TabItem>().Where(tab => tab.IsVisible), tab => Assert.InRange(tab.ActualHeight, 28, 36));
            var bitmap = new RenderTargetBitmap((int)page.ActualWidth, (int)page.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(page);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(Path.GetTempPath(), "vision-editor-desktop-preview.png"));
            encoder.Save(stream);
        }));
    }

    [Theory]
    [InlineData(900)]
    [InlineData(1300)]
    public void OfflineThemeAndSimulatedDpi_KeepToolbarAndContextMenuVisible_AndSaveOriginalPixels(int width)
    {
        host.Invoke(() => WithPage((page, browser) =>
        {
            var window = Window.GetWindow(page)!;
            var originalDpi = VisualTreeHelper.GetDpi(window);
            var theme = Assert.IsType<global::Vision.Workbench.ViewModels.MainWindowViewModel>(Application.Current.MainWindow.DataContext).Theme;
            var originalTheme = (theme.IsDark, theme.Primary, theme.Secondary);
            var directory = Path.Combine(Path.GetTempPath(), "vision-offline-ui-acceptance");
            Directory.CreateDirectory(directory);
            browser.LoadImage(); Drain();
            var image = browser.InputImage!;
            var viewport = page.ImagePanel.ImageViewport;
            var menu = viewport.ContextMenu;
            try
            {
                foreach (var scale in new[] { 1.0, 1.25, 1.5, 2.0 })
                {
                    Color? lightBackground = null;
                    foreach (var dark in new[] { false, true })
                    {
                        // 只更新本测试Window的根视觉DPI，不改系统显示设置或其他窗口。
                        VisualTreeHelper.SetRootDpi(window, new DpiScale(scale, scale));
                        window.Width = width;
                        theme.Initialize(dark, originalTheme.Primary, originalTheme.Secondary); // 不持久化主题配置。
                        page.UpdateLayout(); Drain();
                        Assert.Null(theme.LastError);
                        Assert.Equal(scale, VisualTreeHelper.GetDpi(viewport).DpiScaleX, 6);
                        var background = Assert.IsType<SolidColorBrush>(page.Background).Color;
                        if (!dark) lightBackground = background;
                        else Assert.NotEqual(lightBackground, background);
                        var foreground = Assert.IsType<SolidColorBrush>(page.Foreground).Color;
                        Assert.True(dark ? Brightness(foreground) > Brightness(background) : Brightness(foreground) < Brightness(background));

                        var header = Assert.IsType<Border>(page.ImageToolbar.FindName("ImageViewsBar"));
                        var selector = Assert.IsType<ListBox>(page.ImageToolbar.FindName("ImageViewSelector"));
                        foreach (var item in selector.Items.OfType<ListBoxItem>())
                        {
                            if (Equals(item.Tag, Vision.Controls.Models.ImageViewKind.Training))
                                Assert.Equal(browser.SupportsTemplateInput ? Visibility.Visible : Visibility.Collapsed, item.Visibility);
                            if (!item.IsVisible) continue;
                            Assert.InRange(item.ActualHeight, 28, 36);
                            Assert.True(new Rect(header.RenderSize).Contains(item.TransformToAncestor(header).TransformBounds(new Rect(item.RenderSize))));
                        }
                        var tools = Assert.IsType<StackPanel>(page.ImageToolbar.FindName("CanvasTools"));
                        var overlay = Assert.Single(Descendants(tools).OfType<Menu>());
                        var overlayItem = Assert.IsType<MenuItem>(overlay.Items[0]);
                        var icon = Assert.IsType<MaterialDesignThemes.Wpf.PackIcon>(overlayItem.Header);
                        Assert.True(new Rect(overlay.RenderSize).Contains(icon.TransformToAncestor(overlay).TransformBounds(new Rect(icon.RenderSize))));
                        Assert.True(header.TranslatePoint(new Point(0, header.ActualHeight), page).Y <= viewport.TranslatePoint(new Point(), page).Y + 1);
                        var tag = $"{width}-{(dark ? "dark" : "light")}-{(int)(scale * 100)}";
                        SavePreview(page, scale, Path.Combine(directory, tag + ".png"));

                        // 打开真实Popup并检查可见项；系统物理右击和保存对话框不由此冒充验收。
                        menu.PlacementTarget = viewport;
                        menu.IsOpen = true; Drain();
                        try
                        {
                            var popupRoot = PresentationSource.FromVisual(menu)!.RootVisual;
                            VisualTreeHelper.SetRootDpi(popupRoot, new DpiScale(scale, scale));
                            menu.UpdateLayout(); Drain();
                            Assert.Equal(scale, VisualTreeHelper.GetDpi(menu).DpiScaleX, 6);
                            var items = menu.Items.OfType<MenuItem>().ToArray();
                            Assert.Equal(7, items.Length);
                            foreach (var item in items)
                            {
                                Assert.True(item.IsVisible && item.IsEnabled);
                                Assert.True(item.ActualHeight >= item.FontSize + 4);
                                Assert.True(new Rect(menu.RenderSize).Contains(item.TransformToAncestor(menu).TransformBounds(new Rect(item.RenderSize))));
                            }
                            SavePreview(menu, scale, Path.Combine(directory, tag + "-menu.png"));
                            items[2].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Drain();
                            Assert.Equal(1.0, (double)typeof(VisionImageViewport).GetField("_scale", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(viewport)!);
                        }
                        finally { menu.IsOpen = false; Drain(); }

                        var exported = Path.Combine(directory, tag + "-image.png");
                        page.ImagePanel.SaveImage(exported);
                        using var stream = File.OpenRead(exported);
                        var bitmap = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
                        Assert.Equal(image.Width, bitmap.PixelWidth); Assert.Equal(image.Height, bitmap.PixelHeight);
                        Assert.Equal(8, bitmap.Format.BitsPerPixel);
                        var actual = new byte[image.Pixels.Length]; bitmap.CopyPixels(actual, image.Width, 0);
                        Assert.Equal(image.Pixels, actual);
                        Assert.Same(image, browser.InputImage);
                        viewport.Fit(); Drain();
                    }
                }
            }
            finally
            {
                menu.IsOpen = false;
                VisualTreeHelper.SetRootDpi(window, originalDpi);
                theme.Initialize(originalTheme.IsDark, originalTheme.Primary, originalTheme.Secondary);
                Drain();
            }
        }));
    }
    private static int Brightness(Color color) => color.R + color.G + color.B;
    private static void SavePreview(FrameworkElement view, double scale, string path)
    {
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(view.ActualWidth * scale), (int)Math.Ceiling(view.ActualHeight * scale),
            96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bitmap.Render(view);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); png.Save(stream);
    }

    private static void WithPage(Action<VisionToolEditorView, VisionBrowserViewModel> action)
    {
        var pixels = new byte[640 * 480];
        for (var y = 0; y < 480; y++) for (var x = 0; x < 640; x++) pixels[y * 640 + x] = (byte)(35 + (x / 40 + y / 40) % 2 * 55);
        using var workspace = new VisionBrowserWorkspace(new(
            [new("示例工具（仅布局）", "Preview", () => (new PreviewTool(), new object(), null))], [],
            _ => throw new InvalidOperationException(), () => VisionImage.FromBuffer(640, 480, Vision.Core.Imaging.PixelFormat.Mono8, pixels)));
        using var browser = new VisionBrowserViewModel(workspace);
        var page = new VisionToolEditorView { Session = browser,
            ToolbarAccessory = new WPFCustomControls.Controls.AppearancePickerView { Theme = new Extension.UI.Theme.ThemeService() } };
        var window = new Window { Content = page, Width = 1300, Height = 850, Left = -10000, Top = -10000, ShowInTaskbar = false };
        window.Show(); Drain();
        try { action(page, browser); }
        finally { window.Close(); Drain(); }
    }
    private sealed class PreviewTool : IExecutableTool
    {
        public string Name => "离线布局预览";
        public long LastElapsedMs => 0;
        public Vision.Core.Results.ValidationResult Validate() => new();
        public ToolResult Run(VisionImage? image) => throw new InvalidOperationException("布局测试不执行算法");
        public void EnsureLoaded() => throw new InvalidOperationException("布局测试不加载模型");
        public void Dispose() { }
    }
    private static void Drain() => Application.Current.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}
