using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using FlowEditor.Controls.Nodes;
using FlowEditor.Controls.Nodes.FlowControl;
using Vision.Core.Results;
using CommunityToolkit.Mvvm.Input;
using Vision.Controls.Services;
using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Tools;
using Vision.Tools.OpenCv.Processing;
using Vision.FlowNode;
using Vision.Tools.Halcon.Measurement;
using Vision.Tools.OpenCv.Measurement;

using WPFCustomControls.PropertyGrid.Core.ViewModels;
using PixelFormat = Vision.Core.Imaging.PixelFormat;
using FindLineParameters = Vision.Tools.OpenCv.Measurement.FindLineParameters;

namespace Vision.Workbench.Tests.Controls;
[Collection("WorkbenchAppBoot")]
[Trait("Category", "Regression")]
public sealed class VisionLinePairTests(WorkbenchAppFixture host)
{
    [Theory]
    [InlineData(LineSearchKind.EdgeBand)]
    [InlineData(LineSearchKind.Calipers)]
    public async Task TwoRealFindLines_OriginalProperties_IntersectThenConsumePoint_SaveCloseAndReopen(LineSearchKind mode)
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create); var reopened = host.Invoke(VisionFlowToolboxTests.Create);
        VisionToolNodeBase a = null!, b = null!, crossing = null!, consumer = null!; Window window = null!; var path = Artifact($"line-pair-{mode}.flow");
        try
        {
            host.Invoke(() =>
            {
                var page = new VisionFlowView { DataContext = vm }; window = new() { Content = page, Width = 1600, Height = 960, ShowInTaskbar = false, Left = -10000 }; window.Show();
                (a, b, crossing, consumer) = Setup(vm, mode);
                crossing.UseReferenceLineBinding = false; crossing.ReferenceLineSourceNodeId = ""; crossing.PointOutputField = "";
                vm.Select(crossing); Row(vm, "UseReferenceLineBinding").Value = true;
                Select(page, "ReferenceLineSourceNodeId", b.Id); Select(page, "PointOutputField", "Intersection");
                vm.Chart.Model.IsModified = false;
            });
            await host.Invoke(() => ((IAsyncRelayCommand)Assert.Single(VisionFlowR0Tests.Descendants(window).OfType<Button>(),
                button => System.Windows.Automation.AutomationProperties.GetName(button) == "当前流程执行").Command).ExecuteAsync(null));
            host.Invoke(() =>
            {
                Check(crossing, consumer); Assert.False(vm.Chart.Model.IsModified); Assert.True(vm.Chart.SaveDocument(path));
                Snapshot((FrameworkElement)window.Content, $"pair-{mode}.png");
                var full = ((VisionFlowView)window.Content).OpenToolEditor(crossing)!;
                var editor = (Vision.Controls.View.VisionToolEditorView)full.Content; ((TabItem)editor.DetailsPanel.FindName("ResultsTab")).IsSelected = true;
                Snapshot(full, $"intersection-{mode}.png"); full.Close(); window.Close();
                Assert.True(((VisionToolInstanceWorkspace)crossing.Session).Editor.IsSessionActive);
            });
            await host.Invoke(() => vm.DisposeAsync().AsTask());
            host.Invoke(() =>
            {
                Assert.True(((VisionToolInstanceWorkspace)crossing.Session).Model.IsClosed); Assert.True(reopened.Chart.LoadDocument(path));
                crossing = reopened.Chart.Nodes.Select(n => n.Model).OfType<VisionToolNodeBase>().Single(n => n.Name == "双线求交");
                consumer = reopened.Chart.Nodes.Select(n => n.Model).OfType<VisionToolNodeBase>().Single(n => n.Name == "交点到参考点");
                Assert.True(crossing.UseReferenceLineBinding); Assert.Equal(b.Id, crossing.ReferenceLineSourceNodeId);
                Assert.Null(crossing.PointOutput); Assert.Null(crossing.BoundReferenceLine); Assert.Null(consumer.Result);
            });
            await host.Invoke(() => reopened.RunFlowCommand.ExecuteAsync(null)); host.Invoke(() => Check(crossing, consumer));
        }
        finally { host.Invoke(() => window?.Close()); await host.Invoke(() => vm.DisposeAsync().AsTask()); await host.Invoke(() => reopened.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task ParallelMissingFailedDisconnectedAndDeletedReference_CannotPublishOldPoint_AndEditingLocks()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create); VisionToolNodeBase a = null!, b = null!, crossing = null!, consumer = null!;
        try
        {
            host.Invoke(() => (a, b, crossing, consumer) = Setup(vm, LineSearchKind.EdgeBand));
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null)); host.Invoke(() => Check(crossing, consumer));
            host.Invoke(() => crossing.ReferenceLineSourceNodeId = a.Id); // 同线：无唯一交点，不允许拿(0,0)代替。
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() => { Assert.False(Assert.IsType<GeometryResult>(crossing.Result).Success); Assert.Null(crossing.PointOutput); Assert.Null(consumer.Result); crossing.ReferenceLineSourceNodeId = b.Id; });
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                Check(crossing, consumer); vm.Chart.Model.IsRunning = true;
                Assert.Throws<InvalidOperationException>(() => crossing.UseReferenceLineBinding = false);
                Assert.Throws<InvalidOperationException>(() => crossing.ReferenceLineSourceNodeId = a.Id); vm.Chart.Model.IsRunning = false;
                var port = crossing.InputPorts.Single(p => p.PropertyName == nameof(VisionToolNodeBase.BoundReferenceLine));
                vm.Chart.Connections.Remove(vm.Chart.Connections.Single(e => e.TargetNodeId == crossing.Id && e.TargetPortId == port.Id));
                Assert.Null(crossing.BoundReferenceLine); Assert.Null(crossing.PointOutput); Assert.True(crossing.UseReferenceLineBinding);
            });
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.Invoke(() => crossing.ExecuteAsync(CancellationToken.None)));
            host.Invoke(() => { crossing.ReferenceLineSourceNodeId = b.Id; crossing.UseReferenceLineBinding = false; });
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.Invoke(() => crossing.ExecuteAsync(CancellationToken.None)));
            host.Invoke(() => { crossing.UseReferenceLineBinding = true; ((FindLineParameters)b.Session.Parameters).MinEdgePoints = int.MaxValue; });
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() => { Assert.Null(b.LineOutput); Assert.Null(crossing.PointOutput); Assert.Null(consumer.Result); ((FindLineParameters)b.Session.Parameters).MinEdgePoints = 30; });
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() => { Check(crossing, consumer); vm.Chart.Nodes.Remove(vm.Chart.Nodes.Single(n => ReferenceEquals(n.Model, b))); Assert.Null(crossing.PointOutput); Assert.Null(crossing.BoundReferenceLine); });
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.Invoke(() => crossing.ExecuteAsync(CancellationToken.None)));
        }
        finally { host.Invoke(() => vm.Chart.Model.IsRunning = false); await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task ReferenceLineFromDifferentFrameOrIdenticalButDistinctCrop_IsRejectedWithoutChangingRecipe()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create); VisionToolNodeBase a = null!, b = null!, crossing = null!, left = null!, right = null!;
        try
        {
            host.Invoke(() => { (a, b, crossing, _) = Setup(vm, LineSearchKind.EdgeBand); });
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() => { var frame = VisionImageSnapshot.Copy(b.Session.ConfiguredImage!); frame.FrameId++; b.Session.SetImage(frame); });
            await host.Invoke(() => b.ExecuteAsync(CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.Invoke(() => crossing.ExecuteAsync(CancellationToken.None)));
            host.Invoke(() =>
            {
                Assert.Null(crossing.PointOutput); Assert.Null(crossing.Result); Assert.Contains("同一帧", ((VisionToolInstanceWorkspace)crossing.Session).Editor.CurrentOperationText);
                var frame = a.Session.ConfiguredImage!; var pixels = new byte[440 * 160];
                for (var y = 0; y < 160; y++) { Array.Copy(frame.Pixels, y * 220, pixels, y * 440, 220); Array.Copy(frame.Pixels, y * 220, pixels, y * 440 + 220, 220); }
                vm.SetImage(VisionImage.FromBuffer(440, 160, PixelFormat.Mono8, pixels));
                left = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/Crop")); right = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/Crop"));
                foreach (var crop in new[] { left, right }) { var p = (CropParameters)crop.Session.Parameters; p.Width = 220; p.Height = 160; p.X = ReferenceEquals(crop, left) ? 0 : 220; p.Y = 0; }
                VisionImageBinding.Select(a, left); VisionImageBinding.Select(b, right); VisionImageBinding.Select(crossing, left);
            });
            await host.Invoke(() => left.ExecuteAsync(CancellationToken.None)); await host.Invoke(() => right.ExecuteAsync(CancellationToken.None));
            await host.Invoke(() => a.ExecuteAsync(CancellationToken.None)); await host.Invoke(() => b.ExecuteAsync(CancellationToken.None));
            host.Invoke(() => Assert.Equal(VisionToolConfiguration.ImageIdentity(left.OutputImage!), VisionToolConfiguration.ImageIdentity(right.OutputImage!)));
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.Invoke(() => crossing.ExecuteAsync(CancellationToken.None)));
            host.Invoke(() => { Assert.Null(crossing.PointOutput); Assert.Contains("坐标来源", ((VisionToolInstanceWorkspace)crossing.Session).Editor.CurrentOperationText); VisionImageBinding.Select(b, left); });
            await host.Invoke(() => b.ExecuteAsync(CancellationToken.None)); await host.Invoke(() => crossing.ExecuteAsync(CancellationToken.None));
            host.Invoke(() => { Assert.NotNull(crossing.PointOutput); Assert.Equal(new Point2D(2000, 2000), ((GeometryParameters)crossing.Session.Parameters).SecondStart); });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task LegacyManualIntersection_WithoutReferenceLineStateOrPort_RemainsManualUnknownUnit()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create); var reopened = host.Invoke(VisionFlowToolboxTests.Create); var path = Artifact("legacy-manual-intersection.flow");
        try
        {
            host.Invoke(() =>
            {
                var node = vm.Add(vm.ToolEntries.Single(e => e.Name == "Common/Geometry")); var p = (GeometryParameters)node.Session.Parameters;
                p.Operation = GeometryOperation.LineIntersection; p.FirstStart = new(0, 0); p.FirstEnd = new(10, 10); p.SecondStart = new(0, 10); p.SecondEnd = new(10, 0);
                Assert.True(vm.Chart.SaveDocument(path)); var root = JsonNode.Parse(File.ReadAllText(path))!; var item = root["nodes"]!.AsArray().Single()!;
                item["properties"]!.AsObject().Remove(nameof(VisionToolNodeBase.UseReferenceLineBinding));
                var ports = item["customInputPorts"]!.AsArray();
                foreach (var port in ports.Where(n => n!["boundProperty"]!.GetValue<string>() == nameof(VisionToolNodeBase.BoundReferenceLine)).ToArray()) ports.Remove(port);
                File.WriteAllText(path, root.ToJsonString());
            });
            await host.Invoke(() => vm.DisposeAsync().AsTask()); host.Invoke(() => Assert.True(reopened.Chart.LoadDocument(path)));
            var node = host.Invoke(() => Assert.IsAssignableFrom<VisionToolNodeBase>(Assert.Single(reopened.Chart.Nodes).Model));
            await host.Invoke(() => node.ExecuteAsync(CancellationToken.None));
            host.Invoke(() => { Assert.False(node.UseReferenceLineBinding); Assert.Equal(new Point2D(5, 5), Assert.IsType<GeometryResult>(node.Result).Intersection); Assert.Null(node.PointOutput); Assert.Empty(reopened.Chart.Connections); });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); await host.Invoke(() => reopened.DisposeAsync().AsTask()); }
    }

    [Theory]
    [InlineData(LineSearchKind.Calipers, true)]
    [InlineData(LineSearchKind.Calipers, false)]
    [InlineData(LineSearchKind.EdgeBand, true)]
    [InlineData(LineSearchKind.EdgeBand, false)]
    public async Task RealLineAngle_OriginalSelectors_DegreeToleranceSwitch_ReopenAndParallelZero(LineSearchKind mode, bool accepted)
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create); var reopened = host.Invoke(VisionFlowToolboxTests.Create);
        VisionToolNodeBase a = null!, b = null!, geometry = null!, tolerance = null!, yes = null!, no = null!; SwitchNode dispatch = null!;
        Window window = null!; var path = Artifact($"angle-{mode}-{accepted}.flow");
        try
        {
            host.Invoke(() =>
            {
                var page = new VisionFlowView { DataContext = vm }; window = new() { Content = page, Width = 1600, Height = 960, ShowInTaskbar = false, Left = -10000 }; window.Show();
                (a, b, geometry, tolerance) = Setup(vm, mode, angle: true);
                (dispatch, yes, no) = AddAngleBranches(vm, tolerance); ((ToleranceParameters)tolerance.Session.Parameters).Maximum = accepted ? 90 : 89.5;
                ((GeometryParameters)geometry.Session.Parameters).Operation = GeometryOperation.LineIntersection;
                geometry.NumericOutputField = ""; vm.Select(geometry);
                Select(page, "Operation", GeometryOperation.LineAngle); Select(page, "NumericOutputField", "AngleDegrees");
                ((ToleranceParameters)tolerance.Session.Parameters).Unit = NumericUnit.Pixel;
                vm.Select(tolerance); Select(page, "Unit", NumericUnit.Degree); Select(page, "NumericSourceNodeId", geometry.Id);
                vm.Chart.Model.IsModified = false;
            });
            await host.Invoke(() => ((IAsyncRelayCommand)Assert.Single(VisionFlowR0Tests.Descendants(window).OfType<Button>(),
                button => System.Windows.Automation.AutomationProperties.GetName(button) == "当前流程执行").Command).ExecuteAsync(null));
            host.Invoke(() =>
            {
                CheckAngle(geometry, tolerance, dispatch, yes, no, accepted); Assert.False(vm.Chart.Model.IsModified); Assert.True(vm.Chart.SaveDocument(path));
                vm.Chart.ZoomToFitCommand.Execute(null); Snapshot((FrameworkElement)window.Content, $"angle-flow-{mode}-{accepted}.png");
                var saved = VisionToolConfiguration.FromState(geometry.CaptureDocumentState()).Parameters!.Value;
                Assert.False(saved.TryGetProperty(nameof(GeometryResult.AngleDegrees), out _)); Assert.False(saved.TryGetProperty(nameof(GeometryResult.AppliedReferenceLine), out _));
                var full = ((VisionFlowView)window.Content).OpenToolEditor(geometry)!;
                ((TabItem)((Vision.Controls.View.VisionToolEditorView)full.Content).DetailsPanel.FindName("ResultsTab")).IsSelected = true;
                Snapshot(full, $"angle-result-{mode}-{accepted}.png"); full.Close(); window.Close();
                Assert.True(((VisionToolInstanceWorkspace)geometry.Session).Editor.IsSessionActive);
            });
            await host.Invoke(() => vm.DisposeAsync().AsTask());
            host.Invoke(() =>
            {
                Assert.True(((VisionToolInstanceWorkspace)geometry.Session).Model.IsClosed); Assert.True(reopened.Chart.LoadDocument(path));
                var nodes = reopened.Chart.Nodes.Select(n => n.Model).OfType<VisionToolNodeBase>().ToArray();
                geometry = nodes.Single(n => n.Name == "双线夹角"); tolerance = nodes.Single(n => n.ToolIdentity == "Common/Tolerance");
                a = nodes.Single(n => n.Name == "水平支撑线"); yes = nodes.Single(n => n.Name == "角度OK"); no = nodes.Single(n => n.Name == "角度NG");
                dispatch = reopened.Chart.Nodes.Select(n => n.Model).OfType<SwitchNode>().Single();
                Assert.Null(geometry.NumericOutput); Assert.Null(tolerance.BooleanOutput); Assert.Null(geometry.BoundReferenceLine);
                Assert.Equal(GeometryOperation.LineAngle, ((GeometryParameters)geometry.Session.Parameters).Operation);
                Assert.Equal(NumericUnit.Degree, ((ToleranceParameters)tolerance.Session.Parameters).Unit);
            });
            await host.Invoke(() => reopened.RunFlowCommand.ExecuteAsync(null)); host.Invoke(() => CheckAngle(geometry, tolerance, dispatch, yes, no, accepted));
            host.Invoke(() => { geometry.ReferenceLineSourceNodeId = a.Id; var p = (ToleranceParameters)tolerance.Session.Parameters; p.Minimum = 0; p.Maximum = 0; });
            await host.Invoke(() => reopened.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                Assert.Equal(0, geometry.NumericOutput!.Value); Assert.True(Assert.IsType<ToleranceResult>(tolerance.Result).IsWithinTolerance);
                Assert.Equal("OK", dispatch.LastMatchedBranch); Assert.NotNull(yes.Result); Assert.Null(no.Result);
            });
        }
        finally { host.Invoke(() => window?.Close()); await host.Invoke(() => vm.DisposeAsync().AsTask()); await host.Invoke(() => reopened.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task AngleMissingFailedOrWrongUnit_IsNotZeroOrOldVerdict_AndDegreeStillRequiresDirectAnchor()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create); VisionToolNodeBase a = null!, b = null!, geometry = null!, tolerance = null!, crop = null!;
        try
        {
            host.Invoke(() => (a, b, geometry, tolerance) = Setup(vm, LineSearchKind.EdgeBand, angle: true));
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() => { Assert.NotNull(tolerance.BooleanOutput); ((ToleranceParameters)tolerance.Session.Parameters).Unit = NumericUnit.Pixel; });
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() => { Assert.Null(tolerance.Result); Assert.Null(tolerance.BooleanOutput); Assert.Contains("单位", ((VisionToolInstanceWorkspace)tolerance.Session).Editor.CurrentOperationText); ((ToleranceParameters)tolerance.Session.Parameters).Unit = NumericUnit.Degree; });
            host.Invoke(() => geometry.ReferenceLineSourceNodeId = "");
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() => { Assert.True(geometry.UseReferenceLineBinding); Assert.Null(geometry.NumericOutput); Assert.Null(tolerance.BooleanOutput); geometry.ReferenceLineSourceNodeId = b.Id; ((FindLineParameters)b.Session.Parameters).MinEdgePoints = int.MaxValue; });
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() => { Assert.Null(b.LineOutput); Assert.Null(geometry.NumericOutput); Assert.Null(tolerance.Result); ((FindLineParameters)b.Session.Parameters).MinEdgePoints = 30; });
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                Assert.NotNull(geometry.NumericOutput);
                crop = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/Crop")); var p = (CropParameters)crop.Session.Parameters; p.X = 0; p.Y = 0; p.Width = 220; p.Height = 160;
                VisionImageBinding.Select(tolerance, crop);
            });
            await host.Invoke(() => crop.ExecuteAsync(CancellationToken.None));
            host.Invoke(() => Assert.Equal(VisionToolConfiguration.ImageIdentity(crop.OutputImage!), geometry.NumericOutput!.ImageIdentity));
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.Invoke(() => tolerance.ExecuteAsync(CancellationToken.None)));
            host.Invoke(() => { Assert.Null(tolerance.BooleanOutput); Assert.Contains("坐标来源", ((VisionToolInstanceWorkspace)tolerance.Session).Editor.CurrentOperationText); });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    private static (SwitchNode, VisionToolNodeBase, VisionToolNodeBase) AddAngleBranches(VisionFlowViewModel vm, VisionToolNodeBase tolerance)
    {
        var dispatch = Assert.IsType<SwitchNode>(vm.AddNodePaletteAt(vm.ToolboxCategories.Single(c => c.Name == "流程控制").Items.Single(item => item.DisplayName == "条件分发")));
        dispatch.ApplyRuleConfiguration([("true", "OK"), ("false", "NG")], true, true);
        var yes = vm.Add(vm.ToolEntries.Single(e => e.Name == "Common/Geometry")); yes.Name = "角度OK";
        var no = vm.Add(vm.ToolEntries.Single(e => e.Name == "Common/Geometry")); no.Name = "角度NG";
        Wire(vm, tolerance, dispatch, "Next_Out", "Next_IN"); Wire(vm, tolerance, dispatch, "布尔结果", "输入值");
        Wire(vm, dispatch, yes, "OK", "Next_IN"); Wire(vm, dispatch, no, "NG", "Next_IN");
        dispatch.X = 680; dispatch.Y = 40; yes.X = 360; yes.Y = 350; no.X = 680; no.Y = 350;
        tolerance.X = 360; tolerance.Y = 600; return (dispatch, yes, no);
    }
    private static void Wire(VisionFlowViewModel vm, NodeModel from, NodeModel to, string output, string input) => vm.Chart.Connections.Add(new()
    { SourceNodeId = from.Id, SourcePortId = from.OutputPorts.Single(p => p.Name == output).Id, TargetNodeId = to.Id, TargetPortId = to.InputPorts.Single(p => p.Name == input).Id });
    private static void CheckAngle(VisionToolNodeBase geometry, VisionToolNodeBase tolerance, SwitchNode dispatch, VisionToolNodeBase yes, VisionToolNodeBase no, bool accepted)
    {
        var angle = Assert.IsType<GeometryResult>(geometry.Result); Assert.True(angle.Success, angle.Message); Assert.InRange(angle.AngleDegrees, 89.9, 90);
        Assert.Equal(NumericUnit.Degree, geometry.NumericOutput!.Unit); Assert.Null(geometry.PointOutput);
        var verdict = Assert.IsType<ToleranceResult>(tolerance.Result); Assert.True(verdict.Success); Assert.Equal(accepted, verdict.IsWithinTolerance);
        Assert.Equal(angle.AngleDegrees, verdict.AppliedMeasurement!.Value); Assert.Equal(NumericUnit.Degree, verdict.AppliedMeasurement.Unit);
        Assert.Equal(accepted ? "OK" : "NG", dispatch.LastMatchedBranch); Assert.NotNull((accepted ? yes : no).Result); Assert.Null((accepted ? no : yes).Result);
        var p = (GeometryParameters)geometry.Session.Parameters; Assert.Equal(new Point2D(1000, 1000), p.FirstStart); Assert.Equal(new Point2D(2000, 2000), p.SecondStart);
        Assert.Equal(new Point2D(1005, 1005), p.FirstEnd); Assert.Equal(new Point2D(2005, 2005), p.SecondEnd);
        Assert.Equal("Degree", ((VisionToolInstanceWorkspace)geometry.Session).Editor.CurrentCard!.TableRows![0]["单位"]);
        Assert.Equal("Degree", ((VisionToolInstanceWorkspace)tolerance.Session).Editor.CurrentCard!.TableRows![0]["单位"]);
        Assert.Contains("Degree", Vision.Controls.Presentation.ToolResultText.Summarize(angle));
    }

    private static (VisionToolNodeBase, VisionToolNodeBase, VisionToolNodeBase, VisionToolNodeBase) Setup(VisionFlowViewModel vm, LineSearchKind mode, bool angle = false)
    {
        var a = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/FindLine")); a.Name = "水平支撑线";
        var b = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/FindLine")); b.Name = "竖直支撑线";
        Configure((FindLineParameters)a.Session.Parameters, mode, new(120, 60), new(200, 60));
        Configure((FindLineParameters)b.Session.Parameters, mode, new(100, 80), new(100, 140));
        a.LineOutputField = "Line"; b.LineOutputField = "Line";
        var crossing = vm.Add(vm.ToolEntries.Single(e => e.Name == "Common/Geometry")); crossing.Name = angle ? "双线夹角" : "双线求交";
        var p = (GeometryParameters)crossing.Session.Parameters; p.Operation = angle ? GeometryOperation.LineAngle : GeometryOperation.LineIntersection;
        p.FirstStart = new(1000, 1000); p.FirstEnd = new(1005, 1005); p.SecondStart = new(2000, 2000); p.SecondEnd = new(2005, 2005);
        crossing.LineInputParameter = "FirstLine"; crossing.LineSourceNodeId = a.Id; crossing.UseReferenceLineBinding = true; crossing.ReferenceLineSourceNodeId = b.Id; crossing.PointOutputField = angle ? "" : "Intersection";
        var consumer = vm.Add(vm.ToolEntries.Single(e => e.Name == (angle ? "Common/Tolerance" : "Common/Geometry"))); consumer.Name = angle ? "角度公差" : "交点到参考点";
        if (angle)
        {
            crossing.NumericOutputField = "AngleDegrees"; var limits = (ToleranceParameters)consumer.Session.Parameters; limits.Unit = NumericUnit.Degree; limits.Minimum = 89; limits.Maximum = 90;
            consumer.NumericInputParameter = "Measurement"; consumer.NumericSourceNodeId = crossing.Id; consumer.BooleanOutputField = "IsWithinTolerance";
        }
        else
        {
            ((GeometryParameters)consumer.Session.Parameters).FirstEnd = new(129.5, 99.5); consumer.PointInputParameter = "FirstStart"; consumer.PointSourceNodeId = crossing.Id;
        }
        Wire(vm, a, b); Wire(vm, b, crossing); Wire(vm, crossing, consumer);
        a.X = 40; a.Y = 40; b.X = 40; b.Y = 330; crossing.X = 360; crossing.Y = 40; consumer.X = 680; consumer.Y = 330;
        var pixels = new byte[220 * 160]; for (var y = 60; y < 160; y++) for (var x = 100; x < 220; x++) pixels[y * 220 + x] = 255;
        var image = VisionImage.FromBuffer(220, 160, PixelFormat.Mono8, pixels); image.FrameId = 501; vm.SetImage(image);
        return (a, b, crossing, consumer);
    }
    private static void Configure(FindLineParameters p, LineSearchKind mode, Point2D start, Point2D end)
    {
        p.SearchMode = mode; p.LineStart = start; p.LineEnd = end; p.BandHalfWidth = 4; p.MinEdgePoints = 30; p.CannyLow = 30; p.CannyHigh = 80;
        p.CaliperCount = 9; p.CaliperHalfLength = 8; p.CaliperHalfWidth = 2; p.Threshold = 20;
    }
    private static void Check(VisionToolNodeBase crossing, VisionToolNodeBase consumer)
    {
        var result = Assert.IsType<GeometryResult>(crossing.Result); Assert.True(result.Success, result.Message); var point = result.Intersection!.Value;
        Assert.InRange(point.X, 98, 101); Assert.InRange(point.Y, 58, 61); // 独立像素边界预期，Canny整数/卡尺亚像素不强行一致。
        Assert.Equal(point, crossing.PointOutput!.Position); Assert.NotNull(result.AppliedReferenceLine);
        var distance = Assert.IsType<GeometryResult>(consumer.Result); Assert.True(distance.Success, distance.Message); Assert.InRange(distance.Distance, 48, 52);
        Assert.Equal(crossing.PointOutput, distance.AppliedPoint);
        var p = (GeometryParameters)crossing.Session.Parameters; Assert.Equal(new Point2D(1000, 1000), p.FirstStart); Assert.Equal(new Point2D(2000, 2000), p.SecondStart);
        Assert.Equal("Pixel", ((VisionToolInstanceWorkspace)crossing.Session).Editor.CurrentCard!.TableRows![0]["单位"]);
    }
    private static PropertyItemViewModel Row(VisionFlowViewModel vm, string name) => Assert.Single(vm.PropertyGridVM.Groups.SelectMany(g => g.Items), r => r.PropertyName == name);
    private static void Select(FrameworkElement page, string name, object value)
    {
        page.UpdateLayout(); var combo = Assert.Single(VisionFlowR0Tests.Descendants(page).OfType<ComboBox>(), c => c.DataContext is PropertyItemViewModel r && r.PropertyName == name);
        combo.SelectedValue = value; page.UpdateLayout(); combo.BringIntoView(); page.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        Assert.Equal(value, combo.SelectedValue); Assert.NotNull(combo.SelectedItem);
        if (name == "ReferenceLineSourceNodeId") Assert.Contains(VisionFlowR0Tests.Descendants(combo).OfType<TextBlock>(), t => t.Text.Contains("竖直支撑线"));
    }
    private static void Wire(VisionFlowViewModel vm, VisionToolNodeBase from, VisionToolNodeBase to) => vm.Chart.Connections.Add(new()
    { SourceNodeId = from.Id, SourcePortId = from.OutputPorts.Single(p => p.Name == "Next_Out").Id, TargetNodeId = to.Id, TargetPortId = to.InputPorts.Single(p => p.Name == "Next_IN").Id });
    private static string Artifact(string name) { var folder = Path.Combine(AppContext.BaseDirectory, "line-pair-acceptance"); Directory.CreateDirectory(folder); return Path.Combine(folder, name); }
    private static void Snapshot(FrameworkElement view, string name)
    {
        view.UpdateLayout(); view.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)view.ActualWidth, (int)view.ActualHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(view); var png = new System.Windows.Media.Imaging.PngBitmapEncoder(); png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var stream = File.Create(Artifact(name)); png.Save(stream);
    }
}
