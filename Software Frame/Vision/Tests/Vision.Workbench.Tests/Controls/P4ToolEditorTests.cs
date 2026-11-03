using System.ComponentModel;
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
using Vision.Core;
using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Results;
using ValidationResult = Vision.Core.Results.ValidationResult;
using Vision.Tools.Halcon.Calibration;


namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
public sealed class P4ToolEditorTests(WorkbenchAppFixture host)
{
    [Theory]
    [InlineData("HALCON/Barcode")]
    [InlineData("HALCON/DataCode")]
    [InlineData("HALCON/FindLine")]
    [InlineData("HALCON/NPointsCalibration")]
    [InlineData("OpenCV/FindLine")]
    [InlineData("OpenCV/FindCircle")]
    [InlineData("AI/Classification")]
    [InlineData("AI/Detection")]
    [InlineData("AI/Segmentation")]
    [InlineData("AI/AnomalyDetection")]
    public void CatalogTools_UseTheSameMetadataPanel_WithoutCreatingParallelViewsOrLoadingModels(string name)
    {
        host.Invoke(() =>
        {
            var entries = HalconToolCatalog.Entries.Concat(OpenCvToolCatalog.Entries).Concat(AIToolCatalog.Entries).Concat(CommonToolCatalog.Entries).ToArray();
            var providerCalls = 0;
            using var workspace = new VisionBrowserWorkspace(new(entries, [],
                _ => { providerCalls++; throw new InvalidOperationException("不能隐式创建 Provider"); }, () => null));
            using var browser = new VisionBrowserViewModel(workspace);
            browser.SelectedEntry = entries.Single(e => e.Name == name);
            var page = new VisionToolEditorView { Session = browser };
            var window = Show(page);
            try
            {
                Assert.True(browser.UsesMetadataEditor);
                Assert.Null(browser.BlobEditor);
                var panel = Assert.Single(Descendants(page).OfType<VisionPropertyPanel>());
                Assert.Same(browser.CurrentParameters, panel.TargetObject);
                Assert.Empty(Descendants(page).OfType<Xceed.Wpf.Toolkit.PropertyGrid.PropertyGrid>());
                Assert.Null(browser.CurrentCard!.LastResult);
                Assert.Equal(0, providerCalls);
            }
            finally { window.Close(); Drain(); }
        });
    }

    [Fact]
    public void PointEditor_ReplacesComponentsInOriginalDto_WithoutOverwritingOtherCoordinate_AndClearsResult()
    {
        host.Invoke(() =>
        {
            var parameters = new Vision.Tools.Halcon.Measurement.FindLineParameters();
            using var workspace = Workspace(parameters);
            using var browser = new VisionBrowserViewModel(workspace);
            var page = new VisionToolEditorView { Session = browser };
            var window = Show(page);
            try
            {
                ((TabItem)page.DetailsPanel.FindName("RoiTab")).IsSelected = true; Drain();
                var coordinates = Descendants(page).OfType<DecimalUpDown>().Where(c =>
                    BindingOperations.GetBinding(c, DecimalUpDown.ValueProperty)?.Path?.Path == nameof(parameters.LineStart)).ToArray();
                Assert.Equal(2, coordinates.Length);
                browser.CurrentCard!.Run();
                Assert.NotNull(browser.CurrentCard.LastResult);
                coordinates[0].SetCurrentValue(DecimalUpDown.ValueProperty, -12.125m);
                Drain();
                Assert.Equal(new Point2D(-12.125, 100), parameters.LineStart);
                Assert.Null(browser.CurrentCard.LastResult);
                coordinates[1].SetCurrentValue(DecimalUpDown.ValueProperty, 23.375m);
                Drain();
                Assert.Equal(new Point2D(-12.125, 23.375), parameters.LineStart);
                Assert.Equal(new Point2D(200, 100), parameters.LineEnd);
            }
            finally { window.Close(); Drain(); }
        });
    }

    [Fact]
    public void CalibrationGrid_AddEditRemove_UseSameList_AndInvalidateResults_ButReloadDoesNot()
    {
        host.Invoke(() =>
        {
            var parameters = new NPointsCalibrationParameters();
            using var workspace = Workspace(parameters);
            using var browser = new VisionBrowserViewModel(workspace);
            var page = new VisionToolEditorView { Session = browser };
            var window = Show(page);
            try
            {
                var table = Assert.Single(Descendants(page).OfType<DataGrid>());
                Assert.Same(parameters.Points, table.ItemsSource);
                Assert.True(table.CanUserAddRows);
                Assert.True(table.CanUserDeleteRows);
                var editable = (IEditableCollectionView)table.Items;
                browser.CurrentCard!.Run();
                var added = Assert.IsType<CalibrationPoint>(editable.AddNew());
                editable.CommitNew();
                Drain();
                Assert.Same(added, Assert.Single(parameters.Points));
                Assert.Null(browser.CurrentCard.LastResult);
                Assert.Contains(table.Columns, c => Equals(c.Header, "像素 X（列）"));
                table.SelectedItem = added;
                var pixelColumn = table.Columns.Single(c => Equals(c.Header, "像素 X（列）"));
                table.CurrentCell = new DataGridCellInfo(added, pixelColumn);
                Assert.True(table.BeginEdit());
                Drain();
                var input = Descendants(table).OfType<TextBox>().Single(t =>
                    BindingOperations.GetBinding(t, TextBox.TextProperty)?.Path?.Path == nameof(CalibrationPoint.PixelX));
                browser.CurrentCard.Run();
                input.Text = "17";
                input.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
                table.CommitEdit(DataGridEditingUnit.Cell, true);
                table.CommitEdit(DataGridEditingUnit.Row, true);
                Drain();
                Assert.Equal(17, added.PixelX);
                Assert.Null(browser.CurrentCard.LastResult);
                browser.CurrentCard.Run();
                var result = browser.CurrentCard.LastResult;
                window.Content = null; Drain();
                window.Content = page; Drain();
                Assert.Same(result, browser.CurrentCard.LastResult);
                editable.Remove(added);
                Drain();
                Assert.Empty(parameters.Points);
                Assert.Null(browser.CurrentCard.LastResult);
            }
            finally { window.Close(); Drain(); }
        });
    }

    [Fact]
    public void ReadOnlyPoint_IsNotMadeEditableByTheNewCoordinateEditor()
    {
        host.Invoke(() =>
        {
            var panel = new VisionPropertyPanel { TargetObject = new ReadOnlyPoint(), ExpandGroups = true };
            var window = Show(panel);
            try { Assert.Empty(Descendants(panel).OfType<DecimalUpDown>()); }
            finally { window.Close(); Drain(); }
        });
    }

    [Fact]
    public void ResultsTab_UsesReadOnlyDomainColumns_AndDoesNotPretendToOfferBlobSelection()
    {
        host.Invoke(() =>
        {
            using var workspace = new VisionBrowserWorkspace(new(
                [new("Any", "Test", () => (new ClassificationResultTool(), new object(), null))], [],
                _ => throw new InvalidOperationException(), () => null));
            using var browser = new VisionBrowserViewModel(workspace);
            var page = new VisionToolEditorView { Session = browser };
            var window = Show(page);
            try
            {
                browser.CurrentCard!.Run();
                Assert.IsType<TabItem>(page.DetailsPanel.FindName("ResultsTab")).IsSelected = true;
                Drain();
                var grid = Assert.Single(Descendants(page).OfType<DataGrid>());
                Assert.Same(browser.CurrentCard.TableRows, grid.ItemsSource);
                Assert.True(grid.IsReadOnly);
                Assert.False(grid.CanUserAddRows);
                Assert.False(grid.CanUserDeleteRows);
                Assert.Contains(grid.Columns, c => Equals(c.Header, "类别"));
                grid.SelectedItem = grid.Items[0];
                Drain();
                Assert.Null(browser.CurrentCard.Results.SelectedId);
                var rows = browser.CurrentCard.TableRows;
                window.Content = null; Drain();
                window.Content = page; Drain();
                Assert.Same(rows, browser.CurrentCard.TableRows);
                browser.ParametersChanged();
                Drain();
                Assert.False(browser.CurrentCard.HasTabularResults);
                Assert.Empty(Descendants(page).OfType<DataGrid>());
            }
            finally { window.Close(); Drain(); }
        });
    }

    private sealed class ReadOnlyPoint
    {
        [ToolProperty(DisplayName = "只读点", EditorType = E_PropertyEditorType.Point)]
        public Point2D Point => new(7, 8);
    }
    private sealed class ClassificationResultTool : IExecutableTool
    {
        public string Name => "Result test";
        public long LastElapsedMs => 0;
        public ValidationResult Validate() => new();
        public ToolResult Run(VisionImage? image) => new Vision.Core.AI.AIClassificationResult
        { Success = true, TopLabel = "good", TopScore = 0.9, Scores = [new() { Label = "good", ClassId = 2, Score = 0.9 }] };
        public void EnsureLoaded() { }
        public void Dispose() { }
    }

    private static VisionBrowserWorkspace Workspace(object parameters) => new(new(
        [new("Arbitrary", "Test", () => (new FakeTool(), parameters, null))], [],
        _ => throw new InvalidOperationException(), () => null));
    private sealed class FakeTool : IExecutableTool
    {
        public string Name => "Test";
        public long LastElapsedMs => 0;
        public ValidationResult Validate() => new();
        public ToolResult Run(VisionImage? image) => new() { Success = true };
        public void EnsureLoaded() => throw new InvalidOperationException("不能隐式加载。");
        public void Dispose() { }
    }
    private static Window Show(object content)
    {
        var window = new Window { Content = content, Width = 1300, Height = 850, Left = -10000, Top = -10000, ShowInTaskbar = false };
        window.Show(); Drain(); return window;
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
