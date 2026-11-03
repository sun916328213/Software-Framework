using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Vision.Controls.Models;
using Vision.Controls.Services;
using Vision.Controls.View;
using Vision.Controls.ViewModels;
using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Results;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
public sealed class EditorFeedbackTests(WorkbenchAppFixture host)
{
    [Fact]
    public void StatusBelongsToImagePanel_AndToolIdentityRemainsAboveEveryDetailsTab()
    {
        host.Invoke(() => WithPage((page, browser) =>
        {
            var status = Assert.IsAssignableFrom<FrameworkElement>(page.ImagePanel.FindName("ExecutionStatus"));
            Assert.Contains(status, Descendants(page.ImagePanel));
            var identity = Assert.IsType<TextBlock>(page.DetailsPanel.FindName("ToolIdentity"));
            Assert.Null(page.DetailsPanel.FindName("DiagnosticsTab"));
            foreach (var tab in new[] { "ParametersTab", "RoiTab", "ResultsTab" })
            {
                if (page.DetailsPanel.FindName(tab) is TabItem item) item.IsSelected = true;
                Drain(); Assert.True(identity.IsVisible); Assert.Equal(browser.SelectedEntry!.Name, identity.Text);
            }
        }));
    }
    [Theory]
    [InlineData("ParametersTab")]
    [InlineData("ResultsTab")]
    [InlineData("RoiTab")]
    public void RoiImageIsEditableRegardlessOfDetailsTab_AndResultImageRestoresResultPicking(string tab)
    {
        host.Invoke(() => WithPage((page, browser) =>
        {
            browser.CurrentCard!.Run();
            var selectedTab = (TabItem)page.DetailsPanel.FindName(tab); selectedTab.IsSelected = true; Drain();
            browser.SelectedImageView = ImageViewKind.Roi; Drain();
            Assert.True(selectedTab.IsSelected); Assert.Equal("选择", page.ImagePanel.ImageViewport.Mode);
            Assert.True(page.ImagePanel.ImageViewport.IsRoiEditingEnabled); Assert.True(page.ImagePanel.ImageViewport.ShowRois);
            browser.SelectedImageView = ImageViewKind.Result; Drain();
            if (tab == "ResultsTab") Assert.Equal("结果选择", page.ImagePanel.ImageViewport.Mode);
        }));
    }
    [Fact]
    public void SelectedBlobFeaturesAppearAsResultColumns_AndSurviveTabReload()
    {
        host.Invoke(() => WithPage((page, browser) =>
        {
            browser.BlobEditor!.SelectedFeatures.Add("width"); browser.BlobEditor.SelectedFeatures.Add("circularity");
            browser.CurrentCard!.Run();
            ((TabItem)page.DetailsPanel.FindName("ResultsTab")).IsSelected = true; Drain();
            var grid = Assert.Single(Descendants(page.DetailsPanel).OfType<DataGrid>(), control => System.Windows.Automation.AutomationProperties.GetName(control) == "视觉结果表格");
            Assert.Contains(grid.Columns, column => Equals(column.Header, "外接宽(px)"));
            Assert.Contains(grid.Columns, column => Equals(column.Header, "circularity"));
            Assert.Single(browser.CurrentCard.Results.Items);
            var widthColumn = grid.Columns.Single(column => Equals(column.Header, "外接宽(px)"));
            grid.ScrollIntoView(grid.Items[0], widthColumn); Drain();
            Assert.Equal("4", Assert.IsType<TextBlock>(widthColumn.GetCellContent(grid.Items[0])).Text);
            ((TabItem)page.DetailsPanel.FindName("ParametersTab")).IsSelected = true; Drain();
            browser.BlobEditor.SelectedFeatures.Remove("width");
            ((TabItem)page.DetailsPanel.FindName("ResultsTab")).IsSelected = true; Drain();
            grid = Assert.Single(Descendants(page.DetailsPanel).OfType<DataGrid>(), control => System.Windows.Automation.AutomationProperties.GetName(control) == "视觉结果表格");
            Assert.DoesNotContain(grid.Columns, column => Equals(column.Header, "width"));
            Assert.Contains(grid.Columns, column => Equals(column.Header, "外接宽(px)")); // 尺寸是固定逐斑点列。
            Assert.Contains(grid.Columns, column => Equals(column.Header, "circularity"));
        }));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BothLineRegionsHaveCornerResizeAndRotationLikeRotatedAreaRoi_AndCancelRestoresNativeParameters(bool calipers)
    {
        ILineSearchRegion region = calipers ? new Vision.Tools.Halcon.Measurement.FindLineParameters { CaliperHalfWidth = 2 } : new Vision.Tools.OpenCv.Measurement.FindLineParameters();
        region.LineStart = new(0, 0); region.LineEnd = new(100, 0); region.NormalHalfLength = 10;
        var session = new LineSearchEditorSession(region);
        var corners = Assert.IsAssignableFrom<IReadOnlyList<Point2D>>(typeof(LineSearchEditorSession).GetProperty("ResizeHandles")?.GetValue(session));
        Assert.Equal(4, corners.Count);
        var corner = corners.OrderByDescending(p => p.X + p.Y).First();
        Assert.True(session.Begin(corner, 1)); Assert.True(session.Update(new(corner.X + 20, corner.Y + 10)));
        Assert.True(region.LineStart.DistanceTo(region.LineEnd) > 100); Assert.True(region.NormalHalfLength > 10);
        session.End(cancel: true);
        Assert.Equal(new Point2D(0, 0), region.LineStart); Assert.Equal(new Point2D(100, 0), region.LineEnd); Assert.Equal(10, region.NormalHalfLength);
        var rotation = Assert.IsType<Point2D>(typeof(LineSearchEditorSession).GetProperty("RotationHandle")?.GetValue(session));
        Assert.True(session.Begin(rotation, 1)); Assert.True(session.Update(new(80, 0)));
        Assert.Equal(100, region.LineStart.DistanceTo(region.LineEnd), 6);
        Assert.True(Math.Abs(region.LineEnd.Y - region.LineStart.Y) > 99);
        session.End(cancel: true);
        Assert.Equal(new Point2D(0, 0), region.LineStart); Assert.Equal(new Point2D(100, 0), region.LineEnd);
        if (region is Vision.Tools.Halcon.Measurement.FindLineParameters hp) { Assert.Equal(2, hp.CaliperHalfWidth); Assert.Equal(9, hp.CaliperCount); }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BlobFeatureRowsAreImmutableSnapshots_AndMissingValuesAreNotInvented(bool halcon)
    {
        var values = new Dictionary<string, double> { ["width"] = 4, ["circularity"] = 0.7 };
        ToolResult result = halcon
            ? new Vision.Tools.Halcon.Inspection.BlobResult { Items = [new() { Index = 1, FeatureValues = values }] }
            : new Vision.Tools.OpenCv.Inspection.BlobResult { Items = [new() { Index = 1, FeatureValues = values }] };
        using (result)
        {
            var row = Assert.Single(Vision.Controls.Presentation.VisionDisplay.ResultItems(result));
            values["width"] = 999;
            Assert.Equal(4, row.FeatureValues["width"]); Assert.Equal(0.7, row.FeatureValues["circularity"]);
            Assert.False(row.FeatureValues.ContainsKey("phi"));
            Assert.Throws<NotSupportedException>(() => ((IDictionary<string, double>)row.FeatureValues).Add("height", 0));
        }
    }
    private static void WithPage(Action<VisionToolEditorView, VisionBrowserViewModel> action)
    {
        using var workspace = new VisionBrowserWorkspace(new([new("TestBackend/Blob", "TestBackend", () => (new Probe(), new Vision.Tools.OpenCv.Inspection.BlobParameters(), null))], [],
            _ => throw new InvalidOperationException(), () => VisionImage.FromBuffer(320, 240, Vision.Core.Imaging.PixelFormat.Mono8, new byte[320 * 240])));
        using var browser = new VisionBrowserViewModel(workspace); browser.LoadImage();
        var page = new VisionToolEditorView { Session = browser };
        var window = new Window { Content = page, Width = 1300, Height = 850, Left = -10000, Top = -10000, ShowInTaskbar = false };
        window.Show(); Drain(); try { action(page, browser); } finally { window.Close(); Drain(); }
    }
    private sealed class Probe : IExecutableTool
    {
        public string Name => "Blob"; public long LastElapsedMs => 0;
        public Vision.Core.Results.ValidationResult Validate() => new();
        public ToolResult Run(VisionImage? image) => new Vision.Tools.OpenCv.Inspection.BlobResult { Items = [new() { Index = 1, Area = 20, Center = new(3, 4), Bounds = new(1, 2, 4, 5), FeatureValues = new Dictionary<string, double> { ["width"] = 4, ["circularity"] = 0.7 } }] };
        public void EnsureLoaded() { } public void Dispose() { }
    }
    private static void Drain() => Application.Current.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { var child = VisualTreeHelper.GetChild(root, i); yield return child; foreach (var next in Descendants(child)) yield return next; }
    }
}
