using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Vision.Controls.Presentation;
using Vision.Controls.Services;
using Vision.Controls.View;
using Vision.Controls.ViewModels;
using Vision.Core.Imaging;
using ImageFormat = Vision.Core.Imaging.PixelFormat;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
public sealed class ResultSelectionInteractionTests(WorkbenchAppFixture host)
{
    [Fact]
    public void SortedTableAndCanvas_ShareSelectionById_AndPageReloadDoesNotReleaseResults()
    {
        host.Invoke(() =>
        {
            using var workspace = new VisionBrowserWorkspace(new(
                [new("Regions", "Test", () => (new ResultSelectionTests.ResultTool(ResultSelectionTests.CreateResult), new object(), null))], [],
                _ => throw new InvalidOperationException(), BlackImage));
            using var browser = new VisionBrowserViewModel(workspace);
            var page = new VisionToolEditorView { Session = browser };
            var window = new Window { Content = page, Width = 1300, Height = 850, Left = -10000, Top = -10000, ShowInTaskbar = false };
            window.Show();
            try
            {
                browser.LoadImage();
                browser.CurrentCard!.Run();
                Drain();
                var resultsTab = Assert.IsType<TabItem>(page.DetailsPanel.FindName("ResultsTab"));
                resultsTab.IsSelected = true;
                Drain();
                Assert.True(resultsTab.IsSelected);
                var grid = Assert.Single(Descendants(page).OfType<DataGrid>());
                var viewport = Assert.Single(Descendants(page).OfType<VisionImageViewport>());
                var results = browser.CurrentCard.Results;
                Assert.Same(results, viewport.Results);
                var view = CollectionViewSource.GetDefaultView(grid.ItemsSource);
                view.SortDescriptions.Add(new SortDescription(nameof(VisionResultItem.Area), ListSortDirection.Ascending));
                Drain();
                Assert.Equal(7, Assert.IsType<VisionResultItem>(grid.Items[0]).Id);
                grid.SelectedItem = grid.Items[0];
                Drain();
                Assert.Equal(7, results.SelectedId);
                var roi = browser.RoiEditor.Selected;
                Assert.True(viewport.SelectResultAt(new(15, 30), 0.1));
                Drain();
                Assert.Equal(9, Assert.IsType<VisionResultItem>(grid.SelectedItem).Id);
                Assert.Equal(2, grid.SelectedIndex); // 排序后的索引不是算法 ID。
                Assert.Same(roi, browser.RoiEditor.Selected);
                Assert.False(browser.RoiEditor.IsManipulating);
                grid.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(grid)!, 0, Key.Escape)
                    { RoutedEvent = Keyboard.PreviewKeyDownEvent });
                Drain();
                Assert.Null(results.SelectedId);
                Assert.True(viewport.SelectResultAt(new(15, 30), 0.1));
                Drain();
                window.Content = null;
                Drain();
                Assert.Equal(9, results.SelectedId);
                Assert.Equal(3, results.Items.Count);
                window.Content = page;
                Drain();
                var reloaded = Assert.Single(Descendants(page).OfType<DataGrid>());
                Assert.Equal(9, Assert.IsType<VisionResultItem>(reloaded.SelectedItem).Id);
                browser.ParametersChanged();
                Drain();
                Assert.Null(results.SelectedId);
                Assert.Empty(results.Items);
                Assert.Null(viewport.Result);
            }
            finally { window.Close(); Drain(); }
        });
    }

    [Fact]
    public void TrueContourAndHole_RenderWithoutBoundsOrCenters_SelectionChangesColor_AndModesAreSeparate()
    {
        host.Invoke(() =>
        {
            using var card = new ToolCardViewModel(new ResultSelectionTests.ResultTool(ResultSelectionTests.CreateResult));
            card.Run();
            var editor = new RoiEditorViewModel();
            var roi = editor.AddCircle(75, 75, 10);
            var viewport = new VisionImageViewport
            {
                Image = BlackImage(), Result = card.LastResult, Results = card.Results,
                Editor = editor, Mode = "结果选择", ShowOutput = false
            };
            viewport.Measure(new Size(100, 100));
            viewport.Arrange(new Rect(0, 0, 100, 100));
            viewport.UpdateLayout();
            viewport.ActualPixels();
            var regular = PixelAt(viewport, 30, 10);
            Assert.True(regular.G > regular.R);
            Assert.True(PixelAt(viewport, 20, 30).G > 0); // 内孔边界同样显示。
            Assert.Equal(Colors.Black, PixelAt(viewport, 30, 30)); // 默认无质心十字。
            Assert.Equal(Colors.Black, PixelAt(viewport, 90, 40)); // L 形外接框角不是轮廓。
            Assert.True(viewport.SelectResultAt(new(15, 30), 0.1));
            viewport.InvalidateVisual(); // 此离屏视口未 Loaded，没有安装展示订阅。
            var selected = PixelAt(viewport, 30, 10);
            Assert.True(selected.R > selected.G);
            Assert.Same(roi, editor.Selected);
            Assert.False(editor.IsManipulating);
            viewport.Mode = "选择";
            Assert.False(viewport.SelectResultAt(new(65, 35), 0.1));
            Assert.Equal(9, card.Results.SelectedId);
            viewport.Mode = "结果选择";
            viewport.ShowResults = false;
            Assert.False(viewport.SelectResultAt(new(65, 35), 0.1));
            Assert.Equal(9, card.Results.SelectedId);
            Assert.Equal(Colors.Black, PixelAt(viewport, 30, 10));
            viewport.ShowResults = true;
            viewport.ShowResultCenters = true;
            Assert.True(PixelAt(viewport, 30, 30).R > 0);
        });
    }

    private static VisionImage BlackImage() => VisionImage.FromBuffer(100, 100, ImageFormat.Mono8, new byte[10000]);
    private static Color PixelAt(FrameworkElement view, int x, int y)
    {
        view.UpdateLayout();
        var bitmap = new RenderTargetBitmap(100, 100, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(view);
        var pixel = new byte[4];
        bitmap.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0);
        return Color.FromArgb(pixel[3], pixel[2], pixel[1], pixel[0]);
    }
    private static void Drain() => Application.Current.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
