using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf;
using Vision.Controls;
using Vision.Controls.Services;
using Vision.Controls.View;
using Vision.Controls.ViewModels;
using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Results;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
[Trait("Category", "Regression")]
public sealed class LineRoiUnificationTests(WorkbenchAppFixture host)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GeometryLivesOnlyInRoiTab_AlgorithmFieldsStayInParameters_AndOpeningDoesNotChangeJson(bool calipers)
    {
        host.Invoke(() => WithPage(calipers, (page, browser, region) =>
        {
            var before = JsonSerializer.Serialize(region, region.GetType());
            var parameters = Assert.IsType<VisionPropertyPanel>(page.DetailsPanel.FindName("ParametersPanel"));
            var names = parameters.Model.Groups.SelectMany(group => group.Items).OfType<Vision.Controls.Presentation.VisionPropertyItem>().Where(item => item.IsRowVisible).Select(item => item.PropertyName).ToArray();
            Assert.DoesNotContain("LineStart", names); Assert.DoesNotContain("LineEnd", names);
            Assert.DoesNotContain(calipers ? "CaliperHalfLength" : "BandHalfWidth", names);
            Assert.Contains(calipers ? "Threshold" : "CannyLow", names);
            ((TabItem)page.DetailsPanel.FindName("RoiTab")).IsSelected = true; Drain();
            var editor = Assert.IsAssignableFrom<UserControl>(page.DetailsPanel.FindName("LineRegionEditor"));
            var geometry = Assert.IsType<VisionPropertyPanel>(editor.FindName("GeometryPanel"));
            Assert.Same(region, geometry.TargetObject);
            var geometryNames = geometry.Model.Groups.SelectMany(group => group.Items).OfType<Vision.Controls.Presentation.VisionPropertyItem>().Where(item => item.IsRowVisible).Select(item => item.PropertyName).ToArray();
            Assert.Contains("LineStart", geometryNames); Assert.Contains("LineEnd", geometryNames);
            Assert.Contains(calipers ? "CaliperHalfLength" : "BandHalfWidth", geometryNames);
            Assert.DoesNotContain(calipers ? "Threshold" : "CannyLow", geometryNames);
            if (calipers) { Assert.Contains("CaliperCount", geometryNames); Assert.Contains("CaliperHalfWidth", geometryNames); }
            else Assert.DoesNotContain("CaliperCount", geometryNames);
            Assert.Equal(before, JsonSerializer.Serialize(region, region.GetType()));
            Assert.Same(browser.InputImage, page.ImagePanel.ImageViewport.Image);
            Assert.False(page.ImagePanel.ImageViewport.ShowRoiImage); Assert.False(page.ImagePanel.ImageViewport.ShowOutput);
            Assert.True(page.ImagePanel.ImageViewport.ShowRois);
        }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NumericEditAndCanvasDrag_ShareOriginalDto_RefreshLiveAndRollback_WithoutChangingAlgorithmFields(bool calipers)
    {
        host.Invoke(() => WithPage(calipers, (page, browser, region) =>
        {
            ((TabItem)page.DetailsPanel.FindName("RoiTab")).IsSelected = true; Drain();
            var editor = Assert.IsAssignableFrom<UserControl>(page.DetailsPanel.FindName("LineRegionEditor"));
            var geometry = Assert.IsType<VisionPropertyPanel>(editor.FindName("GeometryPanel"));
            var length = Assert.Single(Descendants(geometry).OfType<DecimalUpDown>(), control =>
                BindingOperations.GetBinding(control, DecimalUpDown.ValueProperty)?.Path?.Path == (calipers ? "CaliperHalfLength" : "BandHalfWidth"));
            browser.CurrentCard!.Run(); Assert.NotNull(browser.CurrentCard.LastResult); Assert.False(browser.ShowRois);
            length.SetCurrentValue(DecimalUpDown.ValueProperty, 23m); Drain();
            Assert.Equal(23, region.NormalHalfLength); Assert.Null(browser.CurrentCard.LastResult);
            Assert.True(browser.ShowRois); Assert.True(browser.ShowOriginalImage);
            browser.CurrentCard.Run(); Assert.NotNull(browser.CurrentCard.LastResult);
            browser.SelectedImageView = Vision.Controls.Models.ImageViewKind.Roi; Drain();
            var viewport = page.ImagePanel.ImageViewport;
            var session = new LineSearchEditorSession(region);
            Assert.True(session.Begin(region.LineStart, 1));
            typeof(VisionImageViewport).GetField("_lineGesture", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(viewport, session);
            typeof(VisionImageViewport).GetMethod("UpdateLineSearch", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(viewport, [new Point(110, 120)]);
            Drain();
            var x = Descendants(geometry).OfType<DecimalUpDown>().First(control => BindingOperations.GetBinding(control, DecimalUpDown.ValueProperty)?.Path?.Path == "LineStart");
            Assert.Equal(110m, x.Value); Assert.Equal(new Point2D(110, 120), region.LineStart);
            Assert.Null(browser.CurrentCard.LastResult);
            viewport.Mode = "平移"; Drain();
            x = Descendants(geometry).OfType<DecimalUpDown>().First(control => BindingOperations.GetBinding(control, DecimalUpDown.ValueProperty)?.Path?.Path == "LineStart");
            Assert.Equal(100m, x.Value); Assert.Equal(new Point2D(100, 100), region.LineStart);
            Assert.Equal(23, region.NormalHalfLength);
            if (region is Vision.Tools.Halcon.Measurement.FindLineParameters hp) Assert.Equal(30, hp.Threshold);
            if (region is Vision.Tools.OpenCv.Measurement.FindLineParameters cp) Assert.Equal(50, cp.CannyLow);
        }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BothBackends_DrawSameFrameAndThreeHandles_OnlyRealCalipersGetSamplingBoxes(bool calipers)
    {
        host.Invoke(() => WithPage(calipers, (page, browser, region) =>
        {
            region.LineStart = new(10, 20); region.LineEnd = new(30, 20); region.NormalHalfLength = 4;
            if (region is Vision.Tools.Halcon.Measurement.FindLineParameters hp) { hp.CaliperCount = 3; hp.CaliperHalfWidth = 2; }
            var drawing = new DrawingGroup();
            using (var context = drawing.Open())
                typeof(VisionImageViewport).GetMethod("DrawLineSearch", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(page.ImagePanel.ImageViewport, [context]);
            var shapes = drawing.Children.OfType<GeometryDrawing>().ToArray();
            var outlines = shapes.Where(shape => shape.Geometry is StreamGeometry).ToArray();
            var frame = outlines[0];
            Assert.Equal(Colors.Yellow, Assert.IsType<SolidColorBrush>(frame.Pen.Brush).Color);
            Assert.Equal(new Rect(calipers ? 8 : 10, 16, calipers ? 24 : 20, 8), frame.Geometry.Bounds);
            Assert.Equal(calipers ? 2 : 1, outlines.Length);
            if (calipers)
            {
                Assert.Equal(Colors.Cyan, Assert.IsType<SolidColorBrush>(outlines[1].Pen.Brush).Color);
                Assert.Equal(3, PathGeometry.CreateFromGeometry(outlines[1].Geometry).Figures.Count);
            }
            Assert.Equal(7, shapes.Count(shape => shape.Geometry is RectangleGeometry && shape.Brush is SolidColorBrush brush && brush.Color == Colors.Yellow));
        }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnloadReloadBorrowsSession_AndLateOldGeometryCallbackCannotClearReplacementResult(bool calipers)
    {
        host.Invoke(() => WithPage(calipers, (page, browser, region) =>
        {
            ((TabItem)page.DetailsPanel.FindName("RoiTab")).IsSelected = true; Drain();
            var editor = Assert.IsAssignableFrom<UserControl>(page.DetailsPanel.FindName("LineRegionEditor"));
            var geometry = Assert.IsType<VisionPropertyPanel>(editor.FindName("GeometryPanel"));
            var window = Window.GetWindow(page);
            window.Content = null; Drain();
            Assert.Null(geometry.Model.TargetObject);
            Assert.Same(region, browser.CurrentParameters);
            browser.CurrentCard!.Run(); Assert.True(browser.CurrentCard.LastResult!.Success);
            window.Content = page; Drain();
            Assert.Same(region, geometry.Model.TargetObject); Assert.NotEmpty(geometry.Model.Groups);
            browser.SelectedEntry = browser.ToolEntries.Last(); Drain();
            Assert.NotSame(region, browser.CurrentParameters);
            Assert.Same(browser.CurrentParameters, geometry.TargetObject);
            browser.CurrentCard!.Run(); var result = browser.CurrentCard.LastResult;
            typeof(ToolDetailsView).GetMethod("LineRoiGeometryEdited", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(page.DetailsPanel, [editor, new LineSearchEditedEventArgs(region, true)]);
            Assert.Same(result, browser.CurrentCard.LastResult);
            Assert.Same(browser.LineSearch, page.ImagePanel.ImageViewport.LineSearch);
        }));
    }

    [Fact]
    public void OpenCvCalipers_DrawRealSamplingArray_AndBorrowTheOriginalGeometry()
    {
        host.Invoke(() => WithPage(false, (page, browser, region) =>
        {
            var parameters = Assert.IsType<Vision.Tools.OpenCv.Measurement.FindLineParameters>(region);
            Assert.Equal(LineSearchKind.Calipers, parameters.SearchMode);
            Assert.Same(parameters, page.ImagePanel.ImageViewport.LineSearch);
            var expected = new Vision.Tools.Halcon.Measurement.FindLineParameters();
            Assert.Equal(LineSearchGeometry.EditingFrame(expected).Size, LineSearchGeometry.EditingFrame(region).Size);
            ((TabItem)page.DetailsPanel.FindName("RoiTab")).IsSelected = true; Drain();
            var editor = Assert.IsType<LineRoiEditorView>(page.DetailsPanel.FindName("LineRegionEditor"));
            Assert.Contains("真实采样卡尺", editor.Description);
            var geometry = Assert.IsType<VisionPropertyPanel>(editor.FindName("GeometryPanel"));
            Assert.Same(parameters, geometry.TargetObject);
            var count = Descendants(geometry).OfType<NumericUpDown>().Single(control =>
                BindingOperations.GetBinding(control, NumericUpDown.ValueProperty)?.Path?.Path == nameof(parameters.CaliperCount));
            count.SetCurrentValue(NumericUpDown.ValueProperty, 5); Drain();
            Assert.Equal(5, parameters.CaliperCount);
            var drawing = new DrawingGroup();
            using (var context = drawing.Open())
                typeof(VisionImageViewport).GetMethod("DrawLineSearch", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(page.ImagePanel.ImageViewport, [context]);
            var cyan = Assert.Single(drawing.Children.OfType<GeometryDrawing>(), shape => shape.Geometry is StreamGeometry && shape.Pen.Brush is SolidColorBrush brush && brush.Color == Colors.Cyan);
            Assert.Equal(5, PathGeometry.CreateFromGeometry(cyan.Geometry).Figures.Count);
            Assert.Empty(browser.RoiEditor.Rois);
        }, openCvCalipers: true));
    }

    [Fact]
    public void OpenCvModeSwitch_UpdatesVisibleEditorsAndDescription_WithoutReplacingDtoOrRunning()
    {
        host.Invoke(() => WithPage(false, (page, browser, region) =>
        {
            var parameters = Assert.IsType<Vision.Tools.OpenCv.Measurement.FindLineParameters>(region);
            var panel = Assert.IsType<VisionPropertyPanel>(page.DetailsPanel.FindName("ParametersPanel"));
            var mode = Descendants(panel).OfType<ComboBox>().Single(control =>
                BindingOperations.GetBinding(control, ComboBox.SelectedItemProperty)?.Path?.Path == nameof(parameters.SearchMode));
            var rows = panel.Model.Groups.SelectMany(group => group.Items).OfType<Vision.Controls.Presentation.VisionPropertyItem>().ToArray();
            Assert.True(rows.Single(row => row.PropertyName == nameof(parameters.CannyLow)).IsRowVisible);
            Assert.False(rows.Single(row => row.PropertyName == nameof(parameters.Threshold)).IsRowVisible);
            browser.CurrentCard!.Run(); Assert.NotNull(browser.CurrentCard.LastResult);
            mode.SetCurrentValue(ComboBox.SelectedItemProperty, LineSearchKind.Calipers); Drain();
            Assert.Same(parameters, browser.CurrentParameters);
            Assert.Equal(LineSearchKind.Calipers, region.SearchKind);
            Assert.Null(browser.CurrentCard.LastResult); // 清旧结果，不隐式执行。
            Assert.False(rows.Single(row => row.PropertyName == nameof(parameters.CannyLow)).IsRowVisible);
            Assert.True(rows.Single(row => row.PropertyName == nameof(parameters.Threshold)).IsRowVisible);
            var editor = Assert.IsType<LineRoiEditorView>(page.DetailsPanel.FindName("LineRegionEditor"));
            Assert.Contains("真实采样卡尺", editor.Description);
            mode.SetCurrentValue(ComboBox.SelectedItemProperty, LineSearchKind.EdgeBand); Drain();
            Assert.Equal(LineSearchKind.EdgeBand, region.SearchKind);
            Assert.True(rows.Single(row => row.PropertyName == nameof(parameters.CannyLow)).IsRowVisible);
            Assert.Contains("连续搜索带", editor.Description);
            Assert.Equal(50, parameters.CannyLow); Assert.Equal(10, parameters.BandHalfWidth);
        }));
    }

    private static void WithPage(bool calipers, Action<VisionToolEditorView, VisionBrowserViewModel, ILineSearchRegion> action, bool openCvCalipers = false)
    {
        ILineSearchRegion region = calipers ? new Vision.Tools.Halcon.Measurement.FindLineParameters() : new Vision.Tools.OpenCv.Measurement.FindLineParameters
        { SearchMode = openCvCalipers ? LineSearchKind.Calipers : LineSearchKind.EdgeBand };
        using var workspace = new VisionBrowserWorkspace(new(
            [new("NoBackendNameInference", "Other", () => (new FakeTool(), region, null)),
             new("AnotherLine", "Other", () => (new FakeTool(), new Vision.Tools.OpenCv.Measurement.FindLineParameters(), null))], [],
            _ => throw new InvalidOperationException(), () => VisionImage.FromBuffer(320, 240, Vision.Core.Imaging.PixelFormat.Mono8, new byte[320 * 240])));
        using var browser = new VisionBrowserViewModel(workspace);
        browser.LoadImage();
        var page = new VisionToolEditorView { Session = browser };
        var window = new Window { Content = page, Width = 1300, Height = 850, Left = -10000, Top = -10000, ShowInTaskbar = false };
        window.Show(); Drain();
        try { action(page, browser, region); }
        finally { window.Close(); Drain(); }
    }
    private sealed class FakeTool : IExecutableTool
    {
        public string Name => "Line";
        public long LastElapsedMs => 0;
        public Vision.Core.Results.ValidationResult Validate() => new();
        private bool _disposed;
        public ToolResult Run(VisionImage? image) { ObjectDisposedException.ThrowIf(_disposed, this); return new() { Success = true }; }
        public void EnsureLoaded() { }
        public void Dispose() => _disposed = true;
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
