using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Vision.Controls.Presentation;
using Vision.Controls.Services;
using Vision.Controls.View;
using Vision.Controls.ViewModels;
using Vision.Core.Imaging;

using Xunit;
using ImageFormat = Vision.Core.Imaging.PixelFormat;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
public sealed class BlobResultFeatureColumnsTests(WorkbenchAppFixture host)
{
    [Fact]
    public void RealOpenCvBlob_ResultTable_ShowsCheckedFeatureColumnsWithMeasuredValues()
    {
        host.Invoke(() =>
        {
            using var workspace = new VisionBrowserWorkspace(new(OpenCvToolCatalog.Entries, [],
                _ => throw new InvalidOperationException(), BlobImage));
            using var browser = new VisionBrowserViewModel(workspace);
            var page = new VisionToolEditorView { Session = browser };
            var window = new Window { Content = page, Width = 1300, Height = 850, Left = -10000, Top = -10000, ShowInTaskbar = false };
            window.Show(); Drain();
            try
            {
                browser.SelectedEntry = browser.ToolEntries.Single(entry => entry.Name == "OpenCV/Blob");
                browser.LoadImage(); Drain();
                var editor = Assert.IsType<BlobParametersEditorViewModel>(browser.BlobEditor);
                editor.SelectedFeatures.Add("width");
                editor.SelectedFeatures.Add("circularity");
                Drain();
                browser.CurrentCard!.Run(); Drain();
                Assert.True(browser.CurrentCard.LastResult!.Success, browser.CurrentCard.LastResult.Message);

                ((TabItem)page.DetailsPanel.FindName("ResultsTab")).IsSelected = true; Drain();
                var grid = Assert.Single(Descendants(page.DetailsPanel).OfType<DataGrid>(), grid => AutomationProperties.GetName(grid) == "视觉结果表格");
                Assert.IsAssignableFrom<WPFCustomControls.Controls.DataGridView>(grid);
                var defaultGrid = new WPFCustomControls.Controls.DataGridView();
                Assert.Equal(DataGridSelectionMode.Extended, defaultGrid.SelectionMode);
                Assert.Equal(VirtualizationMode.Standard, VirtualizingPanel.GetVirtualizationMode(defaultGrid));
                Assert.Equal(VirtualizationMode.Recycling, VirtualizingPanel.GetVirtualizationMode(grid));
                Assert.False(grid.AutoGenerateColumns); Assert.True(grid.IsReadOnly);
                Assert.Equal(DataGridSelectionMode.Single, grid.SelectionMode);
                Assert.Equal(1, grid.FrozenColumnCount);
                var headers = grid.Columns.Select(column => column.Header?.ToString()).ToArray();
                Assert.Contains("外接宽(px)", headers); Assert.Contains("circularity", headers);
                var widthColumn = grid.Columns.Single(column => Equals(column.Header, "外接宽(px)"));
                var cell = Assert.IsType<TextBlock>(widthColumn.GetCellContent(grid.Items[0]));
                Assert.True(double.TryParse(cell.Text, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var width), cell.Text);
                Assert.Equal(10, width, 3);
                grid.SelectedItem = grid.Items[1]; Drain();
                // 真实OpenCV + 合成像素的离线预览，不是用户图片或物理鼠标验收。
                var preview = new System.Windows.Media.Imaging.RenderTargetBitmap((int)page.ActualWidth, (int)page.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                preview.Render(page);
                var png = new System.Windows.Media.Imaging.PngBitmapEncoder();
                png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(preview));
                using var file = System.IO.File.Create(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "vision-blob-feature-results.png"));
                png.Save(file);
                var columnCount = grid.Columns.Count;
                window.Content = null; Drain();
                editor.SelectedFeatures.Add("rectangularity"); Drain();
                Assert.Equal(columnCount, grid.Columns.Count); // 卸载期间不观察借用集合。
                window.Content = page; Drain();
                var reloaded = Assert.Single(Descendants(page.DetailsPanel).OfType<WPFCustomControls.Controls.DataGridView>(), g => AutomationProperties.GetName(g) == "视觉结果表格");
                Assert.Single(reloaded.Columns, column => Equals(column.Header, "rectangularity"));
            }
            finally { window.Close(); Drain(); }
        });
    }

    [Fact]
    public void CheckedFeatures_AreVisibleBeforeExecution_AndEditingTheRangeWritesBackToTheOriginalDto()
    {
        host.Invoke(() =>
        {
            using var workspace = new VisionBrowserWorkspace(new(OpenCvToolCatalog.Entries, [],
                _ => throw new InvalidOperationException(), BlobImage));
            using var browser = new VisionBrowserViewModel(workspace);
            var page = new VisionToolEditorView { Session = browser };
            var window = new Window { Content = page, Width = 1300, Height = 850, Left = -10000, Top = -10000, ShowInTaskbar = false };
            window.Show(); Drain();
            try
            {
                browser.SelectedEntry = browser.ToolEntries.Single(entry => entry.Name == "OpenCV/Blob");
                browser.LoadImage(); Drain();
                var editor = Assert.IsType<BlobParametersEditorViewModel>(browser.BlobEditor);
                editor.SelectedFeatures.Add("width"); editor.SelectedFeatures.Add("circularity");
                Drain();
                ((TabItem)page.DetailsPanel.FindName("ResultsTab")).IsSelected = true; Drain();

                // 未执行时结果表就显示所选特征；结果页不再复制参数范围编辑器。
                var results = Assert.Single(Descendants(page.DetailsPanel).OfType<DataGrid>(), g => AutomationProperties.GetName(g) == "视觉结果表格");
                Assert.Empty(results.Items);
                Assert.Contains(results.Columns, column => Equals(column.Header, "外接宽(px)"));
                Assert.Contains(results.Columns, column => Equals(column.Header, "circularity"));
                Assert.Empty(Descendants(page.DetailsPanel).OfType<BlobFeatureFilterView>());
                ((TabItem)page.DetailsPanel.FindName("ParametersTab")).IsSelected = true; Drain();
                var panel = Assert.IsAssignableFrom<VisionPropertyPanel>(page.DetailsPanel.FindName("ParametersPanel"));
                Assert.Same(editor, panel.TargetObject);
                var ranges = Assert.Single(Descendants(panel).OfType<DataGrid>());
                Assert.Equal(new[] { "特征", "下限", "上限" }, ranges.Columns.Select(c => c.Header?.ToString()));
                Assert.Same(editor.Ranges, ranges.ItemsSource);
                Assert.Equal(new[] { "width", "circularity" }, ranges.Items.Cast<BlobFeatureRow>().Select(r => r.Feature));
                Assert.True(ranges.IsVisible);

                // 从当前参数页的真实单元格写回原DTO，而非编辑已移除的结果页控件。
                var parameters = Assert.IsType<Vision.Tools.OpenCv.Inspection.BlobParameters>(browser.CurrentParameters);
                var minColumn = ranges.Columns.Single(c => Equals(c.Header, "下限"));
                var source = Assert.IsType<BlobFeatureRow>(ranges.Items[0]);
                Assert.Same(editor.Ranges[0], source);
                ranges.CurrentCell = new DataGridCellInfo(source, minColumn);
                Assert.True(ranges.BeginEdit()); Drain();
                var box = Assert.Single(Descendants(ranges).OfType<TextBox>());
                box.Text = "12"; box.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
                Assert.True(ranges.CommitEdit(DataGridEditingUnit.Cell, true)); Drain();
                Assert.Equal(12, Assert.Single(parameters.Features, f => f.Feature == "width").Min);
                Assert.Equal(12, editor.Ranges.Single(r => r.Feature == "width").Min);

                // 独立兼容控件仍保留实测摘要契约，但不为测试把它恢复到产品结果页。
                var filter = new BlobFeatureFilterView { Editor = editor, Items = browser.CurrentCard!.Results.Items };
                var summaryWindow = new Window { Content = filter, Width = 600, Height = 350, Left = -10000, Top = -10000, ShowInTaskbar = false };
                try
                {
                    summaryWindow.Show(); Drain();
                    var summary = Assert.Single(Descendants(filter).OfType<DataGrid>());
                    Assert.Equal(new[] { "特征", "下限", "上限", "本次最小", "本次最大" }, summary.Columns.Select(c => c.Header?.ToString()));
                    Assert.Equal(new[] { "width", "circularity" }, summary.Items.Cast<BlobFeatureFilterRow>().Select(r => r.Feature));
                    var row = Assert.IsType<BlobFeatureFilterRow>(summary.Items[0]);
                    Assert.Equal(source.Min, row.Min);
                    // 宽为10，先前设置下限12应筛空，不能伪造仍有实测值。
                    browser.CurrentCard.Run(); Drain();
                    Assert.True(browser.CurrentCard.LastResult!.Success);
                    Assert.Empty(browser.CurrentCard.Results.Items);
                    Assert.Null(row.ObservedMin); Assert.Null(row.ObservedMax);
                    // 放宽同一范围后再执行，原DTO、参数行与实测摘要都必须更新。
                    row.Min = 8;
                    Assert.Equal(8, source.Min);
                    Assert.Equal(8, Assert.Single(parameters.Features, f => f.Feature == "width").Min);
                    browser.CurrentCard.Run(); Drain();
                    Assert.Single(browser.CurrentCard.Results.Items);
                    Assert.Equal(10, row.ObservedMin);
                    Assert.Equal(10, row.ObservedMax);
                    var observedColumn = summary.Columns.Single(c => Equals(c.Header, "本次最小"));
                    summary.ScrollIntoView(summary.Items[0], observedColumn); Drain();
                    var observed = Assert.IsType<TextBlock>(observedColumn.GetCellContent(summary.Items[0]));
                    Assert.Equal("10", observed.Text);
                }
                finally { summaryWindow.Close(); Drain(); }
            }
            finally { window.Close(); Drain(); }
        });
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FilterReload_RebindsSameEditor_DetachesOldRows_AndDoesNotRoundUneditedRanges(bool halcon)
    {
        host.Invoke(() =>
        {
            var parameters = halcon ? (object)new Vision.Tools.Halcon.Inspection.BlobParameters() : new Vision.Tools.OpenCv.Inspection.BlobParameters();
            using var editor = BlobParametersEditorViewModel.TryCreate(parameters, () => { })!;
            editor.SelectedFeatures.Add("width");
            editor.Ranges[0].Min = 0.123456789;
            var filter = new BlobFeatureFilterView { Editor = editor };
            var window = new Window { Content = filter, Width = 340, Height = 400, Left = -10000, Top = -10000, ShowInTaskbar = false };
            window.Show(); Drain();
            try
            {
                var row = Assert.Single(filter.Rows);
                var grid = Assert.IsType<DataGrid>(filter.FindName("FeatureGrid"));
                Assert.True(grid.Columns.All(c => c.ActualWidth > 20));
                grid.CurrentCell = new DataGridCellInfo(row, grid.Columns[1]);
                Assert.True(grid.BeginEdit()); Drain();
                Assert.Equal("0.123456789", Assert.Single(Descendants(grid).OfType<TextBox>()).Text);
                grid.CommitEdit(DataGridEditingUnit.Cell, true); Drain();
                Assert.Equal(0.123456789, editor.Ranges[0].Min);
                window.Content = null; Drain();
                Assert.Empty(filter.Rows);
                editor.SelectedFeatures.Add("height");
                Assert.Empty(filter.Rows);
                Assert.Throws<ObjectDisposedException>(() => row.Min = 999);
                window.Content = filter; Drain();
                Assert.Same(editor, filter.Editor);
                Assert.Equal(new[] { "width", "height" }, filter.Rows.Select(r => r.Feature));
                editor.Ranges[0].Min = 5;
                Assert.Equal(5, filter.Rows[0].Min);
                editor.SelectedFeatures.Remove("height");
                Assert.Single(filter.Rows);
            }
            finally { window.Close(); Drain(); }
        });
    }
    [Fact]
    public void EveryBlobShowsItsOwnAreaAndBoundingSize_BeforeCoordinateColumns_AndSelectionMatchesCanvas()
    {
        host.Invoke(() =>
        {
            using var workspace = new VisionBrowserWorkspace(new(OpenCvToolCatalog.Entries, [],
                _ => throw new InvalidOperationException(), BlobImage));
            using var browser = new VisionBrowserViewModel(workspace);
            browser.SelectedEntry = browser.ToolEntries.Single(entry => entry.Name == "OpenCV/Blob");
            browser.LoadImage(); browser.BlobEditor!.SelectedFeatures.Add("circularity"); browser.CurrentCard!.Run();
            var page = new VisionToolEditorView { Session = browser };
            var window = new Window { Content = page, Width = 1300, Height = 850, Left = -10000, Top = -10000, ShowInTaskbar = false };
            window.Show(); Drain();
            try
            {
                ((TabItem)page.DetailsPanel.FindName("ResultsTab")).IsSelected = true; Drain();
                var grid = Assert.Single(Descendants(page.DetailsPanel).OfType<DataGrid>(), g => AutomationProperties.GetName(g) == "视觉结果表格");
                Assert.Equal(new[] { "ID", "面积(px²)", "外接宽(px)", "外接高(px)" }, grid.Columns.OrderBy(c => c.DisplayIndex).Take(4).Select(c => c.Header));
                Assert.Equal("circularity", grid.Columns.Single(c => c.DisplayIndex == 4).Header);
                var rows = browser.CurrentCard.Results.Items;
                Assert.Equal(new[] { 100.0, 24.0 }, rows.Select(r => r.Area!.Value));
                for (var i = 0; i < rows.Count; i++)
                {
                    var expected = i == 0 ? new[] { "100", "10", "10" } : new[] { "24", "6", "4" };
                    for (var column = 1; column <= 3; column++)
                        Assert.Equal(expected[column - 1], Assert.IsType<TextBlock>(grid.Columns[column].GetCellContent(rows[i])).Text);
                }
                Assert.Empty(Descendants(page.DetailsPanel).OfType<BlobFeatureFilterView>()); // 范围只在参数页编辑，结果页不重复。
                grid.SelectedItem = rows[1]; Drain();
                Assert.Equal(rows[1].Id, browser.CurrentCard.Results.SelectedId);
                var detail = Assert.Single(Descendants(page.DetailsPanel).OfType<StackPanel>(), panel => panel.Name == "SelectedBlobMeasurements");
                Assert.Same(rows[1], detail.DataContext);
                Assert.Contains(Descendants(detail).OfType<TextBlock>(), label => label.Text == "像素面积：24 px²");
                Assert.True(page.ImagePanel.ImageViewport.SelectResultAt(rows[0].Center, 0.1)); Drain();
                Assert.Same(rows[0], grid.SelectedItem);
                Assert.Same(rows[0], detail.DataContext);
                var missing = new VisionResultItem(99, null, default, default, []);
                Assert.Null(missing.BoundingWidth); Assert.Null(missing.BoundingHeight);
            }
            finally { window.Close(); Drain(); }
        });
    }
    private static VisionImage BlobImage()
    {
        var pixels = new byte[40 * 40];
        for (var y = 10; y < 20; y++) for (var x = 10; x < 20; x++) pixels[y * 40 + x] = 255;
        for (var y = 26; y < 30; y++) for (var x = 26; x < 32; x++) pixels[y * 40 + x] = 255;
        return VisionImage.FromBuffer(40, 40, ImageFormat.Mono8, pixels);
    }
    private static void Drain() => Application.Current.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var next in Descendants(child)) yield return next;
        }
    }
}
