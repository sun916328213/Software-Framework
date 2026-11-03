using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Vision.Controls.Presentation;
using Vision.Controls.Services;
using Vision.Controls.View;
using Vision.Controls.ViewModels;
using Vision.Core.AI;
using Vision.Core.Geometry;
using Vision.Core.Imaging;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
[Trait("Category", "Regression")]
public sealed class DetectionResultSelectionTests(WorkbenchAppFixture host)
{
    [Fact]
    public void DetectionRows_AreSnapshots_WithoutInventingAreaContourOrPose()
    {
        using var source = CreateResult();
        using var card = new ToolCardViewModel(new ResultSelectionTests.ResultTool(() => source));
        card.Run();
        Assert.Equal(2, card.Results.Items.Count);
        var row = card.Results.Items[0];
        Assert.Equal(new Rect2D(10, 10, 30, 30), row.Bounds);
        Assert.Equal(new Point2D(25, 25), row.Center);
        Assert.Equal(0.9, row.Score);
        Assert.Null(row.Area); Assert.Null(row.AngleDeg); Assert.Null(row.Scale); Assert.Empty(row.Contours);
        Assert.Contains("检测框", row.GeometryDescription);
        Assert.False(card.Results.HasMatchItems); Assert.True(card.Results.HasDetectionItems);
        Assert.Equal("螺母", row.Label); Assert.Equal(7, row.ClassId);
        source.Boxes[0].Box = new(70, 70, 20, 20); source.Boxes[0].Score = 0;
        source.Boxes[0].Label = "changed"; source.Boxes[0].ClassId = 99;
        Assert.Equal(new Rect2D(10, 10, 30, 30), row.Bounds); Assert.Equal(0.9, row.Score);
        Assert.Equal("螺母", row.Label); Assert.Equal(7, row.ClassId);
    }

    [Fact]
    public void OverlappingBoxes_SelectTopmost_AndRejectRowsFromEarlierExecution()
    {
        using var card = new ToolCardViewModel(new ResultSelectionTests.ResultTool(CreateResult));
        card.Run();
        Assert.True(card.Results.SelectAt(new(25, 25), 0)); Assert.Equal(2, card.Results.SelectedId);
        Assert.True(card.Results.SelectAt(new(15, 15), 0)); Assert.Equal(1, card.Results.SelectedId);
        Assert.False(card.Results.SelectAt(new(90, 90), 0)); Assert.Null(card.Results.SelectedId);
        var old = card.Results.Items[0];
        card.Run(); card.Results.SelectedItem = old; Assert.Null(card.Results.SelectedId);
        card.Results.SelectId(2); card.ClearResult(); Assert.Empty(card.Results.Items); Assert.Null(card.Results.SelectedId);
    }

    [Theory]
    [InlineData(0, 0, 0, 10)]
    [InlineData(0, 0, -1, 10)]
    [InlineData(double.NaN, 0, 10, 10)]
    [InlineData(0, 0, double.PositiveInfinity, 10)]
    [InlineData(double.MaxValue, 0, double.MaxValue, 10)]
    public void InvalidBoxes_KeepScalarRows_ButCannotBePicked(double x, double y, double width, double height)
    {
        using var source = new AIDetectionResult { Boxes = [new() { Label = "invalid location", Score = 0.8, Box = new(x, y, width, height) }] };
        var row = Assert.Single(VisionDisplay.ResultItems(source));
        Assert.False(row.HitTest(new(5, 5), 1));
        Assert.False(row.HitTest(new(double.MaxValue, 5), 1));
        Assert.False(row.HasValidBounds); Assert.Contains("无有效位置", row.GeometryDescription);
        host.Invoke(() =>
        {
            var viewport = Viewport(source);
            Assert.Equal(Colors.Black, PixelAt(viewport, 5, 5));
        });
    }

    [Fact]
    public void EmptyAndFailedResults_ClearRowsSelectionAndDetectionPresentation()
    {
        var next = CreateResult();
        using var card = new ToolCardViewModel(new ResultSelectionTests.ResultTool(() => next));
        var notifications = new List<string?>();
        card.Results.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);
        card.Run(); card.Results.SelectId(1); Assert.True(card.Results.HasDetectionItems);
        next = new AIDetectionResult(); card.Run();
        Assert.Empty(card.Results.Items); Assert.Null(card.Results.SelectedId); Assert.False(card.Results.HasDetectionItems);
        next = CreateResult(); card.Run(); card.Results.SelectId(2);
        next = CreateResult(); next.Success = false; card.Run();
        Assert.Empty(card.Results.Items); Assert.Null(card.Results.SelectedId); Assert.False(card.Results.HasDetectionItems);
        Assert.Contains(nameof(ResultSelectionViewModel.HasDetectionItems), notifications);
    }

    [Fact]
    public void DetectionTable_SortsAndSharesSelection_AcrossReloadAndInputChange()
    {
        host.Invoke(() =>
        {
            using var workspace = new VisionBrowserWorkspace(new(
                [new("Any name", "Any backend", () => (new ResultSelectionTests.ResultTool(CreateResult), new object(), null))], [],
                _ => throw new InvalidOperationException(), BlackImage));
            using var browser = new VisionBrowserViewModel(workspace);
            var page = new VisionToolEditorView { Session = browser };
            var window = new Window { Content = page, Width = 1300, Height = 850, Left = -10000, Top = -10000, ShowInTaskbar = false };
            window.Show();
            try
            {
                browser.LoadImage(); browser.CurrentCard!.Run();
                ((TabItem)page.DetailsPanel.FindName("ResultsTab")).IsSelected = true; Drain();
                var grid = Assert.Single(Descendants(page).OfType<DataGrid>());
                Assert.Equal("AI检测结果表格", AutomationProperties.GetName(grid));
                Assert.True(grid.IsReadOnly); Assert.False(grid.CanUserAddRows); Assert.True(grid.EnableRowVirtualization);
                Assert.Contains(grid.Columns, column => Equals(column.Header, "类别"));
                Assert.Contains(grid.Columns, column => Equals(column.Header, "类别编号"));
                Assert.DoesNotContain(grid.Columns, column => Equals(column.Header, "面积") || Equals(column.Header, "角度（度）"));
                var viewport = Assert.Single(Descendants(page).OfType<VisionImageViewport>());
                viewport.Mode = "结果选择";
                var rows = browser.CurrentCard.Results;
                CollectionViewSource.GetDefaultView(grid.ItemsSource).SortDescriptions.Add(new("Score", ListSortDirection.Ascending)); Drain();
                grid.SelectedItem = grid.Items[0]; Drain(); Assert.Equal(2, rows.SelectedId);
                Assert.True(viewport.SelectResultAt(new(15, 15), 0)); Drain();
                Assert.Equal(1, Assert.IsType<VisionResultItem>(grid.SelectedItem).Id); Assert.Equal(1, grid.SelectedIndex);
                Assert.False(browser.RoiEditor.IsManipulating);
                window.Content = null; Drain(); window.Content = page; Drain();
                grid = Assert.Single(Descendants(page).OfType<DataGrid>());
                Assert.Equal(1, Assert.IsType<VisionResultItem>(grid.SelectedItem).Id);
                browser.LoadImage(); Drain();
                Assert.Empty(rows.Items); Assert.Null(rows.SelectedId); Assert.False(rows.HasDetectionItems); Assert.Null(viewport.Result);
            }
            finally { window.Close(); Drain(); }
        });
    }

    [Fact]
    public void DetectionGeometry_UsesStableSnapshotAndSelectedColor_AndHonorsHiddenLayer()
    {
        host.Invoke(() =>
        {
            using var source = CreateResult();
            using var card = new ToolCardViewModel(new ResultSelectionTests.ResultTool(() => source));
            card.Run();
            var viewport = Viewport(source); viewport.Results = card.Results; viewport.Mode = "结果选择";
            var regular = PixelAt(viewport, 10, 25); Assert.True(regular.G > regular.R);
            Assert.True(viewport.SelectResultAt(new(15, 15), 0)); viewport.InvalidateVisual();
            var selected = PixelAt(viewport, 10, 25); Assert.True(selected.R > selected.G);
            source.Boxes[0].Box = new(70, 70, 20, 20); viewport.InvalidateVisual();
            Assert.Equal(selected, PixelAt(viewport, 10, 25)); Assert.Equal(Colors.Black, PixelAt(viewport, 70, 75));
            viewport.ShowResults = false; Assert.Equal(Colors.Black, PixelAt(viewport, 10, 25));
            Assert.False(viewport.SelectResultAt(new(15, 15), 0)); Assert.Equal(1, card.Results.SelectedId);
        });
    }

    private static VisionImageViewport Viewport(AIDetectionResult result)
    {
        var viewport = new VisionImageViewport { Image = BlackImage(), Result = result, ShowOutput = false };
        viewport.Measure(new Size(100, 100)); viewport.Arrange(new Rect(0, 0, 100, 100)); viewport.ActualPixels();
        return viewport;
    }
    private static VisionImage BlackImage() => VisionImage.FromBuffer(100, 100, Vision.Core.Imaging.PixelFormat.Mono8, new byte[10000]);
    private static Color PixelAt(FrameworkElement view, int x, int y)
    {
        view.UpdateLayout(); var bitmap = new RenderTargetBitmap(100, 100, 96, 96, PixelFormats.Pbgra32); bitmap.Render(view);
        var pixel = new byte[4]; bitmap.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0);
        return Color.FromArgb(pixel[3], pixel[2], pixel[1], pixel[0]);
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
    private static AIDetectionResult CreateResult() => new()
    {
        Boxes = [new() { Label = "螺母", ClassId = 7, Score = 0.9, Box = new(10, 10, 30, 30) },
            new() { Label = "螺钉", ClassId = 3, Score = 0.6, Box = new(20, 20, 30, 30) }]
    };
}
