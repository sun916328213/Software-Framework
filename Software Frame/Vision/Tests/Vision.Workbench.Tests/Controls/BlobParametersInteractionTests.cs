using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf;
using Vision.Controls;
using Vision.Controls.View;
using Vision.Controls.ViewModels;
using Cv = Vision.Tools.OpenCv.Inspection;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
public sealed class BlobParametersInteractionTests(WorkbenchAppFixture host)
{
    [Fact]
    public void Browser_UsesOriginalGroupedPanel_AndRealEditorsChangeAlgorithmParameters()
    {
        host.Invoke(() =>
        {
            using var browser = new TestBrowserViewModel();
            var page = new VisionToolEditorView { Session = browser };
            var window = ShowOffscreen(page);
            try
            {
                var editor = Assert.IsType<BlobParametersEditorViewModel>(browser.BlobEditor);
                Assert.Empty(editor.SelectedFeatures);
                Assert.Empty(editor.Ranges);
                Assert.DoesNotContain(Descendants(page), item => item.GetType().Name == "BlobParametersView");
                var panel = Assert.Single(Descendants(page).OfType<VisionPropertyPanel>());
                Assert.Same(editor, panel.TargetObject);
                Assert.True(panel.ExpandGroups);
                Assert.Contains(Descendants(panel).OfType<Expander>(), e => Equals(e.Header, "1.灰度分割") && e.IsExpanded);
                Assert.Contains(Descendants(panel).OfType<TextBlock>(), t => t.Text == "最小灰度值:");
                Assert.Contains(Descendants(panel).OfType<TextBlock>(), t => t.Text == "最小面积:");
                Assert.Contains(Descendants(panel).OfType<TextBlock>(), t => t.Text == "特征多选:");
                Assert.DoesNotContain(Descendants(panel).OfType<TextBlock>(), t => t.Text == "特征说明:");
                var rangeLabel = Descendants(panel).OfType<TextBlock>().Single(t => t.Text == "范围表格:");
                Assert.False(rangeLabel.IsVisible);
                Assert.Empty(Descendants(page).OfType<Xceed.Wpf.Toolkit.PropertyGrid.PropertyGrid>());
                var minimum = Descendants(panel).OfType<NumericUpDown>().Single(c =>
                    BindingOperations.GetBinding(c, NumericUpDown.ValueProperty)?.Path?.Path == nameof(editor.MinGray));
                Assert.Equal(0, minimum.Minimum);
                Assert.Equal(255, minimum.Maximum);
                Assert.Equal(1, minimum.ValueStep);
                minimum.SetCurrentValue(NumericUpDown.ValueProperty, 150);
                Drain();
                Assert.Equal(150, Assert.IsType<Cv.BlobParameters>(browser.CurrentParameters).MinGray);

                var minArea = Descendants(panel).OfType<NumericUpDown>().Single(c =>
                    BindingOperations.GetBinding(c, NumericUpDown.ValueProperty)?.Path?.Path == nameof(editor.MinArea));
                Assert.Equal(0, minArea.Minimum);
                minArea.SetCurrentValue(NumericUpDown.ValueProperty, 150);
                Drain();
                Assert.Equal(150, Assert.IsType<Cv.BlobParameters>(browser.CurrentParameters).MinArea);

                var selector = Assert.Single(Descendants(panel).OfType<VisionMultiSelectComboBox>());
                selector.IsDropDownOpen = true;
                Drain();
                var widthContainer = Assert.IsType<ComboBoxItem>(selector.ItemContainerGenerator.ContainerFromItem("width"));
                var checkbox = Assert.Single(Descendants(widthContainer).OfType<CheckBox>());
                checkbox.SetCurrentValue(ToggleButton.IsCheckedProperty, true);
                Drain();
                var areaContainer = Assert.IsType<ComboBoxItem>(selector.ItemContainerGenerator.ContainerFromItem("area"));
                var areaCheckbox = Assert.Single(Descendants(areaContainer).OfType<CheckBox>());
                areaCheckbox.SetCurrentValue(ToggleButton.IsCheckedProperty, true);
                Drain();
                selector.IsDropDownOpen = false;
                Drain();
                Assert.True(rangeLabel.IsVisible);
                Assert.Contains("width", editor.SelectedFeatures);
                Assert.Contains(Assert.IsType<Cv.BlobParameters>(browser.CurrentParameters).Features, f => f.Feature == "width");

                browser.LoadImage();
                browser.CurrentCard!.Run();
                Assert.True(browser.CurrentCard.LastResult!.Success);
                var table = Assert.Single(Descendants(panel).OfType<DataGrid>());
                Assert.False(table.IsReadOnly);
                Assert.False(table.CanUserAddRows);
                Assert.Contains(table.Columns, c => Equals(c.Header, "下限"));
                var area = editor.Ranges.Single(r => r.Feature == "area");
                table.SelectedItem = area;
                table.CurrentCell = new DataGridCellInfo(area, table.Columns[1]);
                Assert.True(table.BeginEdit());
                Drain();
                var input = Descendants(table).OfType<TextBox>().Single(t =>
                    BindingOperations.GetBinding(t, TextBox.TextProperty)?.Path?.Path == nameof(BlobFeatureRow.Min));
                input.Text = "401";
                input.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
                table.CommitEdit(DataGridEditingUnit.Cell, true);
                Drain();
                Assert.Null(browser.CurrentCard.LastResult);
                Assert.Equal(401, Assert.IsType<Cv.BlobParameters>(browser.CurrentParameters).Features.Single(f => f.Feature == "area").Min);
                browser.CurrentCard.Run();
                Assert.Empty(Assert.IsType<Cv.BlobResult>(browser.CurrentCard.LastResult).Items);

                browser.SelectedEntry = browser.ToolEntries.Single(t => t.Name == "OpenCV/Threshold");
                Drain();
                Assert.DoesNotContain(Descendants(page), item => item.GetType().Name == "BlobParametersView");
                var thresholdPanel = Assert.Single(Descendants(page).OfType<VisionPropertyPanel>());
                Assert.Same(browser.CurrentParameters, thresholdPanel.TargetObject);
                Assert.Empty(Descendants(page).OfType<Xceed.Wpf.Toolkit.PropertyGrid.PropertyGrid>());

                // 所有参数共用同一个面板；切回时绑定新会话，不能缓存旧编辑器。
                browser.SelectedEntry = browser.ToolEntries.Single(t => t.Name == "OpenCV/Blob");
                Drain();
                var restored = Assert.Single(Descendants(page).OfType<VisionPropertyPanel>());
                Assert.Same(browser.ParameterEditor, restored.TargetObject);
                Assert.NotSame(editor, restored.TargetObject);
                Assert.DoesNotContain(Descendants(page), item => item.GetType().Name == "BlobParametersView");
                Assert.Empty(Descendants(page).OfType<Xceed.Wpf.Toolkit.PropertyGrid.PropertyGrid>());
            }
            finally { window.Close(); Drain(); }
        });
    }

    [Fact]
    public void BlobFeatureMultiSelect_DefaultsUnchecked_ForBothBackends()
    {
        host.Invoke(() =>
        {
            using var browser = new TestBrowserViewModel();
            var page = new VisionToolEditorView { Session = browser };
            var window = ShowOffscreen(page);
            try
            {
                Assert.Empty(Assert.IsType<BlobParametersEditorViewModel>(browser.BlobEditor).SelectedFeatures);
                AssertNoFeatureChecked(page);
                AssertSharedFeatureList(page);
                browser.SelectedEntry = browser.ToolEntries.Single(t => t.Name == "HALCON/Blob");
                Drain();
                Assert.Empty(Assert.IsType<BlobParametersEditorViewModel>(browser.BlobEditor).SelectedFeatures);
                AssertNoFeatureChecked(page);
                AssertSharedFeatureList(page);
            }
            finally { window.Close(); Drain(); }
        });
    }

    [Fact]
    public void MultiSelect_RebindAndReload_DetachesOldCollection_AndDoesNotDuplicateSelection()
    {
        host.Invoke(() =>
        {
            var first = new ObservableCollection<string> { "area" };
            var second = new ObservableCollection<string> { "height" };
            var selector = new VisionMultiSelectComboBox { ItemsSource = new[] { "area", "width", "height" }, SelectedItems = first };
            var container = new ContentControl { Content = selector };
            var window = ShowOffscreen(container);
            try
            {
                first.Add("width");
                Assert.Contains("width", selector.Text);
                selector.SelectedItems = second;
                first.Clear();
                Assert.Equal("height", selector.Text);
                second.Add("width");
                Assert.Contains("width", selector.Text);
                container.Content = null;
                Drain();
                second.Clear(); // 已卸载，集合不应继续驱动旧控件。
                Assert.Contains("width", selector.Text);
                container.Content = selector;
                Drain();
                Assert.Empty(selector.Text);
                selector.IsDropDownOpen = true;
                Drain();
                var item = Assert.IsType<ComboBoxItem>(selector.ItemContainerGenerator.ContainerFromItem("area"));
                var checkbox = Assert.Single(Descendants(item).OfType<CheckBox>());
                checkbox.SetCurrentValue(ToggleButton.IsCheckedProperty, true);
                Assert.Equal("area", Assert.Single(second));
                selector.IsDropDownOpen = false;
                container.Content = null;
                Drain();
                container.Content = selector;
                Drain();
                Assert.Equal("area", Assert.Single(second));
                Assert.Equal("area", selector.Text);
            }
            finally { window.Close(); Drain(); }
        });
    }

    private static void AssertSharedFeatureList(VisionToolEditorView page)
    {
        var selector = Assert.Single(Descendants(page).OfType<VisionMultiSelectComboBox>());
        Assert.Equal(Cv.BlobTool.SupportedFeatures, selector.Items.OfType<string>());
    }

    private static void AssertNoFeatureChecked(VisionToolEditorView page)
    {
        var selector = Assert.Single(Descendants(page).OfType<VisionMultiSelectComboBox>());
        Assert.Empty(selector.Text);
        selector.IsDropDownOpen = true;
        Drain();
        try
        {
            Assert.NotEmpty(selector.Items);
            foreach (var item in selector.Items)
            {
                var container = Assert.IsType<ComboBoxItem>(selector.ItemContainerGenerator.ContainerFromItem(item));
                var checkbox = Assert.Single(Descendants(container).OfType<CheckBox>());
                Assert.NotEqual(true, checkbox.IsChecked);
            }
        }
        finally { selector.IsDropDownOpen = false; }
    }

    private static Window ShowOffscreen(object content)
    {
        var window = new Window { Content = content, Width = 1300, Height = 850, Left = -10000, Top = -10000, ShowInTaskbar = false };
        window.Show();
        Drain();
        return window;
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
