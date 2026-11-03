using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using Vision.Controls.Services;
using Vision.Controls.ViewModels;
using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Results;
using Vision.Core.Tools;
using Vision.FlowNode;
using Vision.Tools.Halcon.Measurement;
using Vision.Tools.OpenCv.Processing;

using WPFCustomControls.PropertyGrid.Core.ViewModels;
using PixelFormat = Vision.Core.Imaging.PixelFormat;
using CvCircleParameters = Vision.Tools.OpenCv.Measurement.FindCircleParameters;
using CvLineParameters = Vision.Tools.OpenCv.Measurement.FindLineParameters;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
[Trait("Category", "Regression")]
public sealed class VisionPointBindingTests(WorkbenchAppFixture host)
{
    [Fact]
    public async Task OriginalProperties_OfferExplicitPointField_NotDefaultOrigin()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create);
        try
        {
            host.Invoke(() =>
            {
                var node = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/FindCircle"));
                Assert.Same(node, vm.PropertyGridVM.TargetObject);
                Assert.Single(vm.PropertyGridVM.Groups.SelectMany(g => g.Items), r => r.PropertyName == "PointOutputField");
                Assert.Null(node.PointOutput); Assert.Equal("", node.PointOutputField);
            });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Theory]
    [InlineData("Center")]
    [InlineData("Start")]
    [InlineData("End")]
    public async Task ActualSelectedPointToRecipeReference_OriginalCommands_PreservesConfigurationAndReopens(string field)
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create); var reopened = host.Invoke(VisionFlowToolboxTests.Create);
        Window window = null!; VisionToolNodeBase source = null!, target = null!;
        var path = Artifact($"point-distance-{field}.flow");
        try
        {
            host.Invoke(() =>
            {
                var page = new VisionFlowView { DataContext = vm };
                window = new() { Content = page, Width = 1500, Height = 950, ShowInTaskbar = false, Left = -10000 }; window.Show();
                (source, target) = Setup(vm, field);
                vm.Select(source); Idle(page); Select(page, "PointOutputField", field);
                vm.Select(target); Idle(page);
                Select(page, "PointInputParameter", "FirstStart"); Select(page, "PointSourceNodeId", source.Id);
                source.Name = "实际点" + field; Idle(page); vm.Chart.Model.IsModified = false;
            });
            await host.Invoke(() => ((IAsyncRelayCommand)Assert.Single(VisionFlowR0Tests.Descendants(window).OfType<Button>(),
                b => System.Windows.Automation.AutomationProperties.GetName(b) == "当前流程执行").Command).ExecuteAsync(null));
            await host.Invoke(async () =>
            {
                Check(source, target, field); Assert.False(vm.Chart.Model.IsModified);
                var config = VisionToolConfiguration.FromState(target.CaptureDocumentState());
                Assert.Equal(1000, config.Parameters!.Value.GetProperty("FirstStart").GetProperty("X").GetDouble());
                Assert.Equal(Reference(field).X, config.Parameters.Value.GetProperty("FirstEnd").GetProperty("X").GetDouble());
                Assert.True(vm.Chart.SaveDocument(path));
                var page = (VisionFlowView)window.Content;
                var combo = Assert.Single(VisionFlowR0Tests.Descendants(page).OfType<ComboBox>(),
                    c => c.DataContext is PropertyItemViewModel r && r.PropertyName == "PointSourceNodeId");
                await Task.Delay(350); Idle(page);
                Assert.Equal(source.Id, combo.SelectedValue); Assert.NotNull(combo.SelectedItem);
                Assert.Contains(VisionFlowR0Tests.Descendants(combo).OfType<TextBlock>(), t => t.Text.Contains(source.Name));
                Snapshot(page, $"point-binding-{field}.png");
                var full = page.OpenToolEditor(target)!;
                var editor = (Vision.Controls.View.VisionToolEditorView)full.Content;
                ((TabItem)editor.DetailsPanel.FindName("ResultsTab")).IsSelected = true;
                Snapshot(full, $"measured-point-{field}.png"); full.Close(); window.Close();
            });
            await host.Invoke(() => vm.DisposeAsync().AsTask());
            host.Invoke(() =>
            {
                Assert.True(((VisionToolInstanceWorkspace)target.Session).Model.IsClosed);
                Assert.True(reopened.Chart.LoadDocument(path));
                source = reopened.Chart.Nodes.Select(n => n.Model).OfType<VisionToolNodeBase>().Single(n => n.ToolIdentity != "Common/Geometry");
                target = reopened.Chart.Nodes.Select(n => n.Model).OfType<VisionToolNodeBase>().Single(n => n.ToolIdentity == "Common/Geometry");
                Assert.Null(source.PointOutput); Assert.Null(target.Result);
                Assert.Equal(source.Id, target.PointSourceNodeId); Assert.Equal(field, source.PointOutputField);
                Assert.Equal("FirstStart", target.PointInputParameter);
                Assert.Equal(new Point2D(1000, 1000), ((GeometryParameters)target.Session.Parameters).FirstStart);
                Assert.Equal(Reference(field), ((GeometryParameters)target.Session.Parameters).FirstEnd);
            });
            await host.Invoke(() => reopened.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() => Check(source, target, field));
        }
        finally
        {
            host.Invoke(() => window?.Close());
            await host.Invoke(() => vm.DisposeAsync().AsTask()); await host.Invoke(() => reopened.DisposeAsync().AsTask());
        }
    }

    [Fact]
    public async Task ActualSamePoint_IsZero_ButMissingInvalidatedDisconnectedAndFailedPointNeverUsesOriginOrRecipe()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create); VisionToolNodeBase source = null!, target = null!;
        try
        {
            host.Invoke(() => (source, target) = Setup(vm, "Center"));
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.Invoke(() => target.ExecuteAsync(CancellationToken.None)));
            await host.Invoke(() => source.ExecuteAsync(CancellationToken.None));
            host.Invoke(() =>
            {
                Assert.NotNull(source.PointOutput);
                ((GeometryParameters)target.Session.Parameters).FirstEnd = source.PointOutput!.Position;
                target.Session.ClearResult();
            });
            await host.Invoke(() => target.ExecuteAsync(CancellationToken.None));
            host.Invoke(() =>
            {
                var zero = Assert.IsType<GeometryResult>(target.Result);
                Assert.True(zero.Success, zero.Message); Assert.Equal(0, zero.Distance); Assert.NotNull(zero.AppliedPoint);
                Assert.Equal("Pixel", ((VisionToolInstanceWorkspace)target.Session).Editor.CurrentCard!.TableRows![0]["单位"]);
                var input = target.InputPorts.Single(p => p.PropertyName == nameof(VisionToolNodeBase.BoundPoint));
                vm.Chart.Connections.Remove(vm.Chart.Connections.Single(c => c.TargetNodeId == target.Id && c.TargetPortId == input.Id));
                Assert.Null(target.BoundPoint); Assert.Null(target.Result); Assert.Equal("FirstStart", target.PointInputParameter);
            });
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.Invoke(() => target.ExecuteAsync(CancellationToken.None)));
            host.Invoke(() => target.PointInputParameter = "");
            await host.Invoke(() => target.ExecuteAsync(CancellationToken.None));
            host.Invoke(() =>
            {
                var recipe = Assert.IsType<GeometryResult>(target.Result);
                Assert.True(recipe.Distance > 1000); Assert.Null(recipe.AppliedPoint);
                target.PointInputParameter = "FirstStart"; target.PointSourceNodeId = source.Id;
                source.Session.ClearResult(); Assert.Null(source.PointOutput); Assert.Null(target.Result); Assert.Null(target.BoundPoint);
                vm.SetImage(Frame("Center", blank: true));
            });
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() => { Assert.False(source.Result?.Success == true); Assert.Null(source.PointOutput); Assert.Null(target.Result); });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task OldFrameAndDeletingSource_AreRejected_AndPointFieldsCannotChangeWhileRunning()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create); VisionToolNodeBase source = null!, target = null!;
        try
        {
            host.Invoke(() => (source, target) = Setup(vm, "Center"));
            await host.Invoke(() => source.ExecuteAsync(CancellationToken.None));
            host.Invoke(() =>
            {
                var other = VisionImageSnapshot.Copy(source.Session.Image!); other.FrameId++;
                target.Session.SetImage(other);
            });
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.Invoke(() => target.ExecuteAsync(CancellationToken.None)));
            host.Invoke(() =>
            {
                Assert.Null(target.Result); Assert.Contains("同一帧", ((VisionToolInstanceWorkspace)target.Session).Editor.CurrentOperationText);
                vm.Chart.Model.IsRunning = true;
                Assert.Throws<InvalidOperationException>(() => source.PointOutputField = "");
                Assert.Throws<InvalidOperationException>(() => target.PointInputParameter = "");
                Assert.Throws<InvalidOperationException>(() => target.PointSourceNodeId = "");
                vm.Chart.Model.IsRunning = false;
                Assert.Equal("FirstStart", target.PointInputParameter); Assert.Equal(source.Id, target.PointSourceNodeId);
                vm.Chart.Nodes.Remove(vm.Chart.Nodes.Single(n => ReferenceEquals(n.Model, source)));
            });
            await host.Invoke(() => source.DisposeAsync().AsTask());
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.Invoke(() => target.ExecuteAsync(CancellationToken.None)));
            host.Invoke(() => { Assert.Null(target.Result); Assert.Null(target.BoundPoint); Assert.Equal(new Point2D(1000, 1000), ((GeometryParameters)target.Session.Parameters).FirstStart); });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task SamePixelsFromDistinctCrops_CannotMixPointAndRecipeCoordinateAnchors()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create); VisionToolNodeBase source = null!, target = null!, a = null!, b = null!;
        try
        {
            host.Invoke(() =>
            {
                (source, target) = Setup(vm, "Center");
                a = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/Crop"));
                b = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/Crop"));
                foreach (var crop in new[] { a, b })
                {
                    var p = (CropParameters)crop.Session.Parameters; p.X = 0; p.Y = 0; p.Width = 80; p.Height = 100;
                }
                ((CropParameters)b.Session.Parameters).X = 80;
                var pCircle = (CvCircleParameters)source.Session.Parameters;
                pCircle.ExpectedCenter = new(40, 50); pCircle.RadiusMin = 20; pCircle.RadiusMax = 30;
                var pixels = new byte[240 * 100];
                for (var y = 0; y < 100; y++) for (var x = 0; x < 240; x++)
                    if ((x % 80 - 40) * (x % 80 - 40) + (y - 50) * (y - 50) <= 25 * 25) pixels[y * 240 + x] = 255;
                var image = VisionImage.FromBuffer(240, 100, PixelFormat.Mono8, pixels); image.FrameId = 91; vm.SetImage(image);
                ((GeometryParameters)target.Session.Parameters).FirstEnd = new(70, 90);
                VisionImageBinding.Select(source, a); VisionImageBinding.Select(target, b);
            });
            await host.Invoke(() => a.ExecuteAsync(CancellationToken.None)); await host.Invoke(() => b.ExecuteAsync(CancellationToken.None));
            await host.Invoke(() => source.ExecuteAsync(CancellationToken.None));
            host.Invoke(() =>
            {
                Assert.Equal(VisionToolConfiguration.ImageIdentity(a.OutputImage!), VisionToolConfiguration.ImageIdentity(b.OutputImage!));
                Assert.NotNull(source.PointOutput);
            });
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.Invoke(() => target.ExecuteAsync(CancellationToken.None)));
            host.Invoke(() =>
            {
                Assert.Null(target.Result); Assert.Contains("坐标来源不同", ((VisionToolInstanceWorkspace)target.Session).Editor.CurrentOperationText);
                VisionImageBinding.Select(target, a);
            });
            await host.Invoke(() => target.ExecuteAsync(CancellationToken.None));
            host.Invoke(() =>
            {
                var result = Assert.IsType<GeometryResult>(target.Result); Assert.True(result.Success, result.Message);
                Assert.InRange(result.Distance, 48, 52); Assert.Equal(new Point2D(70, 90), result.AppliedEnd);
            });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OriginalCard_RejectsMixedPointAndCircleSnapshots(bool asynchronous)
    {
        await host.Invoke(async () =>
        {
            var image = Frame("Center"); var identity = VisionToolConfiguration.ImageIdentity(image);
            var tool = new GeometryTool(); tool.Parameters.FirstEnd = new(3, 4);
            var adapter = new HalconToolCardAdapter<GeometryParameters, GeometryResult>(tool);
            ToolCardWorkspace workspace = null!;
            using (workspace = new ToolCardWorkspace(adapter, null, () =>
            {
                workspace.ExecutionPointParameter = new("FirstStart", new(new(0, 0), GeometryUnit.Pixel, identity, "TaskInput"), "TaskInput");
                workspace.ExecutionCircleParameter = new("Circle", new(new(0, 0), 5, GeometryUnit.Pixel, identity, "TaskInput"), "TaskInput");
            }))
            {
                workspace.SetImage(image);
                if (asynchronous) await workspace.RunAsync(); else workspace.Run();
                Assert.Null(workspace.Model.LastResult); Assert.Contains("多个", workspace.Model.StatusText); Assert.Equal(new Point2D(3, 4), tool.Parameters.FirstEnd);
            }
        });
    }

    [Fact]
    public async Task LegacyFlow_WithoutPointStateAndPorts_AddsCapabilityButDoesNotBindDefaultOrigin()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create); var reopened = host.Invoke(VisionFlowToolboxTests.Create);
        VisionToolNodeBase geometry = null!; var path = Artifact("legacy-without-point-binding.flow");
        try
        {
            host.Invoke(() =>
            {
                geometry = vm.Add(vm.ToolEntries.Single(e => e.Name == "Common/Geometry"));
                ((GeometryParameters)geometry.Session.Parameters).FirstEnd = new(3, 4);
                vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/FindCircle"));
                Assert.True(vm.Chart.SaveDocument(path));
                var root = JsonNode.Parse(File.ReadAllText(path))!;
                foreach (var node in root["nodes"]!.AsArray())
                {
                    var properties = node!["properties"]!.AsObject();
                    properties.Remove(nameof(VisionToolNodeBase.PointOutputField)); properties.Remove(nameof(VisionToolNodeBase.PointInputParameter));
                    foreach (var key in new[] { "customInputPorts", "customOutputPorts" })
                    {
                        var ports = node[key]!.AsArray();
                        foreach (var port in ports.Where(p => p!["boundProperty"]!.GetValue<string>() is "BoundPoint" or "PointOutput").ToArray()) ports.Remove(port);
                    }
                }
                File.WriteAllText(path, root.ToJsonString());
            });
            await host.Invoke(() => vm.DisposeAsync().AsTask());
            host.Invoke(() =>
            {
                Assert.True(reopened.Chart.LoadDocument(path));
                geometry = reopened.Chart.Nodes.Select(n => n.Model).OfType<VisionToolNodeBase>().Single(n => n.ToolIdentity == "Common/Geometry");
                var source = reopened.Chart.Nodes.Select(n => n.Model).OfType<VisionToolNodeBase>().Single(n => n.ToolIdentity == "OpenCV/FindCircle");
                Assert.Equal("", geometry.PointInputParameter); Assert.Equal("", source.PointOutputField); Assert.Null(source.PointOutput);
                Assert.Single(geometry.InputPorts, p => p.PropertyName == nameof(VisionToolNodeBase.BoundPoint));
            });
            await host.Invoke(() => geometry.ExecuteAsync(CancellationToken.None));
            host.Invoke(() => { var result = Assert.IsType<GeometryResult>(geometry.Result); Assert.True(result.Success, result.Message); Assert.Equal(5, result.Distance); Assert.Null(result.AppliedPoint); });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); await host.Invoke(() => reopened.DisposeAsync().AsTask()); }
    }

    private static (VisionToolNodeBase Source, VisionToolNodeBase Target) Setup(VisionFlowViewModel vm, string field)
    {
        var source = vm.Add(vm.ToolEntries.Single(e => e.Name == (field == "Center" ? "OpenCV/FindCircle" : "OpenCV/FindLine")));
        if (field == "Center")
        {
            var p = (CvCircleParameters)source.Session.Parameters; p.ExpectedCenter = new(80, 70); p.RadiusMin = 25; p.RadiusMax = 35;
        }
        else
        {
            var p = (CvLineParameters)source.Session.Parameters;
            p.LineStart = new(20, 80); p.LineEnd = new(180, 80); p.CaliperHalfLength = 12; p.CaliperHalfWidth = 2;
            p.Transition = Vision.Tools.OpenCv.Measurement.EdgeTransition.Negative;
            p.EdgeSelection = Vision.Tools.OpenCv.Measurement.CaliperEdgeSelection.First;
        }
        source.PointOutputField = field;
        var target = vm.Add(vm.ToolEntries.Single(e => e.Name == "Common/Geometry"));
        var recipe = (GeometryParameters)target.Session.Parameters;
        recipe.FirstStart = new(1000, 1000); recipe.FirstEnd = Reference(field);
        target.PointInputParameter = "FirstStart"; target.PointSourceNodeId = source.Id;
        vm.Chart.Connections.Add(new()
        {
            SourceNodeId = source.Id, SourcePortId = source.OutputPorts.Single(p => p.Name == "Next_Out").Id,
            TargetNodeId = target.Id, TargetPortId = target.InputPorts.Single(p => p.Name == "Next_IN").Id
        });
        vm.SetImage(Frame(field)); return (source, target);
    }
    private static Point2D Reference(string field) => field == "Start" ? new(50, 119.5) : field == "End" ? new(150, 119.5) : new(110, 110);
    private static VisionImage Frame(string field, bool blank = false)
    {
        var pixels = new byte[220 * 160];
        if (!blank)
            for (var y = 0; y < 160; y++) for (var x = 0; x < 220; x++)
                if (field == "Center" ? (x - 80) * (x - 80) + (y - 70) * (y - 70) <= 30 * 30 : y >= 80 && y <= 120)
                    pixels[y * 220 + x] = 255;
        var image = VisionImage.FromBuffer(220, 160, PixelFormat.Mono8, pixels); image.FrameId = 91; return image;
    }
    private static void Check(VisionToolNodeBase source, VisionToolNodeBase target, string field)
    {
        Assert.True(source.Result?.Success == true, source.Result?.Message); Assert.NotNull(source.PointOutput);
        var measured = Assert.IsType<GeometryResult>(target.Result); Assert.True(measured.Success, measured.Message);
        Assert.InRange(measured.Distance, 48, 52); // 独立合成布局约3:4:5：名义delta=(30,40)，不是由实现反算预期。
        Assert.Equal(source.PointOutput!.Position, measured.AppliedStart); Assert.Equal(Reference(field), measured.AppliedEnd);
        Assert.Equal(GeometryUnit.Pixel, measured.AppliedPoint!.Unit); Assert.Equal(source.ResultImageIdentity, measured.AppliedPoint.ImageIdentity);
        Assert.Equal(new Point2D(1000, 1000), ((GeometryParameters)target.Session.Parameters).FirstStart);
        Assert.Equal(Reference(field), ((GeometryParameters)target.Session.Parameters).FirstEnd);
        var table = ((VisionToolInstanceWorkspace)target.Session).Editor.CurrentCard!.TableRows!;
        Assert.Equal(measured.Distance, table[0]["距离"]); Assert.Equal("Pixel", table[0]["单位"]);
        Assert.Equal(measured.AppliedStart.X, table[0]["执行起点X"]); Assert.Equal(Reference(field).X, table[0]["执行终点X"]);
    }
    private static void Select(FrameworkElement page, string name, object value)
    {
        Idle(page); var combo = Assert.Single(VisionFlowR0Tests.Descendants(page).OfType<ComboBox>(), c => c.DataContext is PropertyItemViewModel r && r.PropertyName == name);
        combo.SelectedValue = value; Idle(page); Assert.Equal(value, combo.SelectedValue);
    }
    private static void Idle(FrameworkElement view) { view.UpdateLayout(); view.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle); }
    private static string Artifact(string name)
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "point-acceptance"); Directory.CreateDirectory(folder); return Path.Combine(folder, name);
    }
    private static void Snapshot(FrameworkElement view, string name)
    {
        Idle(view); var bitmap = new RenderTargetBitmap((int)view.ActualWidth, (int)view.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(view);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = File.Create(Artifact(name)); png.Save(stream);
    }
}
