using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Vision.Controls.Services;
using Vision.Controls.View;
using Vision.Core.Imaging;
using Vision.Tools.OpenCv.Processing;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
public sealed class VisionRoiPurposeTests(WorkbenchAppFixture host)
{
    [Fact]
    public async Task CropPurpose_TracksActualGeometryDtoCancellationImportAndBorrowedWindow()
    {
        var catalog = Catalog();
        var crop = host.Invoke(() => new VisionToolInstanceWorkspace(catalog, catalog.Tools.Single(t => t.Name == "OpenCV/Crop")));
        var other = host.Invoke(() => new VisionToolInstanceWorkspace(catalog, catalog.Tools.Single(t => t.Name == "OpenCV/Threshold")));
        try
        {
            host.Invoke(() =>
            {
                crop.SetImage(Image());
                var parameters = Assert.IsType<CropParameters>(crop.Parameters);
                parameters.X = 3; parameters.Y = 4; parameters.Width = 12; parameters.Height = 14;
                var page = new VisionToolEditorView { Session = crop.Editor };
                var window = Show(page);
                try
                {
                    SelectRoiTab(page);
                    var purpose = Purpose(page);
                    Assert.Contains("裁剪", purpose.Text);
                    var roi = crop.RoiEditor.AddRectangle(20, 20, 20, 10);
                    Drain();
                    Assert.Contains("未应用", purpose.Text);
                    Assert.Equal((3d, 4d, 12d, 14d), (parameters.X, parameters.Y, parameters.Width, parameters.Height));
                    crop.Editor.SetCropRegionFromRoi(); Drain();
                    Assert.Contains("已应用", purpose.Text);
                    Assert.Equal((10d, 15d, 20d, 10d), (parameters.X, parameters.Y, parameters.Width, parameters.Height));
                    var saved = crop.ExportConfiguration();
                    Assert.NotSame(roi, saved.Rois[0]);
                    crop.RoiEditor.SelectedX = 25; Drain();
                    Assert.Equal(20, saved.Rois[0].Pose.X);
                    Assert.Contains("未应用", purpose.Text);
                    Assert.Equal(10, parameters.X);
                    Assert.True(crop.RoiEditor.TryBeginManipulation(new(25, 20), 0.1));
                    Assert.True(crop.RoiEditor.UpdateManipulation(new(30, 20)));
                    crop.RoiEditor.EndManipulation(cancel: true); Drain();
                    Assert.Equal(25, roi.Pose.X);
                    Assert.Contains("未应用", purpose.Text);
                    crop.ApplyConfiguration(saved); Drain();
                    Assert.Same(parameters, crop.Parameters);
                    Assert.NotSame(saved.Rois[0], crop.RoiEditor.Selected);
                    Assert.Equal(20, crop.RoiEditor.Selected!.Pose.X);
                    Assert.Contains("已应用", purpose.Text);
                    parameters.X = 11; Drain();
                    Assert.Contains("未应用", purpose.Text);
                    parameters.X = 10; Drain();
                    Assert.Contains("已应用", purpose.Text);
                    crop.RoiEditor.Selected = null; Drain();
                    Assert.DoesNotContain("已应用", purpose.Text);
                    Assert.Equal(10, parameters.X);
                    page.Session = other.Editor; Drain();
                    Assert.DoesNotContain("裁剪", Purpose(page).Text);
                    crop.RoiEditor.Selected = crop.RoiEditor.Rois[0];
                    crop.RoiEditor.SelectedX = 29; Drain();
                    Assert.DoesNotContain("未应用", Purpose(page).Text);
                    page.Session = crop.Editor; Drain();
                    Assert.Contains("未应用", Purpose(page).Text);
                    window.Content = null; Drain();
                    crop.RoiEditor.SelectedX = 20;
                    window.Content = page; Drain(); SelectRoiTab(page);
                    Assert.Contains("已应用", Purpose(page).Text);
                    Assert.Equal(10, parameters.X);
                }
                finally { window.Close(); Drain(); }
            });
        }
        finally
        {
            await host.Invoke(() => crop.DisposeAsync().AsTask());
            await host.Invoke(() => other.DisposeAsync().AsTask());
        }
    }

    [Fact]
    public async Task ExportedPolygon_IsIndependentInBothDirections()
    {
        var catalog = Catalog();
        var workspace = host.Invoke(() => new VisionToolInstanceWorkspace(catalog, catalog.Tools.Single(t => t.Name == "OpenCV/Threshold")));
        try
        {
            host.Invoke(() =>
            {
                var roi = workspace.RoiEditor.AddPolygon([new(1, 1), new(8, 1), new(1, 8)]);
                var saved = workspace.ExportConfiguration();
                Assert.NotSame(roi.OutlinePoints, saved.Rois[0].OutlinePoints);
                roi.OutlinePoints[0] = new(2, 2);
                Assert.Equal(new Vision.Core.Geometry.Point2D(1, 1), saved.Rois[0].OutlinePoints[0]);
                workspace.ApplyConfiguration(saved);
                workspace.RoiEditor.Selected!.OutlinePoints[0] = new(3, 3);
                Assert.Equal(new Vision.Core.Geometry.Point2D(1, 1), saved.Rois[0].OutlinePoints[0]);
            });
        }
        finally { await host.Invoke(() => workspace.DisposeAsync().AsTask()); }
    }

    [Theory]
    [InlineData(-1.2, 2.3, 5.4, 6.2, 0, 2, 5, 7)]
    [InlineData(98.2, 99.1, 5.0, 4.0, 98, 99, 2, 1)]
    public async Task CropPurpose_PixelBoundsMatchCrop(double x, double y, double width, double height, int left, int top, int pixelWidth, int pixelHeight)
    {
        var catalog = Catalog();
        var workspace = host.Invoke(() => new VisionToolInstanceWorkspace(catalog, catalog.Tools.Single(t => t.Name == "OpenCV/Crop")));
        try
        {
            host.Invoke(() =>
            {
                var image = Image();
                workspace.SetImage(image);
                var parameters = Assert.IsType<CropParameters>(workspace.Parameters);
                parameters.X = x; parameters.Y = y; parameters.Width = width; parameters.Height = height;
                var result = ImageRegionCrop.Crop(image, new(x, y, width, height));
                Assert.Equal((left, top, pixelWidth, pixelHeight),
                    ((int)result.SourceBounds.X, (int)result.SourceBounds.Y, result.OutputImage.Width, result.OutputImage.Height));
                Assert.Contains($"生效像素范围：左上 ({left}, {top})，宽 {pixelWidth}，高 {pixelHeight}", workspace.Editor.RoiPurpose);
            });
        }
        finally { await host.Invoke(() => workspace.DisposeAsync().AsTask()); }
    }

    [Theory]
    [InlineData("OpenCV/FindCircle", "圆心")]
    [InlineData("OpenCV/Blob", "搜索")]
    [InlineData("OpenCV/TemplateMatch", "模板")]
    [InlineData("OpenCV/Threshold", "标注")]
    public async Task Purpose_DescribesTheActualToolCapability(string name, string expected)
    {
        var catalog = Catalog();
        var workspace = host.Invoke(() => new VisionToolInstanceWorkspace(catalog, catalog.Tools.Single(t => t.Name == name)));
        try
        {
            host.Invoke(() =>
            {
                var page = new VisionToolEditorView { Session = workspace.Editor };
                var window = Show(page);
                try
                {
                    SelectRoiTab(page);
                    var text = Purpose(page).Text;
                    Assert.Contains(expected, text);
                    if (name == "OpenCV/FindCircle")
                    {
                        Assert.Contains("半径", text);
                        Assert.DoesNotContain("ROI 设置半径", text);
                        Assert.DoesNotContain("ROI设置半径", text);
                    }
                    if (name == "OpenCV/Threshold") Assert.DoesNotContain("搜索域", text);
                }
                finally { window.Close(); Drain(); }
            });
        }
        finally { await host.Invoke(() => workspace.DisposeAsync().AsTask()); }
    }

    private static VisionBrowserCatalog Catalog() => new(OpenCvToolCatalog.Entries, [], _ => throw new NotSupportedException(), Image);
    private static VisionImage Image() => VisionImage.FromBuffer(100, 100, PixelFormat.Mono8, new byte[10000]);
    private static TextBlock Purpose(VisionToolEditorView page) => Assert.IsType<TextBlock>(page.DetailsPanel.FindName("RoiPurpose"));
    private static void SelectRoiTab(VisionToolEditorView page)
    {
        Assert.IsType<TabItem>(page.DetailsPanel.FindName("RoiTab")).IsSelected = true;
        Drain();
    }
    private static Window Show(object content)
    {
        var window = new Window { Content = content, Width = 1300, Height = 850, Left = -10000, Top = -10000, ShowInTaskbar = false };
        window.Show(); Drain(); return window;
    }
    private static void Drain() => Application.Current.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
}
