using System.Text.Json;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf;
using Vision.Controls.Models;
using Vision.Controls.Presentation;
using Vision.Controls.Services;
using Vision.Controls.View;
using Vision.Core.Imaging;
using Vision.Tools.OpenCv.Processing;
using PixelFormat = Vision.Core.Imaging.PixelFormat;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
[Trait("Category", "Regression")]
public sealed class ToolParameterInteractionUiTests(WorkbenchAppFixture host)
{
    [Theory]
    [InlineData("Morphology", "KernelSize")]
    [InlineData("ImageFilter", "KernelSize")]
    [InlineData("AdaptiveThreshold", "BlockSize")]
    public async Task OddParameters_RealControlStepsByTwo_AndWritesOriginalDto(string name, string property)
    {
        var workspace = host.Invoke(() => Create(name)); Window window = null!;
        try
        {
            host.Invoke(() =>
            {
                var editor = Open(workspace, out window);
                var panel = Panel(editor);
                var control = Number(panel, property);
                Assert.Equal(2, control.ValueStep);
                var original = workspace.Parameters;
                control.SetCurrentValue(NumericUpDown.ValueProperty, control.Value + control.ValueStep); Drain();
                Assert.Same(original, workspace.Parameters);
                var value = (int)original.GetType().GetProperty(property)!.GetValue(original)!;
                Assert.Equal(control.Value, value); Assert.Equal(1, value % 2);
                Assert.True(workspace.Editor.CurrentCard!.Model.IsValid);
            });
        }
        finally { host.Invoke(() => window?.Close()); await host.Invoke(() => workspace.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task ModeSelectors_UpdateExistingRowsWithoutReplacingDto_AndKeepUnusedValues()
    {
        foreach (var name in new[] { "ImageFilter", "IntensityTransform" })
        {
            var workspace = host.Invoke(() => Create(name)); Window window = null!;
            try
            {
                host.Invoke(() =>
                {
                    var editor = Open(workspace, out window); var panel = Panel(editor);
                    var original = workspace.Parameters;
                    if (original is ImageFilterParameters filter)
                    {
                        Decimal(panel, "Sigma", 2.5m);
                        Assert.True(Row(panel, "Sigma").IsRowVisible);
                        Select(panel, "Mode", ImageFilterMode.Mean);
                        Assert.False(Row(panel, "Sigma").IsRowVisible);
                        Assert.Equal(2.5, filter.Sigma);
                        Select(panel, "Mode", ImageFilterMode.Gaussian);
                        Assert.True(Row(panel, "Sigma").IsRowVisible);
                    }
                    else
                    {
                        var transform = Assert.IsType<IntensityTransformParameters>(original);
                        Decimal(panel, "Gain", 2m); Decimal(panel, "Offset", 3m);
                        Select(panel, "Mode", IntensityTransformMode.Gamma);
                        Decimal(panel, "Gamma", 1.5m);
                        Assert.False(Row(panel, "Gain").IsRowVisible); Assert.False(Row(panel, "Offset").IsRowVisible);
                        Assert.True(Row(panel, "Gamma").IsRowVisible);
                        Select(panel, "Mode", IntensityTransformMode.Normalize);
                        Assert.False(Row(panel, "Gain").IsRowVisible); Assert.False(Row(panel, "Gamma").IsRowVisible);
                        Select(panel, "Mode", IntensityTransformMode.Linear);
                        Assert.True(Row(panel, "Gain").IsRowVisible); Assert.True(Row(panel, "Offset").IsRowVisible);
                        Assert.False(Row(panel, "Gamma").IsRowVisible);
                        Assert.Equal(2, transform.Gain); Assert.Equal(3, transform.Offset); Assert.Equal(1.5, transform.Gamma);
                    }
                    Assert.Same(original, panel.TargetObject);
                });
            }
            finally { host.Invoke(() => window?.Close()); await host.Invoke(() => workspace.DisposeAsync().AsTask()); }
        }
    }

    [Fact]
    public async Task ColorSpaceChangesLabelsAndHint_WithoutClampingOrResettingStoredRange()
    {
        var workspace = host.Invoke(() => Create("ColorSegment")); Window window = null!;
        try
        {
            host.Invoke(() =>
            {
                var color = Assert.IsType<ColorSegmentParameters>(workspace.Parameters);
                color.Space = ColorSegmentSpace.Rgb; color.FirstMaximum = 240;
                var editor = Open(workspace, out window); var panel = Panel(editor);
                Assert.StartsWith("R 下限", Row(panel, "FirstMinimum").Label);
                Select(panel, "Space", ColorSegmentSpace.Hsv);
                Assert.StartsWith("H 下限", Row(panel, "FirstMinimum").Label);
                Assert.StartsWith("S 下限", Row(panel, "SecondMinimum").Label);
                Assert.StartsWith("V 下限", Row(panel, "ThirdMinimum").Label);
                Assert.Contains("179", Row(panel, "FirstMaximum").Label);
                Assert.Equal(240, color.FirstMaximum); Assert.Equal(170, color.FirstMinimum);
                Assert.IsType<SolidColorBrush>(editor.TryFindResource("MaterialDesign.Brush.ValidationError"));
                Assert.False(workspace.Editor.CurrentCard!.Model.IsValid);
                Assert.Contains(Descendants(RowGrid(panel, "FirstMaximum")).OfType<TextBlock>(), text => text.IsVisible && text.Text.Contains("首通道范围无效"));
                Assert.DoesNotContain(Descendants(RowGrid(panel, "FirstMinimum")).OfType<TextBlock>(), text => text.IsVisible && text.Text.Contains("首通道范围无效"));
                Snapshot(editor, "color-space-error.png");
                Select(panel, "Space", ColorSegmentSpace.Rgb);
                Assert.StartsWith("R 下限", Row(panel, "FirstMinimum").Label);
                Assert.Equal(240, color.FirstMaximum);
                Assert.True(workspace.Editor.CurrentCard!.Model.IsValid);
                using var json = JsonDocument.Parse(JsonSerializer.Serialize(color));
                Assert.Equal(7, json.RootElement.EnumerateObject().Count());
                Assert.Equal(240, json.RootElement.GetProperty("FirstMaximum").GetInt32());
            });
        }
        finally { host.Invoke(() => window?.Close()); await host.Invoke(() => workspace.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task InvalidParameters_ShowNextToField_ClearOldResult_AndRecoverThroughRealRun()
    {
        var workspace = host.Invoke(() => Create("ImageFilter")); Window window = null!; VisionToolEditorView editor = null!;
        try
        {
            host.Invoke(() => { workspace.SetImage(VisionImage.FromBuffer(5, 5, PixelFormat.Mono8, Enumerable.Range(0, 25).Select(i => (byte)(i * 8)).ToArray())); editor = Open(workspace, out window); });
            await host.Invoke(() => workspace.Editor.CurrentCard!.RunCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                Assert.True(workspace.Result!.Success);
                var panel = Panel(editor);
                Number(panel, "KernelSize").SetCurrentValue(NumericUpDown.ValueProperty, 4); Drain();
                Assert.Null(workspace.Result);
                var rowGrid = RowGrid(panel, "KernelSize");
                Assert.Contains(Descendants(rowGrid).OfType<TextBlock>(), text => text.IsVisible && text.Text.Contains("奇数"));
                Snapshot(editor, "filter-field-error.png");
                Number(panel, "KernelSize").SetCurrentValue(NumericUpDown.ValueProperty, 5); Drain();
                Assert.DoesNotContain(Descendants(rowGrid).OfType<TextBlock>(), text => text.IsVisible && text.Text.Contains("奇数"));
                // 完整校验语义不改：不用的Sigma若非法，必须露出该行以便修复，不能藏着阻断执行。
                ((ImageFilterParameters)workspace.Parameters).Sigma = 0;
                Select(panel, "Mode", ImageFilterMode.Mean);
                Assert.False(workspace.Editor.CurrentCard!.Model.IsValid);
                Assert.True(Row(panel, "Sigma").IsRowVisible);
                Assert.Contains(Descendants(RowGrid(panel, "Sigma")).OfType<TextBlock>(), text => text.IsVisible && text.Text.Contains("Sigma必须"));
                var sigma = Descendants(panel).OfType<DecimalUpDown>().Single(c => BindingOperations.GetBinding(c, DecimalUpDown.ValueProperty)?.Path?.Path == "Sigma");
                sigma.SetCurrentValue(DecimalUpDown.ValueProperty, 2m); Drain();
                Assert.True(workspace.Editor.CurrentCard!.Model.IsValid);
                Assert.False(Row(panel, "Sigma").IsRowVisible);
            });
            await host.Invoke(() => workspace.Editor.CurrentCard!.RunCommand.ExecuteAsync(null));
            host.Invoke(() => Assert.True(workspace.Result!.Success, workspace.Result.Message));
        }
        finally { host.Invoke(() => window?.Close()); await host.Invoke(() => workspace.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task ChangingSessionAndReloadingPanel_DoesNotCarryPreviousToolsValidation()
    {
        var filter = host.Invoke(() => Create("ImageFilter"));
        var color = host.Invoke(() => Create("ColorSegment")); Window window = null!;
        try
        {
            host.Invoke(() =>
            {
                ((ImageFilterParameters)filter.Parameters).KernelSize = 4; filter.Editor.ParametersChanged();
                var editor = Open(filter, out window); var panel = Panel(editor);
                Assert.Contains(Descendants(panel).OfType<TextBlock>(), text => text.IsVisible && text.Text.Contains("奇数"));
                editor.Session = color.Editor; Drain();
                Assert.Same(color.Parameters, panel.TargetObject);
                Assert.DoesNotContain(Descendants(panel).OfType<TextBlock>(), text => text.IsVisible && text.Text.Contains("奇数"));
                window.Content = null; Drain(); Assert.Empty(panel.Model.Groups);
                window.Content = editor; Drain();
                Assert.Same(color.Parameters, panel.TargetObject);
                Assert.StartsWith("H 下限", Row(panel, "FirstMinimum").Label);
                Assert.DoesNotContain(Descendants(panel).OfType<TextBlock>(), text => text.IsVisible && text.Text.Contains("奇数"));
            });
        }
        finally
        {
            host.Invoke(() => window?.Close());
            await host.Invoke(() => filter.DisposeAsync().AsTask()); await host.Invoke(() => color.DisposeAsync().AsTask());
        }
    }

    private static void Snapshot(FrameworkElement element, string name)
    {
        element.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth), (int)Math.Ceiling(element.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var directory = Path.Combine(AppContext.BaseDirectory, "tool-ui-batch1"); Directory.CreateDirectory(directory);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(directory, name)); encoder.Save(stream);
    }

    private static VisionToolInstanceWorkspace Create(string name)
    {
        var entry = OpenCvToolCatalog.Entries.Single(e => e.Name == "OpenCV/" + name);
        return new(new VisionBrowserCatalog([entry], [], _ => throw new NotSupportedException(), () => null), entry);
    }
    private static VisionToolEditorView Open(VisionToolInstanceWorkspace workspace, out Window window)
    {
        var page = new VisionToolEditorView { Session = workspace.Editor };
        window = new() { Content = page, Width = 1100, Height = 850, Left = -10000, Top = -10000, ShowInTaskbar = false };
        window.Show(); Drain(); return page;
    }
    private static VisionPropertyPanel Panel(VisionToolEditorView editor) => Assert.IsType<VisionPropertyPanel>(editor.DetailsPanel.FindName("ParametersPanel"));
    private static VisionPropertyItem Row(VisionPropertyPanel panel, string property) =>
        Assert.Single(panel.Model.Groups.SelectMany(g => g.AllItems).OfType<VisionPropertyItem>(), row => row.PropertyName == property);
    private static Grid RowGrid(VisionPropertyPanel panel, string property) => Assert.Single(Descendants(panel).OfType<Grid>(), grid => ReferenceEquals(grid.DataContext, Row(panel, property)) && grid.Name == "PropertyRow");
    private static NumericUpDown Number(VisionPropertyPanel panel, string property) =>
        Assert.Single(Descendants(panel).OfType<NumericUpDown>(), control => BindingOperations.GetBinding(control, NumericUpDown.ValueProperty)?.Path?.Path == property);
    private static void Decimal(VisionPropertyPanel panel, string property, decimal value)
    {
        var control = Assert.Single(Descendants(panel).OfType<DecimalUpDown>(), c => BindingOperations.GetBinding(c, DecimalUpDown.ValueProperty)?.Path?.Path == property);
        control.SetCurrentValue(DecimalUpDown.ValueProperty, value); Drain();
    }
    private static void Select(VisionPropertyPanel panel, string property, object value)
    {
        var selector = Assert.Single(Descendants(panel).OfType<ComboBox>(), control => BindingOperations.GetBinding(control, ComboBox.SelectedItemProperty)?.Path?.Path == property);
        selector.SelectedItem = value; Drain();
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
