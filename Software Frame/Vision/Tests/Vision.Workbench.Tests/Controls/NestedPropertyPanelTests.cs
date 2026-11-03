using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf;
using Vision.Controls;
using Vision.Controls.View;
using Vision.Tools.Halcon.Measurement;
using Xunit;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
public sealed class NestedPropertyPanelTests(WorkbenchAppFixture host)
{
    [Fact]
    public void LineGroup_UsesNestedMetadataEditors_AndReportsValueAndStructureChanges()
    {
        host.Invoke(() =>
        {
            var line = new FindLineParameters();
            var parameters = new LineGroupParameters { Lines = [line] };
            var panel = new VisionPropertyPanel { ExpandGroups = true, AutoGenerateProperties = true, TargetObject = parameters };
            var notifications = 0; panel.ParametersChanged += (_, _) => notifications++;
            var window = new Window { Content = panel, Width = 900, Height = 850, Left = -10000, Top = -10000, ShowInTaskbar = false };
            window.Show(); Drain();
            try
            {
                var grid = Assert.Single(Descendants(panel).OfType<DataGrid>());
                Assert.True(grid.CanUserAddRows); Assert.True(grid.CanUserDeleteRows);
                grid.SelectedItem = line; grid.UpdateLayout(); Drain();
                var child = Assert.Single(Descendants(panel).OfType<VisionPropertyPanel>());
                Assert.Same(line, child.TargetObject);
                var sigma = Descendants(child).OfType<DecimalUpDown>().Single(control =>
                    BindingOperations.GetBinding(control, DecimalUpDown.ValueProperty)?.Path?.Path == nameof(FindLineParameters.Sigma));
                notifications = 0;
                sigma.SetCurrentValue(DecimalUpDown.ValueProperty, 2.5m); Drain();
                Assert.Equal(2.5, line.Sigma); Assert.True(notifications > 0);
                var view = Assert.IsAssignableFrom<IEditableCollectionView>(CollectionViewSource.GetDefaultView(grid.ItemsSource));
                Assert.True(view.CanAddNew);
                var added = Assert.IsType<FindLineParameters>(view.AddNew()); view.CommitNew(); Drain();
                Assert.Contains(added, parameters.Lines); Assert.Equal(2, parameters.Lines.Count);
                window.Content = null; Drain(); var before = notifications;
                var detached = Assert.IsType<FindLineParameters>(view.AddNew()); view.CommitNew(); Drain();
                Assert.Equal(before, notifications); Assert.Contains(detached, parameters.Lines);
                window.Content = panel; Drain(); notifications = 0;
                view.Remove(detached); Drain(); Assert.True(notifications > 0);
                var replacement = new LineGroupParameters { Lines = [new()] };
                panel.TargetObject = replacement; Drain(); notifications = 0;
                view.Remove(added); Drain(); Assert.Equal(0, notifications);
                Assert.Single(replacement.Lines); Assert.DoesNotContain(added, parameters.Lines);
            }
            finally { window.Close(); Drain(); }
        });
    }
    [Fact]
    public void AutoBarcode_NumericCollectionAndNullableCoordinates_EditOriginalDtoWithoutFlatteningJson()
    {
        host.Invoke(() =>
        {
            var parameters = new Vision.Tools.Halcon.Identification.AutoBarcodeParameters();
            var panel = new VisionPropertyPanel { ExpandGroups = true, TargetObject = parameters };
            var window = new Window { Content = panel, Width = 900, Height = 850, Left = -10000, Top = -10000, ShowInTaskbar = false };
            window.Show(); Drain();
            try
            {
                TextBox Box(string name) => Descendants(panel).OfType<TextBox>().Single(box =>
                    BindingOperations.GetBinding(box, TextBox.TextProperty)?.Path?.Path == name);
                var scales = Box(nameof(parameters.Scales));
                scales.Text = "1, 2.5, 3"; scales.GetBindingExpression(TextBox.TextProperty)!.UpdateSource(); Drain();
                Assert.Equal(new[] { 1.0, 2.5, 3 }, parameters.Scales);
                var valid = parameters.Scales;
                scales.Text = "1, NaN"; scales.GetBindingExpression(TextBox.TextProperty)!.UpdateSource(); Drain();
                Assert.True(Validation.GetHasError(scales)); Assert.Same(valid, parameters.Scales);
                scales.Text = "1, 1.5, 2"; scales.GetBindingExpression(TextBox.TextProperty)!.UpdateSource(); Drain();
                Assert.False(Validation.GetHasError(scales)); Assert.Equal(new[] { 1.0, 1.5, 2 }, parameters.Scales);
                var row = Box(nameof(parameters.Row1)); Assert.Null(parameters.Row1);
                row.Text = "-12.5"; row.GetBindingExpression(TextBox.TextProperty)!.UpdateSource(); Drain();
                Assert.Equal(-12.5, parameters.Row1);
                row.Text = ""; row.GetBindingExpression(TextBox.TextProperty)!.UpdateSource(); Drain();
                Assert.Null(parameters.Row1); Assert.False(Validation.GetHasError(row));
                using var json = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(parameters));
                Assert.Equal(System.Text.Json.JsonValueKind.Array, json.RootElement.GetProperty("Scales").ValueKind);
                Assert.Equal(System.Text.Json.JsonValueKind.Null, json.RootElement.GetProperty("Row1").ValueKind);
            }
            finally { window.Close(); Drain(); }
        });
    }
    private static void Drain() => Application.Current.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
