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
using Vision.Tools.Halcon.Measurement;
using Vision.Tools.OpenCv.Inspection;

using WPFCustomControls.PropertyGrid.Core.ViewModels;
using PixelFormat = Vision.Core.Imaging.PixelFormat;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
[Trait("Category", "Regression")]
public sealed class VisionBlobCountTests(WorkbenchAppFixture host)
{
    [Fact]
    public void CountSummary_DisplaysUnitEvenWhenNarrowResultTableNeedsHorizontalScroll()
    {
        using var result = new ToleranceResult { Success = true, IsWithinTolerance = true, AppliedMeasurement = new(0, NumericUnit.Count) };
        Assert.Contains("Count", Vision.Controls.Presentation.ToolResultText.Summarize(result));
    }

    [Fact]
    public async Task BothCatalogs_DeclareOnlyExplicitCount_WithoutCreatingNativeSession()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create);
        try { host.Invoke(() =>
        {
            foreach (var backend in new[] { "OpenCV", "HALCON" })
                Assert.Equal(new[] { "Count" }, vm.ToolEntries.Single(e => e.Name == backend + "/Blob").NumericResultFields);
            Assert.Equal(28, vm.ToolEntries.Count);
        }); }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(3, 3, true)]
    [InlineData(3, 2, false)]
    public async Task RealBlobCount_OriginalSelectorsAndRunCommand_ToleranceSwitchAndReopen(int count, int maximum, bool accepted)
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create); var reopened = host.Invoke(VisionFlowToolboxTests.Create);
        VisionToolNodeBase source = null!, tolerance = null!, yes = null!, no = null!; SwitchNode dispatch = null!;
        Window window = null!; var path = Artifact($"blob-count-{count}-{maximum}.flow");
        try
        {
            host.Invoke(() =>
            {
                var page = new VisionFlowView { DataContext = vm };
                window = new() { Content = page, Width = 1600, Height = 950, ShowInTaskbar = false, Left = -10000 }; window.Show();
                (source, tolerance, dispatch, yes, no) = Setup(vm, count, maximum);
                source.NumericOutputField = ""; tolerance.NumericInputParameter = ""; tolerance.NumericSourceNodeId = ""; tolerance.BooleanOutputField = "";
                ((ToleranceParameters)tolerance.Session.Parameters).Unit = NumericUnit.Pixel;
                vm.Select(source); Select(page, "NumericOutputField", "Count");
                vm.Select(tolerance); Select(page, "Unit", NumericUnit.Count);
                Select(page, "NumericInputParameter", "Measurement"); Select(page, "NumericSourceNodeId", source.Id);
                Select(page, "BooleanOutputField", "IsWithinTolerance");
                vm.Chart.Model.IsModified = false;
            });
            await host.Invoke(() => ((IAsyncRelayCommand)Assert.Single(VisionFlowR0Tests.Descendants(window).OfType<Button>(),
                b => System.Windows.Automation.AutomationProperties.GetName(b) == "当前流程执行").Command).ExecuteAsync(null));
            host.Invoke(() =>
            {
                Check(source, tolerance, dispatch, yes, no, count, accepted); Assert.False(vm.Chart.Model.IsModified);
                Assert.True(vm.Chart.SaveDocument(path)); Snapshot((FrameworkElement)window.Content, $"count-branch-{count}-{maximum}.png");
                var page = (VisionFlowView)window.Content; var full = page.OpenToolEditor(tolerance)!;
                var workspace = (VisionToolInstanceWorkspace)tolerance.Session;
                var editor = (Vision.Controls.View.VisionToolEditorView)full.Content;
                Assert.Same(workspace.Editor, editor.Session);
                ((TabItem)editor.DetailsPanel.FindName("ResultsTab")).IsSelected = true;
                Snapshot(full, $"count-verdict-{count}-{maximum}.png"); full.Close();
                Assert.True(workspace.Editor.IsSessionActive); window.Close();
                var config = VisionToolConfiguration.FromState(tolerance.CaptureDocumentState()).Parameters!.Value;
                Assert.Equal(maximum, config.GetProperty(nameof(ToleranceParameters.Maximum)).GetDouble());
                Assert.False(config.TryGetProperty("Measurement", out _)); Assert.False(config.TryGetProperty("AppliedMeasurement", out _));
            });
            await host.Invoke(() => vm.DisposeAsync().AsTask());
            host.Invoke(() =>
            {
                Assert.True(((VisionToolInstanceWorkspace)source.Session).Model.IsClosed);
                Assert.True(reopened.Chart.LoadDocument(path));
                source = reopened.Chart.Nodes.Select(n => n.Model).OfType<VisionToolNodeBase>().Single(n => n.ToolIdentity == "OpenCV/Blob");
                tolerance = reopened.Chart.Nodes.Select(n => n.Model).OfType<VisionToolNodeBase>().Single(n => n.ToolIdentity == "Common/Tolerance");
                yes = reopened.Chart.Nodes.Select(n => n.Model).OfType<VisionToolNodeBase>().Single(n => n.Name == "OK数量");
                no = reopened.Chart.Nodes.Select(n => n.Model).OfType<VisionToolNodeBase>().Single(n => n.Name == "NG数量");
                dispatch = reopened.Chart.Nodes.Select(n => n.Model).OfType<SwitchNode>().Single();
                Assert.Equal("Count", source.NumericOutputField); Assert.Equal(NumericUnit.Count, ((ToleranceParameters)tolerance.Session.Parameters).Unit);
                Assert.Null(source.NumericOutput); Assert.Null(tolerance.BooleanOutput); Assert.Null(tolerance.BoundNumeric);
                Assert.Equal(source.Id, tolerance.NumericSourceNodeId);
            });
            await host.Invoke(() => reopened.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                Check(source, tolerance, dispatch, yes, no, count, accepted);
                source.NumericOutputField = ""; // 未选择不等于0，不能走零数量OK或保留上轮NG。
            });
            await host.Invoke(() => reopened.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() => { Assert.Null(source.NumericOutput); Assert.Null(tolerance.BooleanOutput); Assert.Null(yes.Result); Assert.Null(no.Result); });
        }
        finally
        {
            host.Invoke(() => window?.Close()); await host.Invoke(() => vm.DisposeAsync().AsTask()); await host.Invoke(() => reopened.DisposeAsync().AsTask());
        }
    }

    [Fact]
    public async Task CountDisconnectSourceFailureStaleFrameAndDelete_CannotReuseOldZeroOrVerdict_AndEditingLocks()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create); VisionToolNodeBase source = null!, target = null!;
        try
        {
            host.Invoke(() => (source, target, _, _, _) = Setup(vm, 0, 0));
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                Assert.True(target.BooleanOutput); vm.Chart.Model.IsRunning = true;
                Assert.Throws<InvalidOperationException>(() => source.NumericOutputField = ""); vm.Chart.Model.IsRunning = false;
                var input = target.InputPorts.Single(p => p.PropertyName == nameof(VisionToolNodeBase.BoundNumeric));
                vm.Chart.Connections.Remove(vm.Chart.Connections.Single(c => c.TargetNodeId == target.Id && c.TargetPortId == input.Id));
                Assert.Null(target.BoundNumeric); Assert.Null(target.BooleanOutput);
            });
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.Invoke(() => target.ExecuteAsync(CancellationToken.None)));
            host.Invoke(() => target.NumericSourceNodeId = source.Id);
            await host.Invoke(() => source.ExecuteAsync(CancellationToken.None));
            host.Invoke(() => { var image = VisionImageSnapshot.Copy(source.Session.Image!); image.FrameId++; target.Session.SetImage(image); });
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.Invoke(() => target.ExecuteAsync(CancellationToken.None)));
            host.Invoke(() =>
            {
                Assert.Null(target.BooleanOutput); Assert.Contains("同一帧", ((VisionToolInstanceWorkspace)target.Session).Editor.CurrentOperationText);
                vm.SetImage(Frame(0)); ((VisionToolInstanceWorkspace)source.Session).Editor.BlobEditor!.MaxGray = 100; // 可输入范围内的反序阈值，交真实Validate报告失败。
            });
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                Assert.Null(source.NumericOutput); Assert.Null(target.BooleanOutput);
                vm.Chart.Nodes.Remove(vm.Chart.Nodes.Single(n => ReferenceEquals(n.Model, source)));
                Assert.Null(target.BoundNumeric);
            });
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.Invoke(() => target.ExecuteAsync(CancellationToken.None)));
            host.Invoke(() => { Assert.Null(target.BooleanOutput); Assert.Equal("Measurement", target.NumericInputParameter); });
        }
        finally { host.Invoke(() => vm.Chart.Model.IsRunning = false); await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task LegacyBlobWithoutNumericStateOrPort_RestoresOriginalParametersWithoutAutoSelectingCount()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create); var reopened = host.Invoke(VisionFlowToolboxTests.Create);
        var path = Artifact("legacy-blob-without-count.flow"); VisionToolNodeBase source = null!;
        try
        {
            host.Invoke(() =>
            {
                source = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/Blob")); vm.SetImage(Frame(3));
                ((VisionToolInstanceWorkspace)source.Session).Editor.BlobEditor!.MinArea = 10;
                Assert.True(vm.Chart.SaveDocument(path)); var root = JsonNode.Parse(File.ReadAllText(path))!;
                var node = root["nodes"]!.AsArray().Single()!; var properties = node["properties"]!.AsObject();
                properties.Remove(nameof(VisionToolNodeBase.NumericOutputField)); properties.Remove(nameof(VisionToolNodeBase.NumericInputParameter));
                var ports = node["customOutputPorts"]!.AsArray();
                foreach (var port in ports.Where(p => p!["boundProperty"]!.GetValue<string>() == nameof(VisionToolNodeBase.NumericOutput)).ToArray()) ports.Remove(port);
                File.WriteAllText(path, root.ToJsonString());
            });
            await host.Invoke(() => vm.DisposeAsync().AsTask());
            host.Invoke(() =>
            {
                Assert.True(reopened.Chart.LoadDocument(path)); source = Assert.IsAssignableFrom<VisionToolNodeBase>(Assert.Single(reopened.Chart.Nodes).Model);
                Assert.Equal("", source.NumericOutputField); Assert.Null(source.NumericOutput); Assert.Empty(reopened.Chart.Connections);
                Assert.Single(source.OutputPorts, p => p.PropertyName == nameof(VisionToolNodeBase.NumericOutput));
            });
            await host.Invoke(() => source.ExecuteAsync(CancellationToken.None));
            host.Invoke(() => { Assert.Single(Assert.IsType<BlobResult>(source.Result).Items); Assert.Null(source.NumericOutput); });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); await host.Invoke(() => reopened.DisposeAsync().AsTask()); }
    }

    private static (VisionToolNodeBase, VisionToolNodeBase, SwitchNode, VisionToolNodeBase, VisionToolNodeBase) Setup(VisionFlowViewModel vm, int count, int maximum)
    {
        var source = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/Blob")); source.Name = "筛选后目标数";
        ((VisionToolInstanceWorkspace)source.Session).Editor.BlobEditor!.MinArea = 1; source.NumericOutputField = "Count";
        var target = vm.Add(vm.ToolEntries.Single(e => e.Name == "Common/Tolerance")); var p = (ToleranceParameters)target.Session.Parameters;
        p.Unit = NumericUnit.Count; p.Minimum = 0; p.Maximum = maximum;
        target.NumericInputParameter = "Measurement"; target.NumericSourceNodeId = source.Id; target.BooleanOutputField = "IsWithinTolerance";
        var dispatch = Assert.IsType<SwitchNode>(vm.AddNodePaletteAt(vm.ToolboxCategories.Single(c => c.Name == "流程控制").Items.Single(item => item.DisplayName == "条件分发")));
        dispatch.ApplyRuleConfiguration([("true", "OK"), ("false", "NG")], true, true);
        var yes = vm.Add(vm.ToolEntries.Single(e => e.Name == "Common/Geometry")); yes.Name = "OK数量"; ((GeometryParameters)yes.Session.Parameters).FirstEnd = new(3, 4);
        var no = vm.Add(vm.ToolEntries.Single(e => e.Name == "Common/Geometry")); no.Name = "NG数量"; ((GeometryParameters)no.Session.Parameters).FirstEnd = new(5, 12);
        Wire(vm, source, "Next_Out", target, "Next_IN"); Wire(vm, target, "Next_Out", dispatch, "Next_IN");
        Wire(vm, target, "布尔结果", dispatch, "输入值"); Wire(vm, dispatch, "OK", yes, "Next_IN"); Wire(vm, dispatch, "NG", no, "Next_IN");
        source.X = 40; source.Y = 40; target.X = 300; target.Y = 40; dispatch.X = 560; dispatch.Y = 40;
        yes.X = 300; yes.Y = 310; no.X = 560; no.Y = 310;
        vm.SetImage(Frame(count)); return (source, target, dispatch, yes, no);
    }
    private static void Check(VisionToolNodeBase source, VisionToolNodeBase target, SwitchNode dispatch, VisionToolNodeBase yes, VisionToolNodeBase no, int count, bool accepted)
    {
        Assert.True(source.Result!.Success, source.Result.Message); Assert.Equal(count, Assert.IsType<BlobResult>(source.Result).Items.Count);
        Assert.Equal(count, source.NumericOutput!.Value); Assert.Equal(NumericUnit.Count, source.NumericOutput.Unit);
        var result = Assert.IsType<ToleranceResult>(target.Result); Assert.True(result.Success, result.Message);
        Assert.Equal(count, result.AppliedMeasurement!.Value); Assert.Equal(accepted, result.IsWithinTolerance);
        Assert.Equal(accepted ? "OK" : "NG", dispatch.LastMatchedBranch);
        Assert.Equal(accepted ? 5 : 13, Assert.IsType<GeometryResult>((accepted ? yes : no).Result).Distance); Assert.Null((accepted ? no : yes).Result);
        var rows = ((VisionToolInstanceWorkspace)target.Session).Editor.CurrentCard!.TableRows!;
        Assert.Equal("Count", rows[0]["单位"]); Assert.Equal(accepted, rows[0]["OK"]);
    }
    private static VisionImage Frame(int count)
    {
        var pixels = new byte[64 * 40];
        for (var i = 0; i < count; i++) for (var y = 5; y < 7 + i; y++) for (var x = 5 + 15 * i; x < 7 + 16 * i; x++) pixels[y * 64 + x] = 255;
        var image = VisionImage.FromBuffer(64, 40, PixelFormat.Mono8, pixels); image.FrameId = 201; return image;
    }
    private static void Select(FrameworkElement page, string name, object value)
    {
        page.UpdateLayout(); var combo = Assert.Single(VisionFlowR0Tests.Descendants(page).OfType<ComboBox>(), c => c.DataContext is PropertyItemViewModel r && r.PropertyName == name);
        combo.SelectedValue = value; page.UpdateLayout(); Assert.Equal(value, combo.SelectedValue);
    }
    private static void Wire(VisionFlowViewModel vm, NodeModel source, string output, NodeModel target, string input) =>
        vm.Chart.Connections.Add(new() { SourceNodeId = source.Id, SourcePortId = source.OutputPorts.Single(p => p.Name == output).Id, TargetNodeId = target.Id, TargetPortId = target.InputPorts.Single(p => p.Name == input).Id });
    private static string Artifact(string name)
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "count-acceptance"); Directory.CreateDirectory(folder); return Path.Combine(folder, name);
    }
    private static void Snapshot(FrameworkElement view, string name)
    {
        view.UpdateLayout(); view.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)view.ActualWidth, (int)view.ActualHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(view); var png = new System.Windows.Media.Imaging.PngBitmapEncoder(); png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var stream = File.Create(Artifact(name)); png.Save(stream);
    }
}
