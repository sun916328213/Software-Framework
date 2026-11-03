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
using GeometryOperation = Vision.Tools.Halcon.Measurement.GeometryOperation;
using GeometryResult = Vision.Tools.Halcon.Measurement.GeometryResult;
using Roi = Vision.Core.Roi.Roi;
using PixelFormat = Vision.Core.Imaging.PixelFormat;

namespace Vision.Workbench.Tests.Controls;
[Collection("WorkbenchAppBoot")]
[Trait("Category", "Regression")]
public sealed class VisionFindCircleFollowTests(WorkbenchAppFixture host)
{
    [Theory]
    [InlineData(60, 40)]
    [InlineData(-50, 40)]
    public async Task RealTemplateAndHough_MoveSelectionDomainAndExpectedCenter_OriginalUiDiameterAndReopen(int dx, int dy)
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create); var reopened = host.Invoke(VisionFlowToolboxTests.Create);
        VisionToolNodeBase locator = null!, circle = null!, diameter = null!; Window window = null!; var path = Artifact($"circle-follow-{dx}.flow");
        try
        {
            host.Invoke(() =>
            {
                (locator, circle, diameter) = Setup(vm); var page = new VisionFlowView { DataContext = vm };
                window = new() { Content = page, Width = 1600, Height = 960, ShowInTaskbar = false, Left = -10000 }; window.Show();
            });
            await host.Invoke(() => locator.ExecuteAsync(CancellationToken.None));
            host.Invoke(() =>
            {
                vm.Select(circle); Select((FrameworkElement)window.Content, "FixtureSourceNodeId", locator.Id);
                Assert.Empty(vm.PropertyGridVM.ExtraActions);
                var menu = Assert.IsType<MenuItem>(((VisionFlowView)window.Content).FindName("CaptureFixtureMenuItem"));
                Assert.Same(vm.CaptureFixtureReferenceCommand, menu.Command);
                Assert.True(menu.Command!.CanExecute(null)); menu.Command.Execute(null);
                Assert.True(circle.UseCircleFixtureBinding); Assert.True(circle.HasFixtureReference); vm.SetImage(Frame(dx, dy)); vm.Chart.Model.IsModified = false;
            });
            await host.Invoke(() => ((IAsyncRelayCommand)Assert.Single(VisionFlowR0Tests.Descendants(window).OfType<Button>(), b => System.Windows.Automation.AutomationProperties.GetName(b) == "当前流程执行").Command).ExecuteAsync(null));
            host.Invoke(() =>
            {
                Check(circle, diameter, dx, dy); Assert.False(vm.Chart.Model.IsModified); Assert.True(vm.Chart.SaveDocument(path)); vm.Chart.ZoomToFitCommand.Execute(null); Snapshot((FrameworkElement)window.Content, $"flow-{dx}.png");
                var full = ((VisionFlowView)window.Content).OpenToolEditor(circle)!; var editor = (Vision.Controls.View.VisionToolEditorView)full.Content; editor.Session!.SelectedImageView = ImageViewKind.Result;
                ((TabItem)editor.DetailsPanel.FindName("ResultsTab")).IsSelected = true; full.UpdateLayout();
                Assert.Equal(new Pose2D(100 + dx, 100 + dy, 0), editor.ImagePanel.ImageViewport.ExecutionRoi!.Pose); Snapshot(full, $"selected-circle-{dx}.png"); full.Close(); window.Close();
                Assert.True(((VisionToolInstanceWorkspace)circle.Session).Editor.IsSessionActive); var config = VisionToolConfiguration.FromState(circle.CaptureDocumentState()); Assert.Null(config.Fixture);
                Assert.Equal(new Pose2D(100, 100, 0), Assert.Single(config.Rois).Pose); Assert.False(config.Parameters!.Value.TryGetProperty("AppliedExpectedCenter", out _));
            });
            await host.Invoke(() => vm.DisposeAsync().AsTask());
            host.Invoke(() =>
            {
                Assert.True(((VisionToolInstanceWorkspace)circle.Session).Model.IsClosed); Assert.True(reopened.Chart.LoadDocument(path));
                circle = reopened.Chart.Nodes.Select(n => n.Model).OfType<VisionToolNodeBase>().Single(n => n.ToolIdentity == "OpenCV/FindCircle"); diameter = reopened.Chart.Nodes.Select(n => n.Model).OfType<VisionToolNodeBase>().Single(n => n.ToolIdentity == "Common/Geometry");
                Assert.True(circle.UseCircleFixtureBinding); Assert.True(circle.HasFixtureReference); Assert.Null(circle.Session.Fixture); Assert.Null(circle.CircleOutput); Assert.Null(((VisionToolInstanceWorkspace)circle.Session).Model.ExecutionSearchRoi);
            });
            await host.Invoke(() => reopened.RunFlowCommand.ExecuteAsync(null)); host.Invoke(() => Check(circle, diameter, dx, dy));
        }
        finally { host.Invoke(() => window?.Close()); await host.Invoke(() => vm.DisposeAsync().AsTask()); await host.Invoke(() => reopened.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task MissingDomainEmptyDomainOldFrameMissingMatchAndDisconnectedFixture_DoNotFallbackToOldCircle()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create); VisionToolNodeBase locator = null!, circle = null!, diameter = null!;
        try
        {
            host.Invoke(() => { (locator, circle, diameter) = Setup(vm); circle.FixtureSourceNodeId = locator.Id; });
            await host.Invoke(() => locator.ExecuteAsync(CancellationToken.None)); host.Invoke(() => VisionFixtureBinding.CaptureReference(circle));
            host.Invoke(() => ((VisionToolInstanceWorkspace)circle.Session).RoiEditor.Clear());
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.Invoke(() => circle.ExecuteAsync(CancellationToken.None)));
            host.Invoke(() => { Assert.Null(circle.CircleOutput); SetRoi(circle, Roi.Rectangle(100, 100, 70, 70)); vm.SetImage(Frame(60, 40)); });
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null)); host.Invoke(() => Check(circle, diameter, 60, 40));
            host.Invoke(() => { SetRoi(circle, Roi.Rectangle(220, 180, 8, 8)); });
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.Invoke(() => circle.ExecuteAsync(CancellationToken.None))); host.Invoke(() => Assert.Null(circle.CircleOutput));
            host.Invoke(() => SetRoi(circle, Roi.Rectangle(100, 100, 70, 70))); await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() => { var frame = VisionImageSnapshot.Copy(circle.Session.ConfiguredImage!); frame.FrameId++; circle.Session.SetImage(frame); });
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.Invoke(() => circle.ExecuteAsync(CancellationToken.None)));
            host.Invoke(() => vm.SetImage(Frame(60, 40, includeTemplate: false))); await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() => { Assert.Null(circle.CircleOutput); Assert.Null(diameter.Result); Assert.Null(((VisionToolInstanceWorkspace)circle.Session).Model.ExecutionSearchRoi); vm.SetImage(Frame(0, 0)); });
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                Assert.NotNull(circle.CircleOutput); vm.Chart.Model.IsRunning = true; Assert.Throws<InvalidOperationException>(() => circle.UseCircleFixtureBinding = false); vm.Chart.Model.IsRunning = false;
                var port = circle.InputPorts.Single(p => p.PropertyName == nameof(VisionToolNodeBase.BoundFixtures)); vm.Chart.Connections.Remove(vm.Chart.Connections.Single(c => c.TargetNodeId == circle.Id && c.TargetPortId == port.Id));
                Assert.True(circle.UseCircleFixtureBinding); Assert.Null(circle.CircleOutput);
            });
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.Invoke(() => circle.ExecuteAsync(CancellationToken.None)));
            host.Invoke(() => vm.Chart.Nodes.Remove(vm.Chart.Nodes.Single(n => ReferenceEquals(n.Model, locator))));
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.Invoke(() => circle.ExecuteAsync(CancellationToken.None)));
            host.Invoke(() => circle.UseCircleFixtureBinding = false); await host.Invoke(() => circle.ExecuteAsync(CancellationToken.None)); host.Invoke(() => Assert.NotNull(circle.CircleOutput));
        }
        finally { host.Invoke(() => vm.Chart.Model.IsRunning = false); await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task SamePixelsFromDistinctCropSources_CannotCaptureCircleFixtureReference()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create); VisionToolNodeBase locator = null!, circle = null!, left = null!, right = null!;
        try
        {
            host.Invoke(() =>
            {
                (locator, circle, _) = Setup(vm); var input = Frame(0, 0); var pixels = new byte[520 * 220];
                for (var y = 0; y < 220; y++) { Array.Copy(input.Pixels, y * 260, pixels, y * 520, 260); Array.Copy(input.Pixels, y * 260, pixels, y * 520 + 260, 260); }
                vm.SetImage(VisionImage.FromBuffer(520, 220, PixelFormat.Mono8, pixels)); left = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/Crop")); right = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/Crop"));
                foreach (var crop in new[] { left, right }) { var p = (CropParameters)crop.Session.Parameters; p.X = ReferenceEquals(crop, left) ? 0 : 260; p.Y = 0; p.Width = 260; p.Height = 220; }
                VisionImageBinding.Select(locator, left); VisionImageBinding.Select(circle, right); circle.FixtureSourceNodeId = locator.Id;
            });
            await host.Invoke(() => left.ExecuteAsync(CancellationToken.None)); await host.Invoke(() => right.ExecuteAsync(CancellationToken.None)); await host.Invoke(() => locator.ExecuteAsync(CancellationToken.None));
            host.Invoke(() =>
            {
                Assert.Equal(VisionToolConfiguration.ImageIdentity(left.OutputImage!), VisionToolConfiguration.ImageIdentity(right.OutputImage!)); Assert.Throws<InvalidOperationException>(() => VisionFixtureBinding.CaptureReference(circle));
                VisionImageBinding.Select(circle, left); VisionFixtureBinding.CaptureReference(circle);
            });
            await host.Invoke(() => circle.ExecuteAsync(CancellationToken.None)); host.Invoke(() => Assert.NotNull(circle.CircleOutput));
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task ProgrammaticNinetyDegreePose_MovesActualHoughSelectionAndExpectedPoint_NotRotationMatching()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create); VisionToolNodeBase circle = null!;
        try
        {
            host.Invoke(() =>
            {
                circle = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/FindCircle")); Configure((FindCircleParameters)circle.Session.Parameters); ((FindCircleParameters)circle.Session.Parameters).ExpectedCenter = new(140, 100);
                SetRoi(circle, Roi.Rectangle(120, 100, 100, 100)); var pixels = new byte[260 * 220]; Disk(pixels, 100, 140, 20); Disk(pixels, 140, 100, 20);
                vm.SetImage(VisionImage.FromBuffer(260, 220, PixelFormat.Mono8, pixels)); circle.ReferenceFixture = new(100, 100, 0); circle.Session.Fixture = new(100, 100, 90);
            });
            await host.Invoke(() => circle.ExecuteAsync(CancellationToken.None));
            host.Invoke(() => { var r = Assert.IsType<FindCircleResult>(circle.Result); Assert.True(r.Success); Assert.Equal(new Point2D(100, 140), r.AppliedExpectedCenter); Assert.InRange(r.Center.Y, 138, 142); Assert.Equal(new Point2D(140, 100), ((FindCircleParameters)circle.Session.Parameters).ExpectedCenter); });
            await host.Invoke(async () =>
            {
                var tool = new FindCircleTool(); Configure(tool.Parameters); tool.Parameters.ExpectedCenter = new(140, 100);
                using var legacy = new VisionToolFlowNode(tool) { Image = circle.Session.Image, SearchRoi = Roi.Rectangle(120, 100, 100, 100), ReferenceFixture = new(100, 100, 0), Fixture = new(100, 100, 90) };
                await legacy.ExecuteAsync(CancellationToken.None); var result = Assert.IsType<FindCircleResult>(legacy.Result);
                Assert.Equal(new Point2D(100, 140), result.AppliedExpectedCenter); Assert.InRange(result.Center.Y, 138, 142); Assert.Null(tool.SearchRegion); Assert.Equal(new Point2D(140, 100), tool.Parameters.ExpectedCenter);
            });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task LegacyWithoutCircleModeOrRoi_RemainsGlobalHoughNearestRecipePoint()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create); var reopened = host.Invoke(VisionFlowToolboxTests.Create); var path = Artifact("legacy-global-circle.flow");
        try
        {
            host.Invoke(() =>
            {
                var circle = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/FindCircle")); Configure((FindCircleParameters)circle.Session.Parameters); vm.SetImage(Frame(0, 0)); Assert.True(vm.Chart.SaveDocument(path));
                var root = JsonNode.Parse(File.ReadAllText(path))!; var node = root["nodes"]!.AsArray().Single()!; node["properties"]!.AsObject().Remove(nameof(VisionToolNodeBase.UseCircleFixtureBinding));
                var ports = node["customInputPorts"]!.AsArray(); foreach (var port in ports.Where(p => p!["boundProperty"]!.GetValue<string>() == nameof(VisionToolNodeBase.BoundFixtures)).ToArray()) ports.Remove(port); File.WriteAllText(path, root.ToJsonString());
            });
            await host.Invoke(() => vm.DisposeAsync().AsTask()); host.Invoke(() => Assert.True(reopened.Chart.LoadDocument(path))); await host.Invoke(() => reopened.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() => { var circle = Assert.IsAssignableFrom<VisionToolNodeBase>(Assert.Single(reopened.Chart.Nodes).Model); Assert.False(circle.UseCircleFixtureBinding); Assert.Empty(reopened.Chart.Connections); Assert.Empty(((VisionToolInstanceWorkspace)circle.Session).RoiEditor.Rois); var r = Assert.IsType<FindCircleResult>(circle.Result); Assert.True(r.Success); Assert.InRange(r.Center.X, 98, 102); });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); await host.Invoke(() => reopened.DisposeAsync().AsTask()); }
    }
    private static (VisionToolNodeBase, VisionToolNodeBase, VisionToolNodeBase) Setup(VisionFlowViewModel vm)
    {
        var locator = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/TemplateMatch")); var p = (TemplateMatchParameters)locator.Session.Parameters; p.MinScore = 0.99; p.MaxMatches = 1; ((VisionToolInstanceWorkspace)locator.Session).Editor.TemplateEditor!.Capture(Template());
        var circle = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/FindCircle")); Configure((FindCircleParameters)circle.Session.Parameters); SetRoi(circle, Roi.Rectangle(100, 100, 70, 70)); circle.CircleOutputField = "Circle";
        var diameter = vm.Add(vm.ToolEntries.Single(e => e.Name == "Common/Geometry")); var g = (GeometryParameters)diameter.Session.Parameters; g.Operation = GeometryOperation.CircleDiameter; g.CircleCenter = new(1000, 1000); g.Radius = 99; diameter.CircleInputParameter = "Circle"; diameter.CircleSourceNodeId = circle.Id;
        Wire(vm, locator, circle); Wire(vm, circle, diameter); locator.X = 40; locator.Y = 40; circle.X = 340; circle.Y = 40; diameter.X = 640; diameter.Y = 40; vm.SetImage(Frame(0, 0)); return (locator, circle, diameter);
    }
    private static void Configure(FindCircleParameters p) { p.ExpectedCenter = new(100, 100); p.RadiusMin = 17; p.RadiusMax = 23; p.CannyHigh = 100; p.AccumThreshold = 18; }
    private static void SetRoi(VisionToolNodeBase circle, Roi roi) { var editor = ((VisionToolInstanceWorkspace)circle.Session).RoiEditor; editor.Clear(); editor.Rois.Add(roi); editor.Selected = roi; }
    private static void Check(VisionToolNodeBase circle, VisionToolNodeBase diameter, int dx, int dy)
    {
        var r = Assert.IsType<FindCircleResult>(circle.Result); Assert.True(r.Success, r.Message); Assert.Equal(new Point2D(100 + dx, 100 + dy), r.AppliedExpectedCenter); Assert.InRange(r.Center.X, 98 + dx, 102 + dx); Assert.InRange(r.Center.Y, 98 + dy, 102 + dy); Assert.InRange(r.Radius, 18, 22);
        var d = Assert.IsType<GeometryResult>(diameter.Result); Assert.True(d.Success); Assert.Equal(2 * r.Radius, d.Diameter, 6); Assert.InRange(d.Diameter, 36, 44); Assert.Equal(99, ((GeometryParameters)diameter.Session.Parameters).Radius);
        var w = (VisionToolInstanceWorkspace)circle.Session; Assert.Equal(new Pose2D(100, 100, 0), Assert.Single(w.RoiEditor.Rois).Pose); Assert.Equal(new Pose2D(100 + dx, 100 + dy, 0), w.Model.ExecutionSearchRoi!.Pose); Assert.Equal(new Point2D(100, 100), ((FindCircleParameters)circle.Session.Parameters).ExpectedCenter); Assert.Equal(100.0 + dx, w.Editor.CurrentCard!.TableRows![0]["执行期望X"]);
    }
    private static VisionImage Template() => VisionImage.FromBuffer(7, 7, PixelFormat.Mono8, Enumerable.Range(0, 49).Select(i => (byte)((i * 31 + i / 7 * 17) % 170 + 10)).ToArray());
    private static VisionImage Frame(int dx, int dy, bool includeTemplate = true)
    {
        var pixels = new byte[260 * 220]; Disk(pixels, 100, 100, 20); if (dx != 0 || dy != 0) Disk(pixels, 100 + dx, 100 + dy, 20);
        var patch = Template(); if (includeTemplate) for (var y = 0; y < 7; y++) Array.Copy(patch.Pixels, y * 7, pixels, (15 + dy + y) * 260 + 80 + dx, 7);
        var image = VisionImage.FromBuffer(260, 220, PixelFormat.Mono8, pixels); image.FrameId = dx == 0 && dy == 0 ? 1 : 2; return image;
    }
    private static void Disk(byte[] pixels, int cx, int cy, int radius) { for (var y = 0; y < 220; y++) for (var x = 0; x < 260; x++) if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= radius * radius) pixels[y * 260 + x] = 255; }
    private static void Wire(VisionFlowViewModel vm, VisionToolNodeBase a, VisionToolNodeBase b) => vm.Chart.Connections.Add(new() { SourceNodeId = a.Id, SourcePortId = a.OutputPorts.Single(p => p.Name == "Next_Out").Id, TargetNodeId = b.Id, TargetPortId = b.InputPorts.Single(p => p.Name == "Next_IN").Id });
    private static void Select(FrameworkElement page, string property, string id) { page.UpdateLayout(); var combo = Assert.Single(VisionFlowR0Tests.Descendants(page).OfType<ComboBox>(), c => c.DataContext is PropertyItemViewModel r && r.PropertyName == property); combo.SelectedValue = id; page.UpdateLayout(); Assert.Equal(id, combo.SelectedValue); Assert.NotNull(combo.SelectedItem); }
    private static string Artifact(string name) { var folder = Path.Combine(AppContext.BaseDirectory, "circle-follow-acceptance"); Directory.CreateDirectory(folder); return Path.Combine(folder, name); }
    private static void Snapshot(FrameworkElement view, string name) { view.UpdateLayout(); view.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle); var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)view.ActualWidth, (int)view.ActualHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32); bitmap.Render(view); var png = new System.Windows.Media.Imaging.PngBitmapEncoder(); png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap)); using var stream = File.Create(Artifact(name)); png.Save(stream); }
}
