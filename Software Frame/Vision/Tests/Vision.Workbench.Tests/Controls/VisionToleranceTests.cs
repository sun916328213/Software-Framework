using System.IO;
using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.Input;
using FlowEditor.Controls.Nodes;
using FlowEditor.Controls.Nodes.FlowControl;
using FlowEditor.Controls.ViewModels;
using Vision.Controls.Services;
using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Tools;
using Vision.FlowNode;
using Vision.Tools.Halcon.Measurement;

using WPFCustomControls.PropertyGrid.Core.ViewModels;
using PixelFormat = Vision.Core.Imaging.PixelFormat;

namespace Vision.Workbench.Tests.Controls;
[Collection("WorkbenchAppBoot")]
[Trait("Category", "Regression")]
public sealed class VisionToleranceTests(WorkbenchAppFixture host)
{
    [Fact]
    public async Task OriginalCatalog_OffersExplicitTolerance_NotConstantBoolFallback()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create);
        try { host.Invoke(() => Assert.Single(vm.ToolEntries, e => e.Name == "Common/Tolerance")); }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ActualCircleGeometryTolerance_UsesOriginalBooleanBranch_AndReopens(bool accepted)
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create); var reopened = host.Invoke(VisionFlowToolboxTests.Create);
        VisionToolNodeBase source = null!, geometry = null!, tolerance = null!, yes = null!, no = null!; ConditionNode condition = null!;
        Window window = null!; var path = Artifact($"tolerance-{accepted}.flow");
        try
        {
            host.Invoke(() =>
            {
                var page = new VisionFlowView { DataContext = vm };
                window = new() { Content = page, Width = 1600, Height = 950, ShowInTaskbar = false, Left = -10000 }; window.Show();
                (source, geometry, tolerance, condition, yes, no) = Setup(vm, accepted);
                vm.Select(geometry); Select(page, "NumericOutputField", "Diameter");
                vm.Select(tolerance); Select(page, "NumericInputParameter", "Measurement");
                Select(page, "NumericSourceNodeId", geometry.Id); Select(page, "BooleanOutputField", "IsWithinTolerance");
                vm.Chart.Model.IsModified = false;
            });
            await host.Invoke(() => ((IAsyncRelayCommand)Assert.Single(VisionFlowR0Tests.Descendants(window).OfType<Button>(),
                b => System.Windows.Automation.AutomationProperties.GetName(b) == "当前流程执行").Command).ExecuteAsync(null));
            host.Invoke(() =>
            {
                Check(tolerance, condition, yes, no, accepted); Assert.False(vm.Chart.Model.IsModified);
                var measure = Assert.IsType<GeometryResult>(geometry.Result);
                Assert.InRange(measure.Diameter, 56, 64); Assert.Equal(measure.Diameter, Assert.IsType<ToleranceResult>(tolerance.Result).AppliedMeasurement!.Value);
                Assert.True(vm.Chart.SaveDocument(path));
                Snapshot((FrameworkElement)window.Content, $"branch-{accepted}.png");
                var full = ((VisionFlowView)window.Content).OpenToolEditor(tolerance)!;
                var editor = (Vision.Controls.View.VisionToolEditorView)full.Content;
                ((TabItem)editor.DetailsPanel.FindName("ResultsTab")).IsSelected = true;
                Snapshot(full, $"verdict-{accepted}.png"); full.Close(); window.Close();
            });
            await host.Invoke(() => vm.DisposeAsync().AsTask());
            host.Invoke(() =>
            {
                Assert.True(reopened.Chart.LoadDocument(path));
                var nodes = reopened.Chart.Nodes.Select(n => n.Model).ToArray();
                tolerance = nodes.OfType<VisionToolNodeBase>().Single(n => n.ToolIdentity == "Common/Tolerance");
                geometry = nodes.OfType<VisionToolNodeBase>().Single(n => n.Name == "测量圆直径");
                condition = nodes.OfType<ConditionNode>().Single();
                yes = nodes.OfType<VisionToolNodeBase>().Single(n => n.Name == "OK分支"); no = nodes.OfType<VisionToolNodeBase>().Single(n => n.Name == "NG分支");
                Assert.Null(tolerance.BooleanOutput); Assert.Null(tolerance.Result);
                Assert.Equal("Measurement", tolerance.NumericInputParameter); Assert.Equal("IsWithinTolerance", tolerance.BooleanOutputField);
            });
            await host.Invoke(() => reopened.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                Check(tolerance, condition, yes, no, accepted);
                tolerance.BooleanOutputField = ""; // 不能让原分支拾取上轮Condition或常量。
            });
            await host.Invoke(() => reopened.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                Assert.Null(tolerance.BooleanOutput); Assert.Null(tolerance.Result); Assert.Null(yes.Result); Assert.Null(no.Result);
                Assert.Contains("布尔", ((VisionToolInstanceWorkspace)tolerance.Session).Editor.CurrentOperationText);
            });
        }
        finally
        {
            host.Invoke(() => window?.Close()); await host.Invoke(() => vm.DisposeAsync().AsTask()); await host.Invoke(() => reopened.DisposeAsync().AsTask());
        }
    }

    [Fact]
    public async Task ZeroMeasurement_IsOk_ThenMissingAndDisconnectedSourceNeverUseOldVerdict()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create);
        VisionToolNodeBase source = null!, geometry = null!, tolerance = null!;
        try
        {
            host.Invoke(() => (source, geometry, tolerance, _, _, _) = Setup(vm, true));
            await host.Invoke(() => source.ExecuteAsync(CancellationToken.None));
            host.Invoke(() =>
            {
                geometry.CircleInputParameter = ""; geometry.CircleSourceNodeId = "";
                geometry.PointInputParameter = "FirstStart"; geometry.PointSourceNodeId = source.Id; source.PointOutputField = "Center";
                geometry.NumericOutputField = "Distance";
            });
            await host.Invoke(() => source.ExecuteAsync(CancellationToken.None)); // 字段修改按原规则撤旧结果。
            host.Invoke(() =>
            {
                ((GeometryParameters)geometry.Session.Parameters).Operation = GeometryOperation.Distance;
                ((GeometryParameters)geometry.Session.Parameters).FirstEnd = source.PointOutput!.Position;
                ((ToleranceParameters)tolerance.Session.Parameters).Maximum = 0;
            });
            await host.Invoke(() => geometry.ExecuteAsync(CancellationToken.None));
            await host.Invoke(() => tolerance.ExecuteAsync(CancellationToken.None));
            host.Invoke(() =>
            {
                var result = Assert.IsType<ToleranceResult>(tolerance.Result); Assert.True(result.Success, result.Message);
                Assert.Equal(0, result.AppliedMeasurement!.Value); Assert.True(result.IsWithinTolerance); Assert.True(tolerance.BooleanOutput);
                var input = tolerance.InputPorts.Single(p => p.PropertyName == nameof(VisionToolNodeBase.BoundNumeric));
                vm.Chart.Connections.Remove(vm.Chart.Connections.Single(c => c.TargetNodeId == tolerance.Id && c.TargetPortId == input.Id));
                Assert.Null(tolerance.BooleanOutput); Assert.Null(tolerance.BoundNumeric);
            });
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.Invoke(() => tolerance.ExecuteAsync(CancellationToken.None)));
            host.Invoke(() =>
            {
                Assert.Null(tolerance.Result); Assert.Null(tolerance.BooleanOutput);
                Assert.Equal("Measurement", tolerance.NumericInputParameter);
            });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task StaleFrameOrGrayUnit_CannotEnterPixelTolerance_AndRunningEditsAreLocked()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create); VisionToolNodeBase geometry = null!, tolerance = null!;
        try
        {
            host.Invoke(() => (_, geometry, tolerance, _, _, _) = Setup(vm, true));
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                var other = VisionImageSnapshot.Copy(geometry.Session.Image!); other.FrameId++; tolerance.Session.SetImage(other);
            });
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.Invoke(() => tolerance.ExecuteAsync(CancellationToken.None)));
            host.Invoke(() =>
            {
                Assert.Null(tolerance.BooleanOutput); Assert.Contains("同一帧", ((VisionToolInstanceWorkspace)tolerance.Session).Editor.CurrentOperationText);
                vm.Chart.Model.IsRunning = true;
                Assert.Throws<InvalidOperationException>(() => tolerance.BooleanOutputField = "");
                vm.Chart.Model.IsRunning = false;
                var stats = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/GrayStatistics"));
                stats.NumericOutputField = "Minimum"; tolerance.NumericSourceNodeId = stats.Id; tolerance.Session.SetImage(geometry.Session.ConfiguredImage);
                stats.Session.SetImage(geometry.Session.ConfiguredImage);
            });
            var statsNode = host.Invoke(() => vm.Chart.Nodes.Select(n => n.Model).OfType<VisionToolNodeBase>().Single(n => n.ToolIdentity == "OpenCV/GrayStatistics"));
            await host.Invoke(() => statsNode.ExecuteAsync(CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.Invoke(() => tolerance.ExecuteAsync(CancellationToken.None)));
            host.Invoke(() => { Assert.Null(tolerance.BooleanOutput); Assert.Contains("单位", ((VisionToolInstanceWorkspace)tolerance.Session).Editor.CurrentOperationText); });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task PixelLengthFromOneCrop_CannotUseAnotherCropEvenWithIdenticalPixels()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create);
        VisionToolNodeBase source = null!, geometry = null!, tolerance = null!, a = null!, b = null!;
        try
        {
            host.Invoke(() =>
            {
                (source, geometry, tolerance, _, _, _) = Setup(vm, true);
                a = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/Crop"));
                b = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/Crop"));
                foreach (var crop in new[] { a, b })
                {
                    var p = (Vision.Tools.OpenCv.Processing.CropParameters)crop.Session.Parameters; p.X = 0; p.Y = 0; p.Width = 80; p.Height = 100;
                }
                ((Vision.Tools.OpenCv.Processing.CropParameters)b.Session.Parameters).X = 80;
                var cp = (Vision.Tools.OpenCv.Measurement.FindCircleParameters)source.Session.Parameters;
                cp.ExpectedCenter = new(40, 50); cp.RadiusMin = 20; cp.RadiusMax = 30;
                var pixels = new byte[240 * 100];
                for (var y = 0; y < 100; y++) for (var x = 0; x < 240; x++)
                    if ((x % 80 - 40) * (x % 80 - 40) + (y - 50) * (y - 50) <= 25 * 25) pixels[y * 240 + x] = 255;
                var image = VisionImage.FromBuffer(240, 100, PixelFormat.Mono8, pixels); image.FrameId = 101; vm.SetImage(image);
                VisionImageBinding.Select(source, a); VisionImageBinding.Select(geometry, a); VisionImageBinding.Select(tolerance, b);
            });
            await host.Invoke(() => a.ExecuteAsync(CancellationToken.None)); await host.Invoke(() => b.ExecuteAsync(CancellationToken.None));
            await host.Invoke(() => source.ExecuteAsync(CancellationToken.None)); await host.Invoke(() => geometry.ExecuteAsync(CancellationToken.None));
            host.Invoke(() => Assert.Equal(VisionToolConfiguration.ImageIdentity(a.OutputImage!), VisionToolConfiguration.ImageIdentity(b.OutputImage!)));
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.Invoke(() => tolerance.ExecuteAsync(CancellationToken.None)));
            host.Invoke(() =>
            {
                Assert.Null(tolerance.BooleanOutput); Assert.Contains("坐标来源不同", ((VisionToolInstanceWorkspace)tolerance.Session).Editor.CurrentOperationText);
                VisionImageBinding.Select(tolerance, a);
            });
            await host.Invoke(() => tolerance.ExecuteAsync(CancellationToken.None));
            host.Invoke(() => Assert.True(Assert.IsType<ToleranceResult>(tolerance.Result).IsWithinTolerance));
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    internal static (VisionToolNodeBase, VisionToolNodeBase, VisionToolNodeBase, ConditionNode, VisionToolNodeBase, VisionToolNodeBase) Setup(VisionFlowViewModel vm, bool accepted)
    {
        var source = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/FindCircle"));
        var cp = (Vision.Tools.OpenCv.Measurement.FindCircleParameters)source.Session.Parameters;
        cp.ExpectedCenter = new(80, 70); cp.RadiusMin = 25; cp.RadiusMax = 35; source.CircleOutputField = "Circle";
        var geometry = vm.Add(vm.ToolEntries.Single(e => e.Name == "Common/Geometry")); geometry.Name = "测量圆直径";
        ((GeometryParameters)geometry.Session.Parameters).Operation = GeometryOperation.CircleDiameter;
        geometry.CircleInputParameter = "Circle"; geometry.CircleSourceNodeId = source.Id; geometry.NumericOutputField = "Diameter";
        var tolerance = vm.Add(vm.ToolEntries.Single(e => e.Name == "Common/Tolerance"));
        var p = (ToleranceParameters)tolerance.Session.Parameters; p.Minimum = accepted ? 0 : 100; p.Maximum = accepted ? 100 : 200;
        tolerance.NumericInputParameter = "Measurement"; tolerance.NumericSourceNodeId = geometry.Id; tolerance.BooleanOutputField = "IsWithinTolerance";
        var condition = new ConditionNode(); vm.Chart.Nodes.Add(new NodeViewModel(condition));
        var yes = vm.Add(vm.ToolEntries.Single(e => e.Name == "Common/Geometry")); yes.Name = "OK分支";
        ((GeometryParameters)yes.Session.Parameters).FirstEnd = new(3, 4);
        var no = vm.Add(vm.ToolEntries.Single(e => e.Name == "Common/Geometry")); no.Name = "NG分支";
        ((GeometryParameters)no.Session.Parameters).FirstEnd = new(5, 12);
        Flow(vm, source, geometry); Flow(vm, geometry, tolerance); Flow(vm, tolerance, condition);
        Wire(vm, tolerance, tolerance.OutputPorts.Single(p => p.PropertyName == nameof(VisionToolNodeBase.BooleanOutput)).Id,
            condition, condition.InputPorts.Single(p => p.PropertyName == nameof(ConditionNode.Condition)).Id);
        Wire(vm, condition, condition.OutputPorts.Single(p => p.Name == "True").Id, yes, yes.InputPorts.Single(p => p.Name == "Next_IN").Id);
        Wire(vm, condition, condition.OutputPorts.Single(p => p.Name == "False").Id, no, no.InputPorts.Single(p => p.Name == "Next_IN").Id);
        source.X = 40; source.Y = 40; geometry.X = 300; geometry.Y = 40; tolerance.X = 560; tolerance.Y = 40;
        condition.X = 300; condition.Y = 310; yes.X = 560; yes.Y = 310; no.X = 800; no.Y = 310;
        var pixels = new byte[180 * 160];
        for (var y = 0; y < 160; y++) for (var x = 0; x < 180; x++)
            if ((x - 80) * (x - 80) + (y - 70) * (y - 70) <= 30 * 30) pixels[y * 180 + x] = 255;
        var image = VisionImage.FromBuffer(180, 160, PixelFormat.Mono8, pixels); image.FrameId = 101; vm.SetImage(image);
        return (source, geometry, tolerance, condition, yes, no);
    }
    private static void Check(VisionToolNodeBase tolerance, ConditionNode condition, VisionToolNodeBase yes, VisionToolNodeBase no, bool accepted)
    {
        var result = Assert.IsType<ToleranceResult>(tolerance.Result); Assert.True(result.Success, result.Message);
        Assert.Equal(accepted, result.IsWithinTolerance); Assert.Equal(accepted, tolerance.BooleanOutput); Assert.Equal(accepted, condition.Condition);
        Assert.Equal(accepted ? 5 : 13, Assert.IsType<GeometryResult>((accepted ? yes : no).Result).Distance);
        Assert.Null((accepted ? no : yes).Result);
        var rows = ((VisionToolInstanceWorkspace)tolerance.Session).Editor.CurrentCard!.TableRows!;
        Assert.Equal(accepted, rows[0]["OK"]); Assert.Equal("Pixel", rows[0]["单位"]);
    }
    private static void Flow(VisionFlowViewModel vm, NodeModel a, NodeModel b) =>
        Wire(vm, a, a.OutputPorts.Single(p => p.Name == "Next_Out").Id, b, b.InputPorts.Single(p => p.Name == "Next_IN").Id);
    private static void Wire(VisionFlowViewModel vm, NodeModel a, string output, NodeModel b, string input) =>
        vm.Chart.Connections.Add(new() { SourceNodeId = a.Id, SourcePortId = output, TargetNodeId = b.Id, TargetPortId = input });
    private static void Select(FrameworkElement page, string name, object value)
    {
        page.UpdateLayout(); var combo = Assert.Single(VisionFlowR0Tests.Descendants(page).OfType<ComboBox>(), c => c.DataContext is PropertyItemViewModel r && r.PropertyName == name);
        combo.SelectedValue = value; page.UpdateLayout(); Assert.Equal(value, combo.SelectedValue);
    }
    private static string Artifact(string name)
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "tolerance-acceptance"); Directory.CreateDirectory(folder); return Path.Combine(folder, name);
    }
    private static void Snapshot(FrameworkElement view, string name)
    {
        view.UpdateLayout(); view.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)view.ActualWidth, (int)view.ActualHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(view); var png = new System.Windows.Media.Imaging.PngBitmapEncoder(); png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var stream = File.Create(Artifact(name)); png.Save(stream);
    }
}
