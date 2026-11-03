using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using Vision.Controls.Models;
using Vision.Controls.Services;
using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Tools;
using Vision.FlowNode;
using Vision.Tools.OpenCv.Matching;
using Vision.Tools.OpenCv.Measurement;
using Vision.Tools.OpenCv.Processing;

using WPFCustomControls.PropertyGrid.Core.ViewModels;
using GeometryParameters = Vision.Tools.Halcon.Measurement.GeometryParameters;
using GeometryResult = Vision.Tools.Halcon.Measurement.GeometryResult;
using PixelFormat = Vision.Core.Imaging.PixelFormat;

namespace Vision.Workbench.Tests.Controls;
[Collection("WorkbenchAppBoot")]
[Trait("Category", "Regression")]
public sealed class VisionFindLineFollowTests(WorkbenchAppFixture host)
{
    [Theory]
    [InlineData(LineSearchKind.Calipers)]
    [InlineData(LineSearchKind.EdgeBand)]
    public async Task RealTemplateMatch_OriginalFixtureSelectionAndReference_FollowsLineAndMeasures_Reopen(LineSearchKind mode)
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create); var reopened = host.Invoke(VisionFlowToolboxTests.Create);
        VisionToolNodeBase locator = null!, line = null!, measure = null!; Window window = null!; var path = Artifact($"line-follow-{mode}.flow");
        try
        {
            host.Invoke(() =>
            {
                (locator, line, measure) = Setup(vm, mode); var page = new VisionFlowView { DataContext = vm };
                window = new() { Content = page, Width = 1600, Height = 960, ShowInTaskbar = false, Left = -10000 }; window.Show();
            });
            await host.Invoke(() => locator.ExecuteAsync(CancellationToken.None));
            host.Invoke(() =>
            {
                vm.Select(line); Select((FrameworkElement)window.Content, "FixtureSourceNodeId", locator.Id);
                Assert.Empty(vm.PropertyGridVM.ExtraActions);
                var menu = Assert.IsType<MenuItem>(((VisionFlowView)window.Content).FindName("CaptureFixtureMenuItem"));
                Assert.Same(vm.CaptureFixtureReferenceCommand, menu.Command);
                Assert.True(menu.Command!.CanExecute(null)); menu.Command.Execute(null); Assert.True(line.HasFixtureReference);
                Assert.Equal(new Pose2D(18.5, 18.5, 0), line.ReferenceFixture); vm.SetImage(Frame(true)); vm.Chart.Model.IsModified = false;
            });
            await host.Invoke(() => ((IAsyncRelayCommand)Assert.Single(VisionFlowR0Tests.Descendants(window).OfType<Button>(), b => System.Windows.Automation.AutomationProperties.GetName(b) == "当前流程执行").Command).ExecuteAsync(null));
            host.Invoke(() =>
            {
                Check(line, measure, mode); Assert.False(vm.Chart.Model.IsModified); Assert.True(vm.Chart.SaveDocument(path));
                vm.Chart.ZoomToFitCommand.Execute(null); Snapshot((FrameworkElement)window.Content, $"flow-{mode}.png");
                var full = ((VisionFlowView)window.Content).OpenToolEditor(line)!; var editor = (Vision.Controls.View.VisionToolEditorView)full.Content;
                editor.Session!.SelectedImageView = ImageViewKind.Roi; full.UpdateLayout();
                Assert.Equal(((VisionToolInstanceWorkspace)line.Session).Model.ExecutionSearchRoi!.Bounds, editor.ImagePanel.ImageViewport.ExecutionRoi!.Bounds);
                Snapshot(full, $"derived-line-{mode}.png"); full.Close(); Assert.True(((VisionToolInstanceWorkspace)line.Session).Editor.IsSessionActive); window.Close();
                var config = VisionToolConfiguration.FromState(line.CaptureDocumentState()); Assert.Null(config.Fixture); Assert.Empty(config.Rois);
                Assert.False(config.Parameters!.Value.TryGetProperty("AppliedSearchStart", out _));
            });
            await host.Invoke(() => vm.DisposeAsync().AsTask());
            host.Invoke(() =>
            {
                Assert.True(((VisionToolInstanceWorkspace)line.Session).Model.IsClosed); Assert.True(reopened.Chart.LoadDocument(path));
                line = reopened.Chart.Nodes.Select(n => n.Model).OfType<VisionToolNodeBase>().Single(n => n.ToolIdentity == "OpenCV/FindLine");
                measure = reopened.Chart.Nodes.Select(n => n.Model).OfType<VisionToolNodeBase>().Single(n => n.ToolIdentity == "Common/Geometry");
                Assert.True(line.HasFixtureReference); Assert.True(line.UseLineFixtureBinding); Assert.Equal(locator.Id, line.FixtureSourceNodeId); Assert.Null(line.Result); Assert.Null(line.Session.Fixture); Assert.Null(((VisionToolInstanceWorkspace)line.Session).Model.ExecutionSearchRoi);
            });
            await host.Invoke(() => reopened.RunFlowCommand.ExecuteAsync(null)); host.Invoke(() => Check(line, measure, mode));
            await host.Invoke(() => line.ExecuteAsync(CancellationToken.None)); host.Invoke(() => Assert.Null(measure.Result)); // 上游重跑必须撤旧测量。
            await host.Invoke(() => measure.ExecuteAsync(CancellationToken.None)); host.Invoke(() => Check(line, measure, mode));
        }
        finally { host.Invoke(() => window?.Close()); await host.Invoke(() => vm.DisposeAsync().AsTask()); await host.Invoke(() => reopened.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task MissingReferenceFailedLocatorOldFrameAndSourceRemoval_DoNotReuseLineOrDerivedFrame()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create); VisionToolNodeBase locator = null!, line = null!, measure = null!;
        try
        {
            host.Invoke(() => { (locator, line, measure) = Setup(vm, LineSearchKind.Calipers); line.FixtureSourceNodeId = locator.Id; });
            await host.Invoke(() => locator.ExecuteAsync(CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.Invoke(() => line.ExecuteAsync(CancellationToken.None)));
            host.Invoke(() => { Assert.Null(line.LineOutput); VisionFixtureBinding.CaptureReference(line); vm.SetImage(Frame(true)); });
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null)); host.Invoke(() => Check(line, measure, LineSearchKind.Calipers));
            host.Invoke(() => { vm.Chart.Model.IsRunning = true; Assert.Throws<InvalidOperationException>(() => line.FixtureSourceNodeId = ""); vm.Chart.Model.IsRunning = false; locator.Session.ClearResult(); Assert.Null(line.LineOutput); Assert.Null(((VisionToolInstanceWorkspace)line.Session).Model.ExecutionSearchRoi); });
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.Invoke(() => line.ExecuteAsync(CancellationToken.None)));
            await host.Invoke(() => locator.ExecuteAsync(CancellationToken.None));
            host.Invoke(() => { var old = VisionImageSnapshot.Copy(line.Session.ConfiguredImage!); old.FrameId++; line.Session.SetImage(old); });
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.Invoke(() => line.ExecuteAsync(CancellationToken.None)));
            host.Invoke(() => { Assert.Null(line.LineOutput); vm.SetImage(Frame(true, includeTemplate: false)); });
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() => { Assert.Null(line.LineOutput); Assert.Null(measure.Result); Assert.Null(((VisionToolInstanceWorkspace)line.Session).Model.ExecutionSearchRoi); vm.SetImage(Frame(false)); });
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                Assert.NotNull(line.LineOutput);
                var input = line.InputPorts.Single(p => p.PropertyName == nameof(VisionToolNodeBase.BoundFixtures));
                vm.Chart.Connections.Remove(vm.Chart.Connections.Single(c => c.TargetNodeId == line.Id && c.TargetPortId == input.Id));
                Assert.True(line.UseLineFixtureBinding); Assert.Null(line.LineOutput); Assert.Null(((VisionToolInstanceWorkspace)line.Session).Model.ExecutionSearchRoi);
            });
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.Invoke(() => line.ExecuteAsync(CancellationToken.None)));
            host.Invoke(() => { vm.Chart.Nodes.Remove(vm.Chart.Nodes.Single(n => ReferenceEquals(n.Model, locator))); Assert.True(line.UseLineFixtureBinding); });
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.Invoke(() => line.ExecuteAsync(CancellationToken.None)));
            host.Invoke(() => line.UseLineFixtureBinding = false);
            await host.Invoke(() => line.ExecuteAsync(CancellationToken.None)); // 显式关闭后才恢复基准无跟随。
            host.Invoke(() => { Assert.NotNull(line.LineOutput); Assert.Null(line.Session.Fixture); Assert.Null(((VisionToolInstanceWorkspace)line.Session).Model.ExecutionSearchRoi); });
        }
        finally { host.Invoke(() => vm.Chart.Model.IsRunning = false); await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task IdenticalPixelsFromDistinctCrops_CannotEstablishLineFixtureReference_UntilSameDirectSource()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create); VisionToolNodeBase locator = null!, line = null!, left = null!, right = null!;
        try
        {
            host.Invoke(() =>
            {
                (locator, line, _) = Setup(vm, LineSearchKind.EdgeBand); var input = Frame(false); var pixels = new byte[480 * 180];
                for (var y = 0; y < 180; y++) { Array.Copy(input.Pixels, y * 240, pixels, y * 480, 240); Array.Copy(input.Pixels, y * 240, pixels, y * 480 + 240, 240); }
                vm.SetImage(VisionImage.FromBuffer(480, 180, PixelFormat.Mono8, pixels)); left = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/Crop")); right = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/Crop"));
                foreach (var crop in new[] { left, right }) { var p = (CropParameters)crop.Session.Parameters; p.X = ReferenceEquals(crop, left) ? 0 : 240; p.Y = 0; p.Width = 240; p.Height = 180; }
                VisionImageBinding.Select(locator, left); VisionImageBinding.Select(line, right); line.FixtureSourceNodeId = locator.Id;
            });
            await host.Invoke(() => left.ExecuteAsync(CancellationToken.None)); await host.Invoke(() => right.ExecuteAsync(CancellationToken.None)); await host.Invoke(() => locator.ExecuteAsync(CancellationToken.None));
            host.Invoke(() =>
            {
                Assert.Equal(VisionToolConfiguration.ImageIdentity(left.OutputImage!), VisionToolConfiguration.ImageIdentity(right.OutputImage!));
                Assert.Throws<InvalidOperationException>(() => VisionFixtureBinding.CaptureReference(line)); Assert.False(line.HasFixtureReference);
                VisionImageBinding.Select(line, left); VisionFixtureBinding.CaptureReference(line);
            });
            await host.Invoke(() => line.ExecuteAsync(CancellationToken.None)); host.Invoke(() => Assert.NotNull(line.LineOutput));
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task ProgrammaticRigidPose_RotatesActualLineAndReadonlyFrame_NotClaimedAsRotationMatching()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create); VisionToolNodeBase line = null!;
        try
        {
            host.Invoke(() =>
            {
                line = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/FindLine")); Configure((FindLineParameters)line.Session.Parameters, LineSearchKind.Calipers);
                var pixels = new byte[240 * 180]; for (var y = 0; y < 180; y++) for (var x = 100; x < 240; x++) pixels[y * 240 + x] = 255;
                vm.SetImage(VisionImage.FromBuffer(240, 180, PixelFormat.Mono8, pixels)); line.ReferenceFixture = new(90, 50, 0); line.Session.Fixture = new(100, 90, 90);
            });
            await host.Invoke(() => line.ExecuteAsync(CancellationToken.None));
            host.Invoke(() =>
            {
                var result = Assert.IsType<FindLineResult>(line.Result); Assert.True(result.Success); Assert.InRange(result.Start.X, 99, 100); Assert.Equal(new Point2D(100, 40), result.AppliedSearchStart);
                var derived = ((VisionToolInstanceWorkspace)line.Session).Model.ExecutionSearchRoi!; Assert.Equal(new Pose2D(100, 90, 90), derived.Pose);
                Assert.Equal(new Point2D(40, 50), ((FindLineParameters)line.Session.Parameters).LineStart);
            });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task LegacyUnboundFindLineWithoutFixturePortAndSearchMode_RemainsEdgeBandAtRecipePath()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create); var reopened = host.Invoke(VisionFlowToolboxTests.Create); var path = Artifact("legacy-unbound-line.flow");
        try
        {
            host.Invoke(() =>
            {
                var line = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/FindLine")); Configure((FindLineParameters)line.Session.Parameters, LineSearchKind.EdgeBand); vm.SetImage(Frame(false)); Assert.True(vm.Chart.SaveDocument(path));
                var root = JsonNode.Parse(File.ReadAllText(path))!; var node = root["nodes"]!.AsArray().Single()!; node["properties"]!["Configuration"]!["parameters"]!.AsObject().Remove("SearchMode"); node["properties"]!.AsObject().Remove(nameof(VisionToolNodeBase.UseLineFixtureBinding));
                var ports = node["customInputPorts"]!.AsArray(); foreach (var port in ports.Where(p => p!["boundProperty"]!.GetValue<string>() == nameof(VisionToolNodeBase.BoundFixtures)).ToArray()) ports.Remove(port);
                File.WriteAllText(path, root.ToJsonString());
            });
            await host.Invoke(() => vm.DisposeAsync().AsTask()); host.Invoke(() => Assert.True(reopened.Chart.LoadDocument(path))); await host.Invoke(() => reopened.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                var line = Assert.IsAssignableFrom<VisionToolNodeBase>(Assert.Single(reopened.Chart.Nodes).Model); Assert.Empty(reopened.Chart.Connections); Assert.False(line.UseLineFixtureBinding); Assert.False(line.HasFixtureReference); Assert.Null(line.Session.Fixture);
                Assert.Equal(LineSearchKind.EdgeBand, ((FindLineParameters)line.Session.Parameters).SearchMode); Assert.True(Assert.IsType<FindLineResult>(line.Result).Success); Assert.Null(((VisionToolInstanceWorkspace)line.Session).Model.ExecutionSearchRoi);
            });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); await host.Invoke(() => reopened.DisposeAsync().AsTask()); }
    }

    private static (VisionToolNodeBase, VisionToolNodeBase, VisionToolNodeBase) Setup(VisionFlowViewModel vm, LineSearchKind mode)
    {
        var locator = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/TemplateMatch")); var p = (TemplateMatchParameters)locator.Session.Parameters; p.MinScore = 0.99; p.MaxMatches = 1;
        ((VisionToolInstanceWorkspace)locator.Session).Editor.TemplateEditor!.Capture(Template());
        var line = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/FindLine")); Configure((FindLineParameters)line.Session.Parameters, mode); line.LineOutputField = "Line";
        var measure = vm.Add(vm.ToolEntries.Single(e => e.Name == "Common/Geometry")); ((GeometryParameters)measure.Session.Parameters).FirstEnd = new(3, 4); measure.LineInputParameter = "FirstLine"; measure.LineSourceNodeId = line.Id;
        Wire(vm, locator, line); Wire(vm, line, measure); locator.X = 40; locator.Y = 40; line.X = 340; line.Y = 40; measure.X = 640; measure.Y = 40;
        vm.SetImage(Frame(false)); Assert.True(line.SupportsFixture); return (locator, line, measure);
    }
    private static void Configure(FindLineParameters p, LineSearchKind mode)
    { p.SearchMode = mode; p.LineStart = new(40, 50); p.LineEnd = new(140, 50); p.BandHalfWidth = 4; p.CaliperHalfLength = 8; p.CaliperHalfWidth = 2; p.CaliperCount = 9; p.Threshold = 20; p.Sigma = 1; p.MinEdgePoints = 30; p.CannyLow = 30; p.CannyHigh = 80; }
    private static void Check(VisionToolNodeBase line, VisionToolNodeBase measure, LineSearchKind mode)
    {
        var result = Assert.IsType<FindLineResult>(line.Result); Assert.True(result.Success, result.Message); Assert.Equal(new Point2D(70, 90), result.AppliedSearchStart); Assert.Equal(new Point2D(170, 90), result.AppliedSearchEnd);
        Assert.InRange(result.Start.Y, 89, 90); Assert.InRange(result.End.Y, 89, 90); var measured = Assert.IsType<GeometryResult>(measure.Result); Assert.True(measured.Success); Assert.InRange(measured.Distance, 99, 101);
        var p = (FindLineParameters)line.Session.Parameters; Assert.Equal(new Point2D(40, 50), p.LineStart); Assert.Equal(new Point2D(140, 50), p.LineEnd);
        var w = (VisionToolInstanceWorkspace)line.Session; Assert.Empty(w.RoiEditor.Rois); Assert.Equal(new Pose2D(120, 90, 0), w.Model.ExecutionSearchRoi!.Pose);
        Assert.Equal(mode == LineSearchKind.Calipers ? 104 : 100, w.Model.ExecutionSearchRoi.Size.Width); Assert.Equal(5, ((GeometryParameters)measure.Session.Parameters).FirstStart.DistanceTo(((GeometryParameters)measure.Session.Parameters).FirstEnd));
    }
    private static VisionImage Template() => VisionImage.FromBuffer(7, 7, PixelFormat.Mono8, Enumerable.Range(0, 49).Select(i => (byte)((i * 31 + i / 7 * 17) % 170 + 10)).ToArray());
    private static VisionImage Frame(bool moved, bool includeTemplate = true)
    {
        var pixels = new byte[240 * 180]; var edge = moved ? 90 : 50; for (var y = edge; y < 180; y++) for (var x = 0; x < 240; x++) pixels[y * 240 + x] = 255;
        var template = Template(); var left = moved ? 45 : 15; var top = moved ? 55 : 15;
        if (includeTemplate) for (var y = 0; y < 7; y++) Array.Copy(template.Pixels, y * 7, pixels, (top + y) * 240 + left, 7);
        var image = VisionImage.FromBuffer(240, 180, PixelFormat.Mono8, pixels); image.FrameId = moved ? 2 : 1; return image;
    }
    private static void Select(FrameworkElement page, string property, string id)
    { page.UpdateLayout(); var combo = Assert.Single(VisionFlowR0Tests.Descendants(page).OfType<ComboBox>(), c => c.DataContext is PropertyItemViewModel r && r.PropertyName == property); combo.SelectedValue = id; page.UpdateLayout(); Assert.Equal(id, combo.SelectedValue); Assert.NotNull(combo.SelectedItem); }
    private static void Wire(VisionFlowViewModel vm, VisionToolNodeBase a, VisionToolNodeBase b) => vm.Chart.Connections.Add(new()
    { SourceNodeId = a.Id, SourcePortId = a.OutputPorts.Single(p => p.Name == "Next_Out").Id, TargetNodeId = b.Id, TargetPortId = b.InputPorts.Single(p => p.Name == "Next_IN").Id });
    private static string Artifact(string name) { var folder = Path.Combine(AppContext.BaseDirectory, "line-follow-acceptance"); Directory.CreateDirectory(folder); return Path.Combine(folder, name); }
    private static void Snapshot(FrameworkElement view, string name)
    { view.UpdateLayout(); view.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle); var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)view.ActualWidth, (int)view.ActualHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32); bitmap.Render(view); var png = new System.Windows.Media.Imaging.PngBitmapEncoder(); png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap)); using var stream = File.Create(Artifact(name)); png.Save(stream); }
}
