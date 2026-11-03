using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using Vision.Controls.Services;
using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Tools;
using Vision.FlowNode;
using Vision.Tools.Halcon.Measurement;
using Vision.Tools.OpenCv.Inspection;

using WPFCustomControls.PropertyGrid.Core.ViewModels;
using PixelFormat = Vision.Core.Imaging.PixelFormat;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
[Trait("Category", "Regression")]
public sealed class VisionBlobPointTests(WorkbenchAppFixture host)
{
    [Fact]
    public async Task Catalogs_DeclareUniqueCandidateField_WithoutImplicitFirstOrLargestSelection()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create);
        try { host.Invoke(() =>
        {
            foreach (var backend in new[] { "OpenCV", "HALCON" })
                Assert.Equal(new[] { "SingleCenter" }, vm.ToolEntries.Single(e => e.Name == backend + "/Blob").PointResultFields);
            var node = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/Blob"));
            Assert.Equal("", node.PointOutputField); Assert.Null(node.PointOutput); Assert.Equal(28, vm.ToolEntries.Count);
        }); }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealUniqueBlobCenter_OriginalSelectorsAndRunCommand_MeasureZeroOrFive_AndReopen(bool zero)
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create); var reopened = host.Invoke(VisionFlowToolboxTests.Create);
        VisionToolNodeBase source = null!, target = null!; Window window = null!; var path = Artifact($"single-blob-{zero}.flow");
        try
        {
            host.Invoke(() =>
            {
                var page = new VisionFlowView { DataContext = vm };
                window = new() { Content = page, Width = 1500, Height = 950, ShowInTaskbar = false, Left = -10000 }; window.Show();
                (source, target) = Setup(vm, zero);
                source.PointOutputField = ""; target.PointInputParameter = ""; target.PointSourceNodeId = "";
                vm.Select(source); Select(page, "PointOutputField", "SingleCenter");
                vm.Select(target); Select(page, "PointInputParameter", "FirstStart"); Select(page, "PointSourceNodeId", source.Id);
                vm.Chart.Model.IsModified = false;
            });
            await host.Invoke(() => ((IAsyncRelayCommand)Assert.Single(VisionFlowR0Tests.Descendants(window).OfType<Button>(),
                b => System.Windows.Automation.AutomationProperties.GetName(b) == "当前流程执行").Command).ExecuteAsync(null));
            host.Invoke(() =>
            {
                Check(source, target, zero); Assert.False(vm.Chart.Model.IsModified);
                var configuration = VisionToolConfiguration.FromState(target.CaptureDocumentState()).Parameters!.Value;
                Assert.Equal(1000, configuration.GetProperty("FirstStart").GetProperty("X").GetDouble());
                Assert.True(vm.Chart.SaveDocument(path)); Snapshot((FrameworkElement)window.Content, $"single-blob-{zero}.png");
                var full = ((VisionFlowView)window.Content).OpenToolEditor(target)!;
                var editor = (Vision.Controls.View.VisionToolEditorView)full.Content;
                Assert.Same(((VisionToolInstanceWorkspace)target.Session).Editor, editor.Session);
                ((TabItem)editor.DetailsPanel.FindName("ResultsTab")).IsSelected = true;
                Snapshot(full, $"single-distance-{zero}.png"); full.Close();
                Assert.True(((VisionToolInstanceWorkspace)target.Session).Editor.IsSessionActive); window.Close();
            });
            await host.Invoke(() => vm.DisposeAsync().AsTask());
            host.Invoke(() =>
            {
                Assert.True(((VisionToolInstanceWorkspace)source.Session).Model.IsClosed); Assert.True(reopened.Chart.LoadDocument(path));
                source = reopened.Chart.Nodes.Select(n => n.Model).OfType<VisionToolNodeBase>().Single(n => n.ToolIdentity == "OpenCV/Blob");
                target = reopened.Chart.Nodes.Select(n => n.Model).OfType<VisionToolNodeBase>().Single(n => n.ToolIdentity == "Common/Geometry");
                Assert.Equal("SingleCenter", source.PointOutputField); Assert.Equal(source.Id, target.PointSourceNodeId);
                Assert.Null(source.PointOutput); Assert.Null(target.BoundPoint); Assert.Null(target.Result);
                Assert.Equal(new Point2D(1000, 1000), ((GeometryParameters)target.Session.Parameters).FirstStart);
            });
            await host.Invoke(() => reopened.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() => Check(source, target, zero));
        }
        finally { host.Invoke(() => window?.Close()); await host.Invoke(() => vm.DisposeAsync().AsTask()); await host.Invoke(() => reopened.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task ZeroMultipleFailedAndDisconnectedBlob_NeverReuseOldCenterOrRecipe_AndRunningEditsLock()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create); VisionToolNodeBase source = null!, target = null!;
        try
        {
            host.Invoke(() => (source, target) = Setup(vm, false));
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null)); host.Invoke(() => Check(source, target, false));
            foreach (var count in new[] { 0, 2 })
            {
                host.Invoke(() => vm.SetImage(Frame(count))); await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
                host.Invoke(() =>
                {
                    Assert.True(source.Result!.Success); Assert.Equal(count, Assert.IsType<BlobResult>(source.Result).Count);
                    Assert.Null(source.PointOutput); Assert.Null(target.Result); Assert.Null(target.BoundPoint);
                });
            }
            host.Invoke(() => vm.SetImage(Frame(1))); await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                Check(source, target, false); vm.Chart.Model.IsRunning = true;
                Assert.Throws<InvalidOperationException>(() => source.PointOutputField = ""); vm.Chart.Model.IsRunning = false;
                var input = target.InputPorts.Single(p => p.PropertyName == nameof(VisionToolNodeBase.BoundPoint));
                vm.Chart.Connections.Remove(vm.Chart.Connections.Single(c => c.TargetNodeId == target.Id && c.TargetPortId == input.Id));
                Assert.Null(target.BoundPoint); Assert.Null(target.Result);
            });
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.Invoke(() => target.ExecuteAsync(CancellationToken.None)));
            host.Invoke(() =>
            {
                target.PointSourceNodeId = source.Id;
                ((VisionToolInstanceWorkspace)source.Session).Editor.BlobEditor!.MaxGray = 100; // 原MinGray128，真实参数失败。
            });
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() => { Assert.Null(source.PointOutput); Assert.Null(target.Result); Assert.Equal(new Point2D(1000, 1000), ((GeometryParameters)target.Session.Parameters).FirstStart); });
        }
        finally { host.Invoke(() => vm.Chart.Model.IsRunning = false); await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task LegacyBlobWithoutPointStateOrPort_GainsEmptyCapability_NotAutomaticCenter()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create); var reopened = host.Invoke(VisionFlowToolboxTests.Create);
        var path = Artifact("legacy-blob-without-single-center.flow"); VisionToolNodeBase source = null!;
        try
        {
            host.Invoke(() =>
            {
                source = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/Blob")); vm.SetImage(Frame(1));
                Assert.True(vm.Chart.SaveDocument(path)); var root = JsonNode.Parse(File.ReadAllText(path))!;
                var node = root["nodes"]!.AsArray().Single()!; node["properties"]!.AsObject().Remove(nameof(VisionToolNodeBase.PointOutputField));
                var ports = node["customOutputPorts"]!.AsArray();
                foreach (var port in ports.Where(p => p!["boundProperty"]!.GetValue<string>() == nameof(VisionToolNodeBase.PointOutput)).ToArray()) ports.Remove(port);
                File.WriteAllText(path, root.ToJsonString());
            });
            await host.Invoke(() => vm.DisposeAsync().AsTask());
            host.Invoke(() => { Assert.True(reopened.Chart.LoadDocument(path)); source = Assert.IsAssignableFrom<VisionToolNodeBase>(Assert.Single(reopened.Chart.Nodes).Model); });
            await host.Invoke(() => source.ExecuteAsync(CancellationToken.None));
            host.Invoke(() => { Assert.Single(Assert.IsType<BlobResult>(source.Result).Items); Assert.Equal("", source.PointOutputField); Assert.Null(source.PointOutput); Assert.Empty(reopened.Chart.Connections); });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); await host.Invoke(() => reopened.DisposeAsync().AsTask()); }
    }

    private static (VisionToolNodeBase, VisionToolNodeBase) Setup(VisionFlowViewModel vm, bool zero)
    {
        var source = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/Blob")); source.Name = "唯一目标中心";
        ((VisionToolInstanceWorkspace)source.Session).Editor.BlobEditor!.MinArea = 1; source.PointOutputField = "SingleCenter";
        var target = vm.Add(vm.ToolEntries.Single(e => e.Name == "Common/Geometry")); target.Name = "中心到配方参考点";
        var parameters = (GeometryParameters)target.Session.Parameters; parameters.FirstStart = new(1000, 1000);
        parameters.FirstEnd = zero ? new(12.5, 22.5) : new(15.5, 26.5);
        target.PointInputParameter = "FirstStart"; target.PointSourceNodeId = source.Id;
        vm.Chart.Connections.Add(new() { SourceNodeId = source.Id, SourcePortId = source.OutputPorts.Single(p => p.Name == "Next_Out").Id,
            TargetNodeId = target.Id, TargetPortId = target.InputPorts.Single(p => p.Name == "Next_IN").Id });
        source.X = 40; source.Y = 40; target.X = 300; target.Y = 40; vm.SetImage(Frame(1)); return (source, target);
    }
    private static void Check(VisionToolNodeBase source, VisionToolNodeBase target, bool zero)
    {
        Assert.Single(Assert.IsType<BlobResult>(source.Result).Items); Assert.Equal(new Point2D(12.5, 22.5), source.PointOutput!.Position);
        var result = Assert.IsType<GeometryResult>(target.Result); Assert.True(result.Success, result.Message); Assert.Equal(zero ? 0 : 5, result.Distance);
        Assert.Equal(source.PointOutput, result.AppliedPoint); Assert.Equal(new Point2D(1000, 1000), ((GeometryParameters)target.Session.Parameters).FirstStart);
    }
    private static VisionImage Frame(int count)
    {
        var pixels = new byte[64 * 40];
        if (count >= 1) for (var y = 20; y < 26; y++) for (var x = 10; x < 16; x++) pixels[y * 64 + x] = 255;
        if (count >= 2) for (var y = 5; y < 10; y++) for (var x = 40; x < 45; x++) pixels[y * 64 + x] = 255;
        var image = VisionImage.FromBuffer(64, 40, PixelFormat.Mono8, pixels); image.FrameId = 301; return image;
    }
    private static void Select(FrameworkElement page, string name, object value)
    {
        page.UpdateLayout(); var combo = Assert.Single(VisionFlowR0Tests.Descendants(page).OfType<ComboBox>(), c => c.DataContext is PropertyItemViewModel r && r.PropertyName == name);
        combo.SelectedValue = value; page.UpdateLayout(); Assert.Equal(value, combo.SelectedValue); Assert.NotNull(combo.SelectedItem);
        if (name == "PointSourceNodeId")
        {
            combo.BringIntoView(); page.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            Assert.Contains(VisionFlowR0Tests.Descendants(combo).OfType<TextBlock>(), t => t.Text.Contains("唯一目标中心"));
        }
    }
    private static string Artifact(string name) { var folder = Path.Combine(AppContext.BaseDirectory, "single-center-acceptance"); Directory.CreateDirectory(folder); return Path.Combine(folder, name); }
    private static void Snapshot(FrameworkElement view, string name)
    {
        view.UpdateLayout(); view.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)view.ActualWidth, (int)view.ActualHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(view); var png = new System.Windows.Media.Imaging.PngBitmapEncoder(); png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var stream = File.Create(Artifact(name)); png.Save(stream);
    }
}
