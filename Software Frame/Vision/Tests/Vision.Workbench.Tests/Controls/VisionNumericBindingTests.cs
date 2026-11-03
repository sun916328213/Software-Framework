using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using Vision.Controls.Services;
using Vision.Core.Imaging;
using Vision.FlowNode;
using Vision.Tools.OpenCv.Inspection;
using Vision.Tools.OpenCv.Processing;

using WPFCustomControls.PropertyGrid.Core.ViewModels;
using PixelFormat = Vision.Core.Imaging.PixelFormat;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
[Trait("Category", "Regression")]
public sealed class VisionNumericBindingTests(WorkbenchAppFixture host)
{
    [Fact]
    public async Task Statistics_HasExplicitNumericFieldSelector_OnOriginalNodeProperties()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create);
        try
        {
            host.Invoke(() =>
            {
                var node = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/GrayStatistics"));
                Assert.Same(node, vm.PropertyGridVM.TargetObject);
                Assert.Single(vm.PropertyGridVM.Groups.SelectMany(g => g.Items), r => r.PropertyName == "NumericOutputField");
            });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(80)]
    public async Task OriginalComboboxesAndRunCommand_ConsumeActualValue_KeepRecipeAndRoundTripNewSession(int minimum)
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create); var reopened = host.Invoke(VisionFlowToolboxTests.Create);
        Window window = null!; VisionToolNodeBase source = null!, target = null!, blob = null!;
        var path = Artifact($"statistics-threshold-{minimum}.flow");
        try
        {
            host.Invoke(() =>
            {
                var page = new VisionFlowView { DataContext = vm };
                window = new() { Content = page, Width = 1500, Height = 950, ShowInTaskbar = false, Left = -10000 }; window.Show();
                source = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/GrayStatistics")); Idle(page);
                Select(page, "NumericOutputField", "Minimum");
                target = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/Threshold")); Idle(page);
                Select(page, "NumericInputParameter", "MinGray"); Select(page, "NumericSourceNodeId", source.Id);
                ((ThresholdParameters)target.Session.Parameters).MinGray = 99;
                blob = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/Blob"));
                ((VisionToolInstanceWorkspace)blob.Session).Editor.BlobEditor!.MinArea = 1;
                Connect(vm, source, target); Connect(vm, target, blob); VisionImageBinding.Select(blob, target);
                vm.SetImage(Frame((byte)minimum)); vm.Select(target); Idle(page);
                source.Name = "同名显示可改"; Idle(page);
                Assert.NotNull(Assert.Single(VisionFlowR0Tests.Descendants(page).OfType<ComboBox>(), c => c.DataContext is PropertyItemViewModel r && r.PropertyName == "NumericSourceNodeId").SelectedItem);
                Assert.Equal(source.Id, Assert.Single(VisionFlowR0Tests.Descendants(page).OfType<ComboBox>(), c => c.DataContext is PropertyItemViewModel r && r.PropertyName == "NumericSourceNodeId").SelectedValue);
                Assert.Equal(source.Id, target.NumericSourceNodeId);
                Assert.Contains(Row(vm, "NumericSourceNodeId").EnumOptions!, o => Equals(o.Value, source.Id) && o.DisplayName.Contains(source.Name));
                vm.Chart.Model.IsModified = false;
            });
            await host.Invoke(() => ((IAsyncRelayCommand)Assert.Single(VisionFlowR0Tests.Descendants(window).OfType<Button>(),
                b => System.Windows.Automation.AutomationProperties.GetName(b) == "当前流程执行").Command).ExecuteAsync(null));
            await host.Invoke(async () =>
            {
                CheckActual(source, target, blob, minimum); Idle((FrameworkElement)window.Content);
                Assert.Equal(source.Id, Assert.Single(VisionFlowR0Tests.Descendants(window).OfType<ComboBox>(), c => c.DataContext is PropertyItemViewModel r && r.PropertyName == "NumericSourceNodeId").SelectedValue);
                Assert.False(vm.Chart.Model.IsModified);
                var rows = ((VisionToolInstanceWorkspace)target.Session).Editor.CurrentCard!.TableRows!;
                Assert.Equal(minimum, rows[0]["执行灰度下限"]); Assert.Equal(255, rows[0]["执行灰度上限"]);
                var state = target.CaptureDocumentState();
                var config = Vision.Core.Tools.VisionToolConfiguration.FromState(state);
                Assert.Equal(99, config.Parameters!.Value.GetProperty(nameof(ThresholdParameters.MinGray)).GetInt32());
                Assert.True(vm.Chart.SaveDocument(path));
                var page = (VisionFlowView)window.Content;
                var selected = Assert.Single(VisionFlowR0Tests.Descendants(page).OfType<ComboBox>(), c => c.DataContext is PropertyItemViewModel r && r.PropertyName == "NumericSourceNodeId");
                Assert.Contains(selected.Items.Cast<WPFCustomControls.PropertyGrid.Core.Models.EnumOption>(), o => Equals(o.Value, source.Id));
                Assert.Contains(source.Name, Assert.IsType<WPFCustomControls.PropertyGrid.Core.Models.EnumOption>(selected.SelectedItem).DisplayName);
                await Task.Delay(350); // 仅等待原控件运行锁/选项展示动画完成后留图。
                Assert.Equal(source.Id, selected.SelectedValue);
                Assert.NotNull(selected.SelectedItem);
                Assert.Contains(VisionFlowR0Tests.Descendants(selected).OfType<TextBlock>(), t => t.Text.Contains(source.Name));
                Snapshot(page, $"parameter-binding-{minimum}.png");
                var full = page.OpenToolEditor(target)!;
                var editor = (Vision.Controls.View.VisionToolEditorView)full.Content;
                ((TabItem)editor.DetailsPanel.FindName("ResultsTab")).IsSelected = true;
                Snapshot(full, $"applied-threshold-{minimum}.png"); full.Close(); window.Close();
            });
            await host.Invoke(() => vm.DisposeAsync().AsTask());
            host.Invoke(() =>
            {
                Assert.True(((VisionToolInstanceWorkspace)target.Session).Model.IsClosed);
                Assert.True(reopened.Chart.LoadDocument(path));
                source = (VisionToolNodeBase)reopened.Chart.Nodes[0].Model; target = (VisionToolNodeBase)reopened.Chart.Nodes[1].Model; blob = (VisionToolNodeBase)reopened.Chart.Nodes[2].Model;
                Assert.Null(source.NumericOutput); Assert.Null(target.Result);
                Assert.Equal("Minimum", source.NumericOutputField); Assert.Equal("MinGray", target.NumericInputParameter);
                Assert.Equal(source.Id, target.NumericSourceNodeId);
                Assert.Equal(99, ((ThresholdParameters)target.Session.Parameters).MinGray);
            });
            await host.Invoke(() => reopened.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                CheckActual(source, target, blob, minimum);
                source.Session.ClearResult();
                Assert.Null(target.Result); Assert.Null(blob.Result); Assert.Null(blob.Session.Image);
            });
        }
        finally
        {
            host.Invoke(() => window?.Close());
            await host.Invoke(() => vm.DisposeAsync().AsTask()); await host.Invoke(() => reopened.DisposeAsync().AsTask());
        }
    }

    [Fact]
    public async Task SameDisplayNamesAndRename_KeepSecondSourceStableIdAndRenderedSelection()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create); Window window = null!;
        try
        {
            host.Invoke(() =>
            {
                var page = new VisionFlowView { DataContext = vm };
                window = new() { Content = page, Width = 1500, Height = 950, ShowInTaskbar = false, Left = -10000 }; window.Show();
                var a = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/GrayStatistics"));
                var b = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/GrayStatistics"));
                a.Name = b.Name = "相同显示名";
                var target = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/Threshold")); Idle(page);
                Select(page, "NumericInputParameter", "MinGray"); Select(page, "NumericSourceNodeId", b.Id);
                a.Name = b.Name = "都已重命名"; Idle(page);
                var combo = Assert.Single(VisionFlowR0Tests.Descendants(page).OfType<ComboBox>(),
                    c => c.DataContext is PropertyItemViewModel r && r.PropertyName == "NumericSourceNodeId");
                Assert.Equal(b.Id, target.NumericSourceNodeId); Assert.Equal(b.Id, combo.SelectedValue);
                Assert.Equal(b.Id, Assert.IsType<WPFCustomControls.PropertyGrid.Core.Models.EnumOption>(combo.SelectedItem).Value);
                Assert.Contains(VisionFlowR0Tests.Descendants(combo).OfType<TextBlock>(), t => t.Text.Contains("都已重命名"));
            });
        }
        finally { host.Invoke(() => window?.Close()); await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task MissingOrInvalidatedSourceAndDisconnectedWire_DoNotUseCachedZeroOrRecipeFallback()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create); VisionToolNodeBase source = null!, target = null!;
        try
        {
            host.Invoke(() => (source, target) = Setup(vm, "Minimum"));
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.Invoke(() => target.ExecuteAsync(CancellationToken.None)));
            host.Invoke(() => Assert.Contains("缺失", ((VisionToolInstanceWorkspace)target.Session).Editor.CurrentOperationText));
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                Assert.Equal(0, Assert.IsType<ThresholdResult>(target.Result).AppliedMinGray);
                source.Session.ClearResult();
                Assert.Null(target.Result); Assert.Null(target.BoundNumeric); Assert.Null(source.NumericOutput);
            });
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.Invoke(() => target.ExecuteAsync(CancellationToken.None)));
            host.Invoke(() => { target.NumericSourceNodeId = ""; Assert.Equal("MinGray", target.NumericInputParameter); });
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                Assert.Null(target.Result);
                Assert.Contains("有效连接", ((VisionToolInstanceWorkspace)target.Session).Editor.CurrentOperationText);
                Assert.Equal(99, ((ThresholdParameters)target.Session.Parameters).MinGray);
                target.NumericInputParameter = ""; // 只有显式取消绑定才恢复配方执行。
            });
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() => { var result = Assert.IsType<ThresholdResult>(target.Result); Assert.Equal(99, result.AppliedMinGray); Assert.Equal(1, result.WhitePixelCount); });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task RemovingSourceNode_CannotSilentlyRestoreRecipeValue()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create); VisionToolNodeBase source = null!, target = null!;
        try
        {
            host.Invoke(() => (source, target) = Setup(vm, "Minimum"));
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() => vm.Chart.Nodes.Remove(vm.Chart.Nodes.Single(n => ReferenceEquals(n.Model, source))));
            await host.Invoke(() => source.DisposeAsync().AsTask());
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.Invoke(() => target.ExecuteAsync(CancellationToken.None)));
            host.Invoke(() =>
            {
                Assert.Equal("MinGray", target.NumericInputParameter);
                Assert.Null(target.Result); Assert.Null(target.OutputImage);
                Assert.Equal(99, ((ThresholdParameters)target.Session.Parameters).MinGray);
            });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FractionalMeanOrGray16Unit_IsRejectedWithoutTouchingRecipe(bool sixteen)
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create); VisionToolNodeBase source = null!, target = null!;
        try
        {
            host.Invoke(() =>
            {
                (source, target) = Setup(vm, sixteen ? "Minimum" : "Mean");
                if (sixteen) source.Session.SetImage(VisionImage.FromBuffer(2, 2, PixelFormat.Mono16, [0, 0, 80, 0, 80, 0, 120, 0]));
                else vm.SetImage(VisionImage.FromBuffer(2, 2, PixelFormat.Mono8, [0, 80, 81, 120]));
            });
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                Assert.True(source.Result!.Success); Assert.NotNull(source.NumericOutput);
                Assert.Null(target.Result); Assert.Null(target.OutputImage);
                Assert.Contains(sixteen ? "单位" : "整数", ((VisionToolInstanceWorkspace)target.Session).Editor.CurrentOperationText);
                Assert.Equal(99, ((ThresholdParameters)target.Session.Parameters).MinGray);
            });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task PreviousFrameWithIdenticalPixels_IsRejected_AndRunningConfigurationIsLocked()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create); VisionToolNodeBase source = null!, target = null!;
        try
        {
            host.Invoke(() =>
            {
                (source, target) = Setup(vm, "Minimum");
                vm.Chart.Model.IsRunning = true;
                Assert.Throws<InvalidOperationException>(() => target.NumericInputParameter = "MaxGray");
                Assert.Throws<InvalidOperationException>(() => source.NumericOutputField = "Maximum");
                vm.Chart.Model.IsRunning = false;
            });
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                var samePixels = VisionImageSnapshot.Copy(target.Session.ConfiguredImage!); samePixels.FrameId++;
                target.Session.SetImage(samePixels);
                Assert.NotNull(source.NumericOutput);
            });
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.Invoke(() => target.ExecuteAsync(CancellationToken.None)));
            host.Invoke(() =>
            {
                Assert.Null(target.Result); Assert.Contains("同一帧", ((VisionToolInstanceWorkspace)target.Session).Editor.CurrentOperationText);
                Assert.Equal(99, ((ThresholdParameters)target.Session.Parameters).MinGray);
            });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    private static (VisionToolNodeBase Source, VisionToolNodeBase Target) Setup(VisionFlowViewModel vm, string field)
    {
        var source = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/GrayStatistics"));
        var target = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/Threshold"));
        source.NumericOutputField = field; target.NumericInputParameter = "MinGray"; target.NumericSourceNodeId = source.Id;
        ((ThresholdParameters)target.Session.Parameters).MinGray = 99;
        Connect(vm, source, target); vm.SetImage(Frame()); return (source, target);
    }
    private static VisionImage Frame(byte minimum = 0)
    {
        var image = VisionImage.FromBuffer(2, 2, PixelFormat.Mono8, [minimum, 80, 80, 120]); image.FrameId = 7; return image;
    }
    private static void CheckActual(VisionToolNodeBase source, VisionToolNodeBase target, VisionToolNodeBase blob, int minimum)
    {
        Assert.Equal(minimum, Assert.IsType<GrayStatisticsResult>(source.Result).Minimum);
        Assert.Equal(minimum, source.NumericOutput!.Value);
        var actual = Assert.IsType<ThresholdResult>(target.Result);
        Assert.True(actual.Success, actual.Message); Assert.Equal(minimum, actual.AppliedMinGray); Assert.Equal(4, actual.WhitePixelCount);
        Assert.Equal(4, Assert.Single(Assert.IsType<BlobResult>(blob.Result).Items).Area);
        Assert.Same(target.OutputImage, blob.Session.Image);
        Assert.Equal(99, ((ThresholdParameters)target.Session.Parameters).MinGray);
    }
    private static PropertyItemViewModel Row(VisionFlowViewModel vm, string name) => vm.PropertyGridVM.Groups.SelectMany(g => g.AllItems).Single(r => r.PropertyName == name);
    private static void Select(FrameworkElement page, string name, string value)
    {
        Idle(page);
        var combo = Assert.Single(VisionFlowR0Tests.Descendants(page).OfType<ComboBox>(),
            c => c.DataContext is PropertyItemViewModel r && r.PropertyName == name);
        combo.SelectedValue = value; Idle(page); Assert.Equal(value, combo.SelectedValue);
    }
    private static void Connect(VisionFlowViewModel vm, VisionToolNodeBase a, VisionToolNodeBase b) => vm.Chart.Connections.Add(new()
    {
        SourceNodeId = a.Id, SourcePortId = a.OutputPorts.Single(p => p.Name == "Next_Out").Id,
        TargetNodeId = b.Id, TargetPortId = b.InputPorts.Single(p => p.Name == "Next_IN").Id
    });
    private static void Idle(FrameworkElement view) { view.UpdateLayout(); view.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle); }
    private static string Artifact(string name)
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "a2-acceptance"); Directory.CreateDirectory(folder); return Path.Combine(folder, name);
    }
    private static void Snapshot(FrameworkElement view, string name)
    {
        Idle(view); var bitmap = new RenderTargetBitmap((int)view.ActualWidth, (int)view.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(view);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = File.Create(Artifact(name)); png.Save(stream);
    }
}
