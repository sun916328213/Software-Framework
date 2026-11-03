using System.Collections.ObjectModel;
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
using Vision.Core.Imaging;
using Vision.Core.Results;
using ValidationResult = Vision.Core.Results.ValidationResult;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
public sealed class PropertyPanelCollectionTests(WorkbenchAppFixture host)
{
    [Fact]
    public void NonBlobTool_UsesSameMetadataPanel_AndUiEditsInvalidateResults()
    {
        host.Invoke(() =>
        {
            var parameters = new SampleParameters();
            using var workspace = new VisionBrowserWorkspace(new(
                [new("Sample", "Fake", () => (new FakeTool(), parameters, null))], [],
                _ => throw new InvalidOperationException(), () => null));
            using var browser = new VisionBrowserViewModel(workspace);
            var page = new VisionToolEditorView { Session = browser };
            var window = new Window { Content = page, Width = 1300, Height = 850, Left = -10000, Top = -10000, ShowInTaskbar = false };
            try
            {
                window.Show();
                Drain();
                Assert.Null(browser.BlobEditor);
                Assert.True(browser.UsesMetadataEditor);
                var panel = Assert.Single(Descendants(page).OfType<VisionPropertyPanel>());
                Assert.Same(parameters, panel.TargetObject);
                Assert.DoesNotContain(Descendants(page), item => item.GetType().Name == "BlobParametersView");
                Assert.Empty(Descendants(page).OfType<Xceed.Wpf.Toolkit.PropertyGrid.PropertyGrid>());
                var selector = Assert.Single(Descendants(panel).OfType<VisionMultiSelectComboBox>());
                Assert.Same(parameters.Choices, selector.ItemsSource);
                Assert.True(selector.IsEnabled);
                var table = Assert.Single(Descendants(panel).OfType<DataGrid>());
                Assert.False(table.IsReadOnly);
                Assert.False(table.CanUserAddRows);
                Assert.False(table.CanUserDeleteRows);
                Assert.Contains(table.Columns, c => Equals(c.Header, "下限"));
                Assert.True(table.Columns.Single(c => Equals(c.Header, "名称")).IsReadOnly);
                Assert.DoesNotContain(table.Columns, c => Equals(c.Header, "Hidden"));
                var minimum = Assert.Single(Descendants(panel).OfType<NumericUpDown>());
                browser.CurrentCard!.Run();
                Assert.NotNull(browser.CurrentCard.LastResult);
                minimum.SetCurrentValue(NumericUpDown.ValueProperty, 77);
                Drain();
                Assert.Equal(77, parameters.Minimum);
                Assert.Null(browser.CurrentCard.LastResult);
                Assert.Equal("校验通过", browser.CurrentCard.StatusText);
                // 普通 getter-only 属性不因支持集合内容编辑而被放开。
                Assert.Contains(Descendants(panel).OfType<TextBlock>(), t => t.Text == "17");
                Assert.Null(typeof(SampleParameters).GetProperty(nameof(SampleParameters.ReadOnlyValue))!.SetMethod);
            }
            finally { window.Close(); Drain(); }
        });
    }

    public sealed class SampleParameters
    {
        public string[] Choices { get; } = ["first", "second"];
        [ToolProperty(DisplayName = "下限", EditorType = E_PropertyEditorType.Int)]
        public int Minimum { get; set; }
        [ToolProperty(DisplayName = "多选", EditorType = E_PropertyEditorType.MultiSelect,
            IsContentEditable = true, ListItemsSourceName = nameof(Choices))]
        public ObservableCollection<string> Selected { get; } = new(["first"]);
        [ToolProperty(DisplayName = "范围", EditorType = E_PropertyEditorType.DataGridList, IsContentEditable = true)]
        public ReadOnlyObservableCollection<RangeRow> Rows { get; } = new(new ObservableCollection<RangeRow> { new() });
        [ToolProperty(DisplayName = "只读")]
        public int ReadOnlyValue => 17;
    }
    public sealed class RangeRow
    {
        [DisplayName("名称")] public string Name => "range";
        [DisplayName("下限")] public double Min { get; set; }
        [Browsable(false)] public string Hidden => "hidden";
    }
    private sealed class FakeTool : IExecutableTool
    {
        public string Name => "Sample";
        public long LastElapsedMs => 0;
        public ValidationResult Validate() => new();
        public ToolResult Run(VisionImage? image) => new();
        public void EnsureLoaded() { }
        public void Dispose() { }
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
