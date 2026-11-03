using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using FlowEditor.Controls.Nodes;
using FlowEditor.Controls.Nodes.FlowControl;
using Vision.Controls.Services;
using Vision.Core.Imaging;
using Vision.Core.Results;
using Vision.Core.Tools;
using Vision.FlowNode;
using Vision.Tools.OpenCv.Inspection;
using Vision.Tools.OpenCv.Processing;

using WPFCustomControls.PropertyGrid.Core.ViewModels;
using PixelFormat = Vision.Core.Imaging.PixelFormat;

namespace Vision.Workbench.Tests.Controls;
[Collection("WorkbenchAppBoot")]
[Trait("Category", "Regression")]
public sealed class VisionOtsuTests(WorkbenchAppFixture host)
{
    [Theory]
    [InlineData(ThresholdMode.Otsu, true)]
    [InlineData(ThresholdMode.OtsuInverse, false)]
    public async Task OriginalModeSelector_ActualMaskBlobCountToleranceSwitch_ReopenAndBlackZero(ThresholdMode mode, bool accepted)
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create); var reopened = host.Invoke(VisionFlowToolboxTests.Create);
        VisionToolNodeBase source = null!, blob = null!, tolerance = null!, yes = null!, no = null!; SwitchNode dispatch = null!; Window window = null!;
        var path = Artifact($"otsu-{mode}.flow");
        try
        {
            host.Invoke(() =>
            {
                var page = new VisionFlowView { DataContext = vm }; window = new() { Content = page, Width = 1600, Height = 960, ShowInTaskbar = false, Left = -10000 }; window.Show();
                (source, blob, tolerance, dispatch, yes, no) = Setup(vm, accepted);
                vm.Select(source); Assert.True(Row(vm, "MinGray").IsVisible); Select(page, "Mode", mode);
                Assert.False(Row(vm, "MinGray").IsVisible); Assert.False(Row(vm, "MaxGray").IsVisible);
                Assert.Equal(mode, ((ThresholdParameters)source.Session.Parameters).Mode); vm.Chart.Model.IsModified = false;
            });
            await host.Invoke(() => ((IAsyncRelayCommand)Assert.Single(VisionFlowR0Tests.Descendants(window).OfType<Button>(),
                b => System.Windows.Automation.AutomationProperties.GetName(b) == "当前流程执行").Command).ExecuteAsync(null));
            host.Invoke(() =>
            {
                Check(source, blob, tolerance, dispatch, yes, no, mode, accepted); Assert.False(vm.Chart.Model.IsModified);
                Assert.True(vm.Chart.SaveDocument(path)); vm.Chart.ZoomToFitCommand.Execute(null); Snapshot((FrameworkElement)window.Content, $"flow-{mode}.png");
                var full = ((VisionFlowView)window.Content).OpenToolEditor(source)!; var editor = (Vision.Controls.View.VisionToolEditorView)full.Content;
                ((TabItem)editor.DetailsPanel.FindName("ResultsTab")).IsSelected = true; Snapshot(full, $"result-{mode}.png"); full.Close(); window.Close();
                Assert.True(((VisionToolInstanceWorkspace)source.Session).Editor.IsSessionActive);
                var saved = VisionToolConfiguration.FromState(source.CaptureDocumentState()).Parameters!.Value;
                Assert.False(saved.TryGetProperty("AppliedOtsuThreshold", out _)); Assert.False(saved.TryGetProperty("UsesRange", out _));
            });
            await host.Invoke(() => vm.DisposeAsync().AsTask());
            host.Invoke(() =>
            {
                Assert.True(((VisionToolInstanceWorkspace)source.Session).Model.IsClosed); Assert.True(reopened.Chart.LoadDocument(path));
                var nodes = reopened.Chart.Nodes.Select(n => n.Model).OfType<VisionToolNodeBase>().ToArray();
                source = nodes.Single(n => n.ToolIdentity == "OpenCV/Threshold"); blob = nodes.Single(n => n.ToolIdentity == "OpenCV/Blob"); tolerance = nodes.Single(n => n.ToolIdentity == "Common/Tolerance");
                yes = nodes.Single(n => n.Name == "自动阈值OK"); no = nodes.Single(n => n.Name == "自动阈值NG"); dispatch = reopened.Chart.Nodes.Select(n => n.Model).OfType<SwitchNode>().Single();
                Assert.Equal(mode, ((ThresholdParameters)source.Session.Parameters).Mode); Assert.Null(source.OutputImage); Assert.Null(tolerance.BooleanOutput);
            });
            await host.Invoke(() => reopened.RunFlowCommand.ExecuteAsync(null)); host.Invoke(() => Check(source, blob, tolerance, dispatch, yes, no, mode, accepted));
            host.Invoke(() =>
            {
                reopened.SetImage(VisionImage.FromBuffer(64, 40, PixelFormat.Mono8, new byte[64 * 40]));
                var count = mode == ThresholdMode.Otsu ? 0 : 1; var p = (ToleranceParameters)tolerance.Session.Parameters; p.Minimum = count; p.Maximum = count;
            });
            await host.Invoke(() => reopened.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                var result = Assert.IsType<ThresholdResult>(source.Result); Assert.True(result.Success); Assert.Equal(0.0, result.AppliedOtsuThreshold);
                Assert.Equal(mode == ThresholdMode.Otsu ? 0 : 2560, result.WhitePixelCount);
                Assert.Equal(mode == ThresholdMode.Otsu ? 0 : 1, Assert.IsType<BlobResult>(blob.Result).Count);
                Assert.True(Assert.IsType<ToleranceResult>(tolerance.Result).IsWithinTolerance); Assert.Equal("OK", dispatch.LastMatchedBranch); Assert.Null(no.Result);
            });
        }
        finally { host.Invoke(() => window?.Close()); await host.Invoke(() => vm.DisposeAsync().AsTask()); await host.Invoke(() => reopened.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task AutomaticModeRejectsOriginalNumericConnection_ExplicitRangeStillConsumesIt()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create); VisionToolNodeBase source = null!, statistic = null!;
        try
        {
            host.Invoke(() =>
            {
                (source, _, _, _, _, _) = Setup(vm, true); statistic = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/GrayStatistics"));
                statistic.NumericOutputField = "Minimum"; source.NumericInputParameter = "MinGray"; source.NumericSourceNodeId = statistic.Id; ((ThresholdParameters)source.Session.Parameters).Mode = ThresholdMode.Otsu;
            });
            await host.Invoke(() => statistic.ExecuteAsync(CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.Invoke(() => source.ExecuteAsync(CancellationToken.None)));
            host.Invoke(() => { Assert.Null(source.Result); Assert.Null(source.OutputImage); Assert.Contains("自动阈值", ((VisionToolInstanceWorkspace)source.Session).Editor.CurrentOperationText); ((ThresholdParameters)source.Session.Parameters).Mode = ThresholdMode.Range; });
            await host.Invoke(() => source.ExecuteAsync(CancellationToken.None));
            host.Invoke(() =>
            {
                var result = Assert.IsType<ThresholdResult>(source.Result); Assert.True(result.Success); Assert.Equal(20, result.AppliedMinGray); Assert.Equal(2548, result.WhitePixelCount);
                Assert.Equal(99, ((ThresholdParameters)source.Session.Parameters).MinGray); Assert.Equal(statistic.Id, source.NumericSourceNodeId);
            });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task InvalidModeOrColorInput_ClearsMaskCountAndOldVerdict()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create); VisionToolNodeBase source = null!, blob = null!, tolerance = null!;
        try
        {
            host.Invoke(() => { (source, blob, tolerance, _, _, _) = Setup(vm, true); ((ThresholdParameters)source.Session.Parameters).Mode = ThresholdMode.Otsu; });
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null)); host.Invoke(() => Assert.True(tolerance.BooleanOutput));
            host.Invoke(() => VisionImageBinding.Select(tolerance, null)); // Count仍要求同帧，不能借原任务灰度图消费掩膜结果。
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() => { Assert.Null(tolerance.BooleanOutput); Assert.Contains("同一帧", ((VisionToolInstanceWorkspace)tolerance.Session).Editor.CurrentOperationText); VisionImageBinding.Select(tolerance, source); ((ThresholdParameters)source.Session.Parameters).Mode = (ThresholdMode)999; });
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() => { Assert.False(Assert.IsType<ThresholdResult>(source.Result).Success); Assert.Null(source.OutputImage); Assert.Null(blob.NumericOutput); Assert.Null(tolerance.BooleanOutput); ((ThresholdParameters)source.Session.Parameters).Mode = ThresholdMode.Otsu; });
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() => { Assert.True(tolerance.BooleanOutput); vm.SetImage(VisionImage.FromBuffer(64, 40, PixelFormat.Bgr24, new byte[64 * 40 * 3])); });
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() => { Assert.Null(source.OutputImage); Assert.Null(Assert.IsType<ThresholdResult>(source.Result).AppliedOtsuThreshold); Assert.Null(blob.NumericOutput); Assert.Null(tolerance.Result); });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task LegacyFlowWithoutMode_RemainsInclusiveRange_NoAutomaticSelection()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create); var reopened = host.Invoke(VisionFlowToolboxTests.Create); var path = Artifact("legacy-range.flow");
        try
        {
            host.Invoke(() =>
            {
                var source = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/Threshold")); var p = (ThresholdParameters)source.Session.Parameters; p.MinGray = 80; p.MaxGray = 80;
                vm.SetImage(VisionImage.FromBuffer(2, 2, PixelFormat.Mono8, [0, 80, 80, 255])); Assert.True(vm.Chart.SaveDocument(path));
                var root = JsonNode.Parse(File.ReadAllText(path))!; root["nodes"]!.AsArray().Single()!["properties"]!["Configuration"]!["parameters"]!.AsObject().Remove("Mode"); File.WriteAllText(path, root.ToJsonString());
            });
            await host.Invoke(() => vm.DisposeAsync().AsTask()); host.Invoke(() => Assert.True(reopened.Chart.LoadDocument(path)));
            await host.Invoke(() => reopened.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                var source = Assert.IsAssignableFrom<VisionToolNodeBase>(Assert.Single(reopened.Chart.Nodes).Model); Assert.Equal(ThresholdMode.Range, ((ThresholdParameters)source.Session.Parameters).Mode);
                var result = Assert.IsType<ThresholdResult>(source.Result); Assert.True(result.Success); Assert.Equal(2, result.WhitePixelCount); Assert.Null(result.AppliedOtsuThreshold);
                Assert.Equal(80, result.AppliedMinGray); Assert.Equal(80, result.AppliedMaxGray); Assert.Empty(reopened.Chart.Connections);
            });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); await host.Invoke(() => reopened.DisposeAsync().AsTask()); }
    }

    private static (VisionToolNodeBase, VisionToolNodeBase, VisionToolNodeBase, SwitchNode, VisionToolNodeBase, VisionToolNodeBase) Setup(VisionFlowViewModel vm, bool accepted)
    {
        var source = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/Threshold")); var p = (ThresholdParameters)source.Session.Parameters; p.MinGray = 99; p.MaxGray = 100;
        var blob = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/Blob")); var bp = (BlobParameters)blob.Session.Parameters; bp.MinGray = 255; bp.MaxGray = 255; bp.MinArea = 1;
        VisionImageBinding.Select(blob, source); blob.NumericOutputField = "Count";
        var tolerance = vm.Add(vm.ToolEntries.Single(e => e.Name == "Common/Tolerance")); var limits = (ToleranceParameters)tolerance.Session.Parameters; limits.Unit = NumericUnit.Count; limits.Minimum = 0; limits.Maximum = accepted ? 1 : 0;
        VisionImageBinding.Select(tolerance, source);
        tolerance.NumericInputParameter = "Measurement"; tolerance.NumericSourceNodeId = blob.Id; tolerance.BooleanOutputField = "IsWithinTolerance";
        var dispatch = Assert.IsType<SwitchNode>(vm.AddNodePaletteAt(vm.ToolboxCategories.Single(c => c.Name == "流程控制").Items.Single(item => item.DisplayName == "条件分发"))); dispatch.ApplyRuleConfiguration([("true", "OK"), ("false", "NG")], true, true);
        var yes = vm.Add(vm.ToolEntries.Single(e => e.Name == "Common/Geometry")); yes.Name = "自动阈值OK";
        var no = vm.Add(vm.ToolEntries.Single(e => e.Name == "Common/Geometry")); no.Name = "自动阈值NG";
        Wire(vm, source, "Next_Out", blob, "Next_IN"); Wire(vm, blob, "Next_Out", tolerance, "Next_IN"); Wire(vm, tolerance, "Next_Out", dispatch, "Next_IN");
        Wire(vm, tolerance, "布尔结果", dispatch, "输入值"); Wire(vm, dispatch, "OK", yes, "Next_IN"); Wire(vm, dispatch, "NG", no, "Next_IN");
        source.X = 40; source.Y = 40; blob.X = 330; blob.Y = 40; tolerance.X = 620; tolerance.Y = 40; dispatch.X = 620; dispatch.Y = 340; yes.X = 40; yes.Y = 340; no.X = 330; no.Y = 340;
        var pixels = Enumerable.Repeat((byte)20, 64 * 40).ToArray(); for (var y = 12; y < 15; y++) for (var x = 10; x < 14; x++) pixels[y * 64 + x] = 180;
        vm.SetImage(VisionImage.FromBuffer(64, 40, PixelFormat.Mono8, pixels)); return (source, blob, tolerance, dispatch, yes, no);
    }
    private static void Check(VisionToolNodeBase source, VisionToolNodeBase blob, VisionToolNodeBase tolerance, SwitchNode dispatch, VisionToolNodeBase yes, VisionToolNodeBase no, ThresholdMode mode, bool accepted)
    {
        var result = Assert.IsType<ThresholdResult>(source.Result); Assert.True(result.Success, result.Message); Assert.Equal(mode, result.AppliedMode); Assert.Equal(20.0, result.AppliedOtsuThreshold);
        Assert.Equal(mode == ThresholdMode.Otsu ? 12 : 2548, result.WhitePixelCount); Assert.Same(source.OutputImage, blob.Session.Image);
        Assert.Equal(1, Assert.IsType<BlobResult>(blob.Result).Count); var verdict = Assert.IsType<ToleranceResult>(tolerance.Result); Assert.True(verdict.Success); Assert.Equal(accepted, verdict.IsWithinTolerance);
        Assert.Equal(accepted ? "OK" : "NG", dispatch.LastMatchedBranch); Assert.NotNull((accepted ? yes : no).Result); Assert.Null((accepted ? no : yes).Result);
        Assert.Equal(99, ((ThresholdParameters)source.Session.Parameters).MinGray); Assert.Equal(100, ((ThresholdParameters)source.Session.Parameters).MaxGray);
        var rows = ((VisionToolInstanceWorkspace)source.Session).Editor.CurrentCard!.TableRows!; Assert.Equal("Gray8", rows[0]["单位"]); Assert.Equal(20.0, rows[0]["自动阈值"]);
        Assert.Contains(mode.ToString(), Vision.Controls.Presentation.ToolResultText.Summarize(result));
    }
    private static PropertyItemViewModel Row(VisionFlowViewModel vm, string name) => Assert.Single(vm.PropertyGridVM.Groups.SelectMany(g => g.AllItems), r => r.PropertyName == name);
    private static void Select(FrameworkElement page, string name, object value)
    {
        page.UpdateLayout(); var combo = Assert.Single(VisionFlowR0Tests.Descendants(page).OfType<ComboBox>(), c => c.DataContext is PropertyItemViewModel r && r.PropertyName == name);
        combo.SelectedValue = value; page.UpdateLayout(); Assert.Equal(value, combo.SelectedValue); Assert.NotNull(combo.SelectedItem);
    }
    private static void Wire(VisionFlowViewModel vm, NodeModel source, string output, NodeModel target, string input) => vm.Chart.Connections.Add(new()
    { SourceNodeId = source.Id, SourcePortId = source.OutputPorts.Single(p => p.Name == output).Id, TargetNodeId = target.Id, TargetPortId = target.InputPorts.Single(p => p.Name == input).Id });
    private static string Artifact(string name) { var folder = Path.Combine(AppContext.BaseDirectory, "otsu-acceptance"); Directory.CreateDirectory(folder); return Path.Combine(folder, name); }
    private static void Snapshot(FrameworkElement view, string name)
    {
        view.UpdateLayout(); view.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)view.ActualWidth, (int)view.ActualHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(view); var png = new System.Windows.Media.Imaging.PngBitmapEncoder(); png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var stream = File.Create(Artifact(name)); png.Save(stream);
    }
}
