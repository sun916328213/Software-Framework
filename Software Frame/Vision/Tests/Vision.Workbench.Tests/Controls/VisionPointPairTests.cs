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
using Vision.Tools.OpenCv.Processing;

using WPFCustomControls.PropertyGrid.Core.ViewModels;
using PixelFormat = Vision.Core.Imaging.PixelFormat;

namespace Vision.Workbench.Tests.Controls;
[Collection("WorkbenchAppBoot")]
[Trait("Category", "Regression")]
public sealed class VisionPointPairTests(WorkbenchAppFixture host)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualTwoBlobSources_OriginalPropertiesAndRun_Measure50OrZero_KeepBothRecipePointsAndReopen(bool same)
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create); var reopened = host.Invoke(VisionFlowToolboxTests.Create);
        VisionToolNodeBase a = null!, b = null!, target = null!; Window window = null!; var path = Artifact($"point-pair-{same}.flow");
        try
        {
            host.Invoke(() =>
            {
                var page = new VisionFlowView { DataContext = vm }; window = new() { Content = page, Width = 1550, Height = 950, ShowInTaskbar = false, Left = -10000 }; window.Show();
                (a, b, target) = Setup(vm); vm.Select(target);
                var mode = Row(vm, "UseReferencePointBinding"); mode.Value = true;
                Select(page, "ReferencePointSourceNodeId", same ? a.Id : b.Id);
                Assert.True(target.UseReferencePointBinding); vm.Chart.Model.IsModified = false;
            });
            await host.Invoke(() => ((IAsyncRelayCommand)Assert.Single(VisionFlowR0Tests.Descendants(window).OfType<Button>(),
                button => System.Windows.Automation.AutomationProperties.GetName(button) == "当前流程执行").Command).ExecuteAsync(null));
            host.Invoke(() =>
            {
                Check(target, same ? 0 : 50); Assert.False(vm.Chart.Model.IsModified); Assert.True(vm.Chart.SaveDocument(path));
                Snapshot((FrameworkElement)window.Content, $"pair-{same}.png");
                var full = ((VisionFlowView)window.Content).OpenToolEditor(target)!;
                var editor = (Vision.Controls.View.VisionToolEditorView)full.Content; ((TabItem)editor.DetailsPanel.FindName("ResultsTab")).IsSelected = true;
                Snapshot(full, $"pair-result-{same}.png"); full.Close(); Assert.True(((VisionToolInstanceWorkspace)target.Session).Editor.IsSessionActive); window.Close();
            });
            await host.Invoke(() => vm.DisposeAsync().AsTask());
            host.Invoke(() =>
            {
                Assert.True(((VisionToolInstanceWorkspace)target.Session).Model.IsClosed); Assert.True(reopened.Chart.LoadDocument(path));
                target = reopened.Chart.Nodes.Select(n => n.Model).OfType<VisionToolNodeBase>().Single(n => n.ToolIdentity == "Common/Geometry");
                Assert.True(target.UseReferencePointBinding); Assert.Equal(same ? a.Id : b.Id, target.ReferencePointSourceNodeId);
                Assert.Null(target.BoundPoint); Assert.Null(target.BoundReferencePoint); Assert.Null(target.Result);
            });
            await host.Invoke(() => reopened.RunFlowCommand.ExecuteAsync(null)); host.Invoke(() => Check(target, same ? 0 : 50));
        }
        finally { host.Invoke(() => window?.Close()); await host.Invoke(() => vm.DisposeAsync().AsTask()); await host.Invoke(() => reopened.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task ReferenceMissingAmbiguousFailedDisconnectedOrDisabledWithWire_NeverFallsBackToRecipe_AndLocks()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create); VisionToolNodeBase a = null!, b = null!, target = null!;
        try
        {
            host.Invoke(() => { (a, b, target) = Setup(vm); target.UseReferencePointBinding = true; target.ReferencePointSourceNodeId = b.Id; });
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null)); host.Invoke(() => Check(target, 50));
            foreach (var count in new[] { 0, 2 })
            {
                host.Invoke(() => vm.SetImage(Frame(count))); await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
                host.Invoke(() => { Assert.NotNull(a.PointOutput); Assert.Null(b.PointOutput); Assert.Null(target.Result); Assert.Null(target.BoundReferencePoint); });
            }
            host.Invoke(() => vm.SetImage(Frame(1))); await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                Check(target, 50); vm.Chart.Model.IsRunning = true;
                Assert.Throws<InvalidOperationException>(() => target.UseReferencePointBinding = false);
                Assert.Throws<InvalidOperationException>(() => target.ReferencePointSourceNodeId = a.Id); vm.Chart.Model.IsRunning = false;
                var port = target.InputPorts.Single(p => p.PropertyName == nameof(VisionToolNodeBase.BoundReferencePoint));
                vm.Chart.Connections.Remove(vm.Chart.Connections.Single(e => e.TargetNodeId == target.Id && e.TargetPortId == port.Id));
                Assert.Null(target.BoundReferencePoint); Assert.Null(target.Result); Assert.True(target.UseReferencePointBinding);
            });
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.Invoke(() => target.ExecuteAsync(CancellationToken.None)));
            host.Invoke(() => { target.ReferencePointSourceNodeId = b.Id; target.UseReferencePointBinding = false; });
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.Invoke(() => target.ExecuteAsync(CancellationToken.None)));
            host.Invoke(() =>
            {
                target.UseReferencePointBinding = true; ((VisionToolInstanceWorkspace)b.Session).Editor.BlobEditor!.MaxGray = 100;
            });
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null)); host.Invoke(() => { Assert.Null(b.PointOutput); Assert.Null(target.Result); });
            host.Invoke(() => { target.ReferencePointSourceNodeId = ""; target.UseReferencePointBinding = false; });
            await host.Invoke(() => target.ExecuteAsync(CancellationToken.None));
            host.Invoke(() =>
            {
                var result = Assert.IsType<GeometryResult>(target.Result); Assert.True(result.Success); Assert.Null(result.AppliedReferencePoint);
                ((VisionToolInstanceWorkspace)b.Session).Editor.BlobEditor!.MaxGray = 255;
                target.UseReferencePointBinding = true; target.ReferencePointSourceNodeId = b.Id;
            });
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                Check(target, 50); vm.Chart.Nodes.Remove(vm.Chart.Nodes.Single(n => ReferenceEquals(n.Model, b)));
                Assert.Null(target.BoundReferencePoint); Assert.Null(target.Result); Assert.True(target.UseReferencePointBinding);
            });
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.Invoke(() => target.ExecuteAsync(CancellationToken.None)));
        }
        finally { host.Invoke(() => vm.Chart.Model.IsRunning = false); await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task ReferenceMustMatchFrameAndDirectCoordinateAnchor_EvenWhenCropPixelsAreIdentical()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create); VisionToolNodeBase a = null!, b = null!, target = null!, left = null!, right = null!;
        try
        {
            host.Invoke(() => { (a, b, target) = Setup(vm); target.UseReferencePointBinding = true; target.ReferencePointSourceNodeId = b.Id; });
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() => { var frame = VisionImageSnapshot.Copy(b.Session.ConfiguredImage!); frame.FrameId++; b.Session.SetImage(frame); });
            await host.Invoke(() => b.ExecuteAsync(CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.Invoke(() => target.ExecuteAsync(CancellationToken.None)));
            host.Invoke(() =>
            {
                Assert.Null(target.Result); var frame = Frame(1); var doubled = new byte[160 * 90];
                for (var y = 0; y < 90; y++) { Array.Copy(frame.Pixels, y * 80, doubled, y * 160, 80); Array.Copy(frame.Pixels, y * 80, doubled, y * 160 + 80, 80); }
                vm.SetImage(VisionImage.FromBuffer(160, 90, PixelFormat.Mono8, doubled));
                left = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/Crop")); right = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/Crop"));
                foreach (var crop in new[] { left, right }) { var p = (CropParameters)crop.Session.Parameters; p.Width = 80; p.Height = 90; p.X = ReferenceEquals(crop, left) ? 0 : 80; p.Y = 0; }
                VisionImageBinding.Select(a, left); VisionImageBinding.Select(b, right); VisionImageBinding.Select(target, left);
            });
            await host.Invoke(() => left.ExecuteAsync(CancellationToken.None)); await host.Invoke(() => right.ExecuteAsync(CancellationToken.None));
            await host.Invoke(() => a.ExecuteAsync(CancellationToken.None)); await host.Invoke(() => b.ExecuteAsync(CancellationToken.None));
            host.Invoke(() => Assert.Equal(VisionToolConfiguration.ImageIdentity(left.OutputImage!), VisionToolConfiguration.ImageIdentity(right.OutputImage!)));
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.Invoke(() => target.ExecuteAsync(CancellationToken.None)));
            host.Invoke(() => { Assert.Null(target.Result); Assert.Contains("坐标来源", ((VisionToolInstanceWorkspace)target.Session).Editor.CurrentOperationText); VisionImageBinding.Select(b, left); });
            await host.Invoke(() => b.ExecuteAsync(CancellationToken.None)); await host.Invoke(() => target.ExecuteAsync(CancellationToken.None));
            host.Invoke(() => Check(target, 50));
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task LegacySinglePointFileWithoutReferenceModeOrPort_StillUsesRecipeAndDoesNotAutoBind()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create); var reopened = host.Invoke(VisionFlowToolboxTests.Create); var path = Artifact("legacy-single-point.flow");
        try
        {
            host.Invoke(() =>
            {
                var (a, b, target) = Setup(vm); ((GeometryParameters)target.Session.Parameters).FirstEnd = new(15.5, 26.5);
                Assert.True(vm.Chart.SaveDocument(path)); var root = JsonNode.Parse(File.ReadAllText(path))!;
                foreach (var node in root["nodes"]!.AsArray())
                {
                    node!["properties"]!.AsObject().Remove(nameof(VisionToolNodeBase.UseReferencePointBinding));
                    var ports = node["customInputPorts"]!.AsArray();
                    foreach (var port in ports.Where(p => p!["boundProperty"]!.GetValue<string>() == nameof(VisionToolNodeBase.BoundReferencePoint)).ToArray()) ports.Remove(port);
                }
                File.WriteAllText(path, root.ToJsonString());
            });
            await host.Invoke(() => vm.DisposeAsync().AsTask());
            host.Invoke(() => Assert.True(reopened.Chart.LoadDocument(path))); await host.Invoke(() => reopened.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                var target = reopened.Chart.Nodes.Select(n => n.Model).OfType<VisionToolNodeBase>().Single(n => n.ToolIdentity == "Common/Geometry");
                Assert.False(target.UseReferencePointBinding); Assert.Equal("", target.ReferencePointSourceNodeId);
                Assert.Equal(5, Assert.IsType<GeometryResult>(target.Result).Distance); Assert.Null(target.BoundReferencePoint);
            });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); await host.Invoke(() => reopened.DisposeAsync().AsTask()); }
    }

    private static (VisionToolNodeBase, VisionToolNodeBase, VisionToolNodeBase) Setup(VisionFlowViewModel vm)
    {
        var a = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/Blob")); a.Name = "测量点A";
        var b = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/Blob")); b.Name = "参考点B";
        foreach (var node in new[] { a, b }) { ((VisionToolInstanceWorkspace)node.Session).Editor.BlobEditor!.MinArea = 1; node.PointOutputField = "SingleCenter"; }
        ((BlobParameters)a.Session.Parameters).Features = [new() { Feature = "area", Min = 36, Max = 36 }];
        ((VisionToolInstanceWorkspace)b.Session).Editor.BlobEditor!.MinArea = 40;
        var target = vm.Add(vm.ToolEntries.Single(e => e.Name == "Common/Geometry"));
        var p = (GeometryParameters)target.Session.Parameters; p.FirstStart = new(1000, 1000); p.FirstEnd = new(-1000, -1000);
        target.PointInputParameter = "FirstStart"; target.PointSourceNodeId = a.Id;
        Wire(vm, a, b); Wire(vm, b, target); a.X = 40; a.Y = 40; b.X = 300; b.Y = 330; target.X = 600; target.Y = 40;
        vm.SetImage(Frame(1)); return (a, b, target);
    }
    private static void Check(VisionToolNodeBase target, double expected)
    {
        var result = Assert.IsType<GeometryResult>(target.Result); Assert.True(result.Success, result.Message); Assert.Equal(expected, result.Distance);
        Assert.NotNull(result.AppliedReferencePoint); Assert.Equal(result.AppliedReferencePoint!.Position, result.AppliedEnd);
        var p = (GeometryParameters)target.Session.Parameters; Assert.Equal(new Point2D(1000, 1000), p.FirstStart); Assert.Equal(new Point2D(-1000, -1000), p.FirstEnd);
    }
    private static VisionImage Frame(int references)
    {
        var pixels = new byte[80 * 90];
        for (var y = 20; y < 26; y++) for (var x = 10; x < 16; x++) pixels[y * 80 + x] = 255;
        if (references >= 1) for (var y = 59; y < 67; y++) for (var x = 39; x < 47; x++) pixels[y * 80 + x] = 255;
        if (references >= 2) for (var y = 10; y < 18; y++) for (var x = 60; x < 68; x++) pixels[y * 80 + x] = 255;
        var image = VisionImage.FromBuffer(80, 90, PixelFormat.Mono8, pixels); image.FrameId = 401; return image;
    }
    private static PropertyItemViewModel Row(VisionFlowViewModel vm, string name) => Assert.Single(vm.PropertyGridVM.Groups.SelectMany(g => g.Items), r => r.PropertyName == name);
    private static void Select(FrameworkElement page, string name, object value)
    {
        page.UpdateLayout(); var combo = Assert.Single(VisionFlowR0Tests.Descendants(page).OfType<ComboBox>(), c => c.DataContext is PropertyItemViewModel r && r.PropertyName == name);
        combo.SelectedValue = value; page.UpdateLayout(); combo.BringIntoView(); page.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        Assert.Equal(value, combo.SelectedValue); Assert.NotNull(combo.SelectedItem);
        var expected = ((VisionFlowViewModel)page.DataContext).Chart.Nodes.Single(n => n.Model.Id == (string)value).Model.Name;
        Assert.Contains(VisionFlowR0Tests.Descendants(combo).OfType<TextBlock>(), t => t.Text.Contains(expected));
    }
    private static void Wire(VisionFlowViewModel vm, VisionToolNodeBase from, VisionToolNodeBase to) => vm.Chart.Connections.Add(new()
    { SourceNodeId = from.Id, SourcePortId = from.OutputPorts.Single(p => p.Name == "Next_Out").Id, TargetNodeId = to.Id, TargetPortId = to.InputPorts.Single(p => p.Name == "Next_IN").Id });
    private static string Artifact(string name) { var folder = Path.Combine(AppContext.BaseDirectory, "point-pair-acceptance"); Directory.CreateDirectory(folder); return Path.Combine(folder, name); }
    private static void Snapshot(FrameworkElement view, string name)
    {
        view.UpdateLayout(); view.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)view.ActualWidth, (int)view.ActualHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(view); var png = new System.Windows.Media.Imaging.PngBitmapEncoder(); png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var stream = File.Create(Artifact(name)); png.Save(stream);
    }
}
