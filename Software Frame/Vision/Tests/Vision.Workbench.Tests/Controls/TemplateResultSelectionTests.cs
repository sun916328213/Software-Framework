using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Vision.Controls.Presentation;
using Vision.Controls.Services;
using Vision.Controls.View;
using Vision.Controls.ViewModels;
using Vision.Core.Imaging;
using Vision.Core.Results;
using Cv = Vision.Tools.OpenCv.Matching;
using Halcon = Vision.Tools.Halcon.Matching;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
[Trait("Category", "Regression")]
public sealed class TemplateResultSelectionTests(WorkbenchAppFixture host)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MatchRows_UseRealRectangleOrCenter_AndRejectPreviousExecutionRows(bool halcon)
    {
        using var card = new ToolCardViewModel(new ResultSelectionTests.ResultTool(() => CreateResult(halcon)));
        card.Run(); var results = card.Results;
        Assert.Equal(2, results.Items.Count);
        Assert.True(results.SelectAt(new(20, 20), 0.1)); Assert.Equal(1, results.SelectedId);
        Assert.Equal(!halcon, results.SelectAt(new(11, 11), 0.1));
        var old = results.Items[0];
        card.Run(); results.SelectedItem = old; Assert.Null(results.SelectedId);
        results.SelectId(2); card.ClearResult(); Assert.Null(results.SelectedId); Assert.Empty(results.Items);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MatchRows_AreScalarGeometrySnapshots_WithActualPoseAndWithoutInventingArea(bool halcon)
    {
        using var source = CreateResult(halcon);
        using var card = new ToolCardViewModel(new ResultSelectionTests.ResultTool(() => source));
        card.Run(); var row = card.Results.Items[0];
        Assert.True(card.Results.HasMatchItems); Assert.Null(row.Area); Assert.Empty(row.Contours);
        Assert.Equal(0.9, row.Score);
        if (halcon)
        {
            Assert.Equal(30, row.AngleDeg); Assert.Equal(1.2, row.Scale);
            Assert.Equal(VisionResultGeometryKind.MatchCenter, row.GeometryKind);
            var item = ((Halcon.ShapeMatchResult)source).Matches[0];
            item.Center = new(99, 99); item.Score = 0; item.AngleDeg = 0; item.Scale = 0;
            Assert.Equal(30, row.AngleDeg); Assert.Equal(1.2, row.Scale);
        }
        else
        {
            Assert.Equal(0, row.AngleDeg); Assert.Equal(1, row.Scale);
            Assert.Equal(VisionResultGeometryKind.TemplateRectangle, row.GeometryKind);
            var item = ((Cv.TemplateMatchResult)source).Matches[0];
            item.Center = new(99, 99); item.Score = 0; item.Bounds = new(90, 90, 5, 5);
            Assert.Equal(10, row.Bounds.X);
        }
        Assert.Equal(20, row.X); Assert.Equal(20, row.Y); Assert.Equal(0.9, row.Score);
        Assert.True(row.HitTest(new(20, 20), 0.1)); Assert.False(row.HitTest(new(99, 99), 0.1));
        Assert.False(row.HitTest(new(double.NaN, 20), 1)); Assert.False(row.HitTest(new(20, 20), -1));
        Assert.False(row.HitTest(new(20, 20), double.NaN));
        card.ClearResult(); Assert.False(card.Results.HasMatchItems);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MatchTable_SortsByScore_AndSharesSelectionWithCanvasAcrossReload(bool halcon)
    {
        host.Invoke(() =>
        {
            using var workspace = new VisionBrowserWorkspace(new(
                [new("Any name", "Any backend", () => (new ResultSelectionTests.ResultTool(() => CreateResult(halcon)), new object(), null))], [],
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
                Assert.Contains(grid.Columns, column => Equals(column.Header, "分数"));
                Assert.DoesNotContain(grid.Columns, column => Equals(column.Header, "面积"));
                var viewport = Assert.Single(Descendants(page).OfType<VisionImageViewport>());
                viewport.Mode = "结果选择";
                var rows = browser.CurrentCard.Results;
                var collection = CollectionViewSource.GetDefaultView(grid.ItemsSource);
                collection.SortDescriptions.Add(new SortDescription("Score", ListSortDirection.Ascending)); Drain();
                grid.SelectedItem = grid.Items[0]; Drain();
                Assert.Equal(2, rows.SelectedId);
                Assert.True(viewport.SelectResultAt(new(20, 20), 0.1)); Drain();
                Assert.Equal(1, Assert.IsType<VisionResultItem>(grid.SelectedItem).Id);
                Assert.Equal(1, grid.SelectedIndex);
                Assert.False(browser.RoiEditor.IsManipulating);
                window.Content = null; Drain(); window.Content = page; Drain();
                grid = Assert.Single(Descendants(page).OfType<DataGrid>());
                Assert.Equal(1, Assert.IsType<VisionResultItem>(grid.SelectedItem).Id);
                browser.ParametersChanged(); Drain(); Assert.Empty(rows.Items); Assert.Null(viewport.Result);
            }
            finally { window.Close(); Drain(); }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MatchGeometry_HighlightsSelectedItem_WithoutInventingHalconModelOutline(bool halcon)
    {
        host.Invoke(() =>
        {
            using var card = new ToolCardViewModel(new ResultSelectionTests.ResultTool(() => CreateResult(halcon)));
            card.Run();
            var viewport = new VisionImageViewport { Image = BlackImage(), Result = card.LastResult, Results = card.Results, ShowOutput = false, Mode = "结果选择" };
            viewport.Measure(new Size(100, 100)); viewport.Arrange(new Rect(0, 0, 100, 100)); viewport.ActualPixels();
            var regular = PixelAt(viewport, 20, 20); Assert.True(regular.G > regular.R);
            Assert.True(viewport.SelectResultAt(new(20, 20), 0.1)); viewport.InvalidateVisual();
            var selected = PixelAt(viewport, 20, 20); Assert.True(selected.R > selected.G);
            var corner = PixelAt(viewport, 10, 20);
            if (halcon) Assert.Equal(Colors.Black, corner); else Assert.True(corner.R > corner.G);
            viewport.ShowResults = false; Assert.Equal(Colors.Black, PixelAt(viewport, 20, 20));
            Assert.False(viewport.SelectResultAt(new(20, 20), 0.1)); Assert.Equal(1, card.Results.SelectedId);
        });
    }

    [Fact]
    public void PoseRectangle_UsesActualFourCornersForSelectionAndRendering_NotAxisAlignedBounds()
    {
        host.Invoke(() =>
        {
            var result = new Cv.TemplateMatchResult
            {
                Matches = [new()
                {
                    Center = new(20, 20), Bounds = new(10, 10, 20, 20), AngleDeg = 45, Scale = 1.25, Score = 0.9,
                    Corners = [new(20, 10), new(30, 20), new(20, 30), new(10, 20)]
                }]
            };
            using var card = new ToolCardViewModel(new ResultSelectionTests.ResultTool(() => result));
            card.Run(); var row = Assert.Single(card.Results.Items);
            Assert.True(row.HitTest(new(20, 11), 0)); Assert.False(row.HitTest(new(10, 10), 0.1));
            Assert.Equal(45, row.AngleDeg); Assert.Equal(1.25, row.Scale);
            result.Matches[0].Corners = [];
            Assert.Equal(4, Assert.Single(row.Contours).Points.Count);
            var viewport = new VisionImageViewport { Image = BlackImage(), Result = result, Results = card.Results, ShowOutput = false, Mode = "结果选择" };
            // 恢复同一原结果的四角供画布创建自己的快照，表格旧快照不受影响。
            result.Matches[0].Corners = row.Contours[0].Points;
            viewport.Result = null; viewport.Result = result;
            viewport.Measure(new Size(100, 100)); viewport.Arrange(new Rect(0, 0, 100, 100)); viewport.ActualPixels();
            Assert.Equal(Colors.Black, PixelAt(viewport, 10, 10));
            var edge = PixelAt(viewport, 20, 10); Assert.True(edge.G > edge.R);
        });
    }

    private static ToolResult CreateResult(bool halcon) => halcon
        ? new Halcon.ShapeMatchResult { Matches = [new() { Center = new(20, 20), Score = 0.9, AngleDeg = 30, Scale = 1.2 }, new() { Center = new(70, 65), Score = 0.6, AngleDeg = -10, Scale = 0.8 }] }
        : new Cv.TemplateMatchResult { Matches = [new() { Center = new(20, 20), Score = 0.9, Bounds = new(10, 10, 20, 20) }, new() { Center = new(70, 65), Score = 0.6, Bounds = new(60, 55, 20, 20) }] };
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
}
