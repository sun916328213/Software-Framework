using System.IO;
using System.Reflection;
using System.Windows.Media;
using Vision.Controls.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Vision.Controls.Models;
using Vision.Controls.Presentation;
using Vision.Controls.Services;
using Vision.Controls.View;
using Vision.Core.Imaging;
using PixelFormat = Vision.Core.Imaging.PixelFormat;
using Vision.Core.Results;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
[Trait("Category", "Regression")]
public sealed class VisionFlowP4Tests(WorkbenchAppFixture host)
{
    [Fact]
    public async Task ActualTemplateSample_UsesSharedCanvas_ExportsLocalPixels_AndRetiresOnClear()
    {
        VisionToolInstanceWorkspace workspace = null!; Window window = null!; VisionToolEditorView page = null!;
        var entry = OpenCvToolCatalog.Entries.Single(e => e.Name == "OpenCV/TemplateMatch");
        var catalog = new VisionBrowserCatalog([entry], [], _ => throw new NotSupportedException(), () => null);
        // 相同尺寸也必须隔离坐标，不能仅靠Width/Height判断能否编辑输入ROI。
        var input = VisionImage.FromBuffer(4, 3, PixelFormat.Mono8, new byte[12]);
        byte[] pixels = [1, 9, 3, 8, 5, 7, 2, 6, 4, 11, 12, 10];
        var sample = VisionImage.FromBuffer(4, 3, PixelFormat.Mono8, pixels);
        var path = Path.Combine(AppContext.BaseDirectory, "p4-acceptance", "training.png");
        host.Invoke(() =>
        {
            workspace = new(catalog, entry); workspace.SetImage(input);
            page = new VisionToolEditorView { Session = workspace.Editor };
            window = new Window { Content = page, Width = 1000, Height = 680, ShowInTaskbar = false, Left = -10000 }; window.Show(); Idle();
        });
        try
        {
            host.Invoke(() =>
            {
                var selector = Assert.IsType<ListBox>(page.ImageToolbar.FindName("ImageViewSelector"));
                var option = Assert.Single(selector.Items.OfType<ListBoxItem>(), item => Equals(item.Content, "训练图像"));
                Assert.False(option.IsEnabled);
                Assert.True(workspace.Editor.TemplateEditor!.Capture(sample)); Idle(); Assert.True(option.IsEnabled);
                selector.SelectedItem = option; Idle();
                Assert.Equal(ImageViewKind.Training, workspace.Editor.SelectedImageView);
                Assert.Same(input, workspace.Image);
                page.ImagePanel.ImageViewport.ShowRois = true; page.ImagePanel.ImageViewport.Mode = "矩形";
                typeof(VisionImageViewport).GetField("_draft", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(page.ImagePanel.ImageViewport, new Rect(0, 0, 2, 2));
                Assert.False(page.ImagePanel.ImageViewport.CompleteDrawing());
                Assert.Empty(workspace.RoiEditor.Rois);
                page.ImagePanel.ImageViewport.ActualPixels(); page.ImagePanel.ImageViewport.ZoomIn();
                Directory.CreateDirectory(Path.GetDirectoryName(path)!); page.ImagePanel.SaveImage(path);
                using (var stream = File.OpenRead(path))
                {
                    var saved = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
                    Assert.Equal(4, saved.PixelWidth); Assert.Equal(3, saved.PixelHeight);
                    var actual = new byte[12]; saved.CopyPixels(actual, 4, 0); Assert.Equal(pixels, actual);
                }
                workspace.Editor.TemplateEditor.Clear(); Idle(); Assert.False(option.IsEnabled);
                // 不能在训练样本消失后悄悄导出输入图。
                Assert.Throws<InvalidOperationException>(() => page.ImagePanel.SaveImage(path + ".missing.png"));
                Assert.Same(input, workspace.Image); Assert.Null(workspace.Result);
            });
        }
        finally { host.Invoke(window.Close); await host.Invoke(() => workspace.DisposeAsync().AsTask()); }
    }

    [Fact]
    public void ThirdPartyOutputImage_UsesExistingResultCapability_NotConcreteBackendType()
    {
        var image = VisionImage.FromBuffer(2, 1, PixelFormat.Mono8, [17, 83]);
        using var result = new ExternalOutput { Success = true, OutputImage = image };
        Assert.Same(image, VisionDisplay.OutputImage(result));
        result.Success = false; Assert.Null(VisionDisplay.OutputImage(result));
    }
    [Fact]
    public void SwitchingTool_RetiresTrainingViewAndSubscription_WithoutDisposingBorrowedSession()
    {
        host.Invoke(() =>
        {
            var entries = OpenCvToolCatalog.Entries.Where(e => e.Name is "OpenCV/TemplateMatch" or "OpenCV/Threshold").ToArray();
            using var workspace = new VisionBrowserWorkspace(new(entries, [], _ => throw new NotSupportedException(), TestBrowserViewModel.CreateInput));
            using var browser = new VisionBrowserViewModel(workspace);
            browser.SelectedEntry = entries.Single(e => e.Name == "OpenCV/TemplateMatch");
            browser.LoadImage(); browser.RoiEditor.AddRectangle(10, 10, 4, 4); browser.SetTemplateFromRoi();
            var oldTemplate = browser.TemplateEditor!;
            Assert.NotNull(browser.TrainingImage); browser.SelectedImageView = ImageViewKind.Training;
            browser.SelectedEntry = entries.Single(e => e.Name == "OpenCV/Threshold");
            Assert.False(oldTemplate.IsActive); Assert.Null(browser.TrainingImage);
            Assert.Equal(ImageViewKind.Input, browser.SelectedImageView);
            using (var borrower = new VisionToolEditorSession(workspace.Model, workspace.RoiEditor)) { }
            Assert.True(browser.IsSessionActive); Assert.NotNull(browser.CurrentCard);
            browser.CurrentCard!.Run(); Assert.True(browser.CurrentCard.LastResult!.Success);
        });
    }

    [Theory]
    [InlineData(900, false, 1.0)]
    [InlineData(900, true, 2.0)]
    [InlineData(1300, false, 1.5)]
    [InlineData(1300, true, 1.25)]
    public async Task FourViews_KeepInputAndModelIdentity_AcrossThemeDpiAndReload(int width, bool dark, double dpi)
    {
        VisionToolInstanceWorkspace workspace = null!; Window window = null!; VisionToolEditorView page = null!;
        var entry = OpenCvToolCatalog.Entries.Single(e => e.Name == "OpenCV/TemplateMatch");
        var catalog = new VisionBrowserCatalog([entry], [], _ => throw new NotSupportedException(), () => null);
        var input = VisionImage.FromBuffer(8, 8, PixelFormat.Mono8, Enumerable.Range(0, 64).Select(n => (byte)(n * 3)).ToArray());
        var sample = VisionImage.FromBuffer(4, 3, PixelFormat.Mono8, [3, 6, 9, 12, 27, 30, 33, 36, 51, 54, 57, 60]);
        host.Invoke(() =>
        {
            workspace = new(catalog, entry); workspace.SetImage(input);
            // 此微小渐变样本用于四视图/主题/图像身份验收，不拿歧义模板验证旋转候选搜索。
            var matching = Assert.IsType<Vision.Tools.OpenCv.Matching.TemplateMatchParameters>(workspace.Parameters);
            matching.AngleStartDeg = 0; matching.AngleExtentDeg = 0;
            Assert.True(workspace.Editor.TemplateEditor!.Capture(sample));
            workspace.Editor.CurrentCard!.Run(); Assert.True(workspace.Result!.Success, workspace.Result.Message);
            page = new VisionToolEditorView { Session = workspace.Editor };
            window = new Window { Content = page, Width = width, Height = 760, ShowInTaskbar = false, Left = -10000 }; window.Show(); Idle();
        });
        try
        {
            host.Invoke(() =>
            {
                var theme = ((global::Vision.Workbench.ViewModels.MainWindowViewModel)Application.Current.MainWindow.DataContext).Theme;
                var before = (theme.IsDark, theme.Primary, theme.Secondary); var oldDpi = VisualTreeHelper.GetDpi(window);
                try
                {
                    theme.Initialize(dark, before.Primary, before.Secondary); VisualTreeHelper.SetRootDpi(window, new DpiScale(dpi, dpi));
                    var result = workspace.Result; var training = workspace.Editor.TrainingImage;
                    workspace.Editor.CurrentCard!.Model.IsReadOnly = true; Assert.False(workspace.Editor.IsEditingEnabled);
                    var selector = (ListBox)page.ImageToolbar.FindName("ImageViewSelector");
                    foreach (var kind in new[] { ImageViewKind.Input, ImageViewKind.Roi, ImageViewKind.Result, ImageViewKind.Training })
                    {
                        selector.SelectedValue = kind; Idle(); Assert.Equal(kind, workspace.Editor.SelectedImageView);
                        window.Content = null; Idle(); window.Content = page; Idle();
                        var viewport = page.ImagePanel.ImageViewport; viewport.ActualPixels(); viewport.ZoomIn(); viewport.Fit();
                        Assert.Equal(dpi, VisualTreeHelper.GetDpi(viewport).DpiScaleX, 6);
                        Assert.Same(input, workspace.Image); Assert.Same(result, workspace.Result); Assert.Same(training, workspace.Editor.TrainingImage);
                        Assert.Equal(kind == ImageViewKind.Training, viewport.ShowTraining);
                        var bar = (Border)page.ImageToolbar.FindName("ImageViewsBar");
                        foreach (var tab in selector.Items.OfType<ListBoxItem>())
                            Assert.True(new Rect(bar.RenderSize).Contains(tab.TransformToAncestor(bar).TransformBounds(new Rect(tab.RenderSize))));
                        var folder = Path.Combine(AppContext.BaseDirectory, "p4-acceptance"); Directory.CreateDirectory(folder);
                        var tag = $"{width}-{dark}-{dpi}-{kind}";
                        page.ImagePanel.SaveImage(Path.Combine(folder, tag + "-pixels.png"));
                        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(page.ActualWidth * dpi), (int)Math.Ceiling(page.ActualHeight * dpi), 96 * dpi, 96 * dpi, PixelFormats.Pbgra32);
                        bitmap.Render(page); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                        using var stream = File.Create(Path.Combine(folder, tag + "-viewport.png")); encoder.Save(stream);
                    }
                }
                finally { theme.Initialize(before.IsDark, before.Primary, before.Secondary); VisualTreeHelper.SetRootDpi(window, oldDpi); }
            });
        }
        finally { host.Invoke(window.Close); await host.Invoke(() => workspace.DisposeAsync().AsTask()); }
    }

    [Theory]
    [InlineData(".png")]
    [InlineData(".tif")]
    public void TrainingDisplayContract_PreservesMono16Stride_WithoutOwningTemplate(string extension)
    {
        host.Invoke(() =>
        {
            // 仅测试托管取样像素显示契约，不执行或宣称HALCON训练。
            using var tool = new TemplateEditorWorkflowTests.FakeTemplateTool();
            byte[] pixels = [0, 0, 255, 255, 0, 128, 99, 99, 52, 18, 1, 0, 254, 255, 88, 88];
            using var template = new TemplateEditorViewModel(tool, () => { }, null);
            Assert.True(template.Capture(VisionImage.FromBuffer(3, 2, PixelFormat.Mono16, pixels, stride: 8)));
            using var session = new VisionToolEditorSession(new VisionToolEditorModel
            {
                TemplateEditor = template, SupportsTemplateInput = true,
                InputImage = VisionImage.FromBuffer(3, 2, PixelFormat.Mono8, new byte[6]), SelectedImageView = ImageViewKind.Training
            });
            var panel = new VisionImagePanel { DataContext = session };
            var window = new Window { Content = panel, Width = 600, Height = 420, ShowInTaskbar = false, Left = -10000 };
            window.Show(); Idle();
            var folder = Path.Combine(AppContext.BaseDirectory, "p4-acceptance"); Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, "training-mono16" + extension);
            try
            {
                panel.ImageViewport.ActualPixels(); panel.ImageViewport.ZoomIn(); panel.SaveImage(path);
                using var stream = File.OpenRead(path);
                var saved = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
                Assert.Equal(3, saved.PixelWidth); Assert.Equal(2, saved.PixelHeight); Assert.Equal(16, saved.Format.BitsPerPixel);
                var actual = new byte[12]; saved.CopyPixels(actual, 6, 0);
                Assert.Equal(new byte[] { 0, 0, 255, 255, 0, 128, 52, 18, 1, 0, 254, 255 }, actual);
                session.Dispose(); Assert.True(template.IsActive); Assert.False(tool.Disposed);
            }
            finally { window.Close(); }
        });
    }

    private sealed class ExternalOutput : ToolResult, IOutputImageResult { public VisionImage OutputImage { get; init; } = null!; }
    private static void Idle() => Application.Current.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
}
