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
using CvCircleResult = Vision.Tools.OpenCv.Measurement.FindCircleResult;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
[Trait("Category", "Regression")]
public sealed class VisionCircleBindingTests(WorkbenchAppFixture host)
{
    [Fact]
    public async Task CircleField_IsSelectedOnOriginalNodeProperties_NotAnImplicitRadius()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create);
        try
        {
            host.Invoke(() =>
            {
                var node = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/FindCircle"));
                Assert.Same(node, vm.PropertyGridVM.TargetObject);
                Assert.Single(vm.PropertyGridVM.Groups.SelectMany(g => g.Items), r => r.PropertyName == "CircleOutputField");
                Assert.Null(node.CircleOutput); Assert.Equal("", node.CircleOutputField);
            });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Theory]
    [InlineData(26, 0)]
    [InlineData(38, 99)]
    public async Task ActualHoughToDiameter_OriginalControlsAndCommand_KeepRecipeAndReopenNewSession(int radius, double recipeRadius)
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create); var reopened = host.Invoke(VisionFlowToolboxTests.Create);
        Window window = null!; VisionToolNodeBase source = null!, target = null!;
        var path = Artifact($"circle-diameter-{radius}.flow");
        try
        {
            host.Invoke(() =>
            {
                var page = new VisionFlowView { DataContext = vm };
                window = new() { Content = page, Width = 1500, Height = 950, ShowInTaskbar = false, Left = -10000 }; window.Show();
                (source, target) = Setup(vm, radius);
                ((GeometryParameters)target.Session.Parameters).Radius = recipeRadius;
                vm.Select(source); Idle(page); Select(page, "CircleOutputField", "Circle");
                vm.Select(target); Idle(page);
                Select(page, "Operation", GeometryOperation.CircleDiameter);
                Select(page, "CircleInputParameter", "Circle"); Select(page, "CircleSourceNodeId", source.Id);
                source.Name = "实际Hough圆"; Idle(page); vm.Chart.Model.IsModified = false;
            });
            await host.Invoke(() => ((IAsyncRelayCommand)Assert.Single(VisionFlowR0Tests.Descendants(window).OfType<Button>(),
                b => System.Windows.Automation.AutomationProperties.GetName(b) == "当前流程执行").Command).ExecuteAsync(null));
            await host.Invoke(async () =>
            {
                Check(source, target, radius, recipeRadius);
                Assert.False(vm.Chart.Model.IsModified);
                var config = VisionToolConfiguration.FromState(target.CaptureDocumentState());
                Assert.Equal(recipeRadius, config.Parameters!.Value.GetProperty("Radius").GetDouble());
                Assert.Equal(1000, config.Parameters.Value.GetProperty("CircleCenter").GetProperty("X").GetDouble());
                Assert.True(vm.Chart.SaveDocument(path));
                var page = (VisionFlowView)window.Content;
                var combo = Assert.Single(VisionFlowR0Tests.Descendants(page).OfType<ComboBox>(),
                    c => c.DataContext is PropertyItemViewModel r && r.PropertyName == "CircleSourceNodeId");
                await Task.Delay(350); Idle(page);
                Assert.Equal(source.Id, combo.SelectedValue); Assert.NotNull(combo.SelectedItem);
                Assert.Contains(VisionFlowR0Tests.Descendants(combo).OfType<TextBlock>(), t => t.Text.Contains(source.Name));
                Snapshot(page, $"circle-binding-{radius}.png");
                var full = page.OpenToolEditor(target)!;
                var editor = (Vision.Controls.View.VisionToolEditorView)full.Content;
                ((TabItem)editor.DetailsPanel.FindName("ResultsTab")).IsSelected = true;
                Snapshot(full, $"measured-diameter-{radius}.png"); full.Close(); window.Close();
            });
            await host.Invoke(() => vm.DisposeAsync().AsTask());
            host.Invoke(() =>
            {
                Assert.True(((VisionToolInstanceWorkspace)target.Session).Model.IsClosed);
                Assert.True(reopened.Chart.LoadDocument(path));
                source = reopened.Chart.Nodes.Select(n => n.Model).OfType<VisionToolNodeBase>().Single(n => n.ToolIdentity == "OpenCV/FindCircle");
                target = reopened.Chart.Nodes.Select(n => n.Model).OfType<VisionToolNodeBase>().Single(n => n.ToolIdentity == "Common/Geometry");
                Assert.Null(source.CircleOutput); Assert.Null(target.Result);
                Assert.Equal(source.Id, target.CircleSourceNodeId); Assert.Equal("Circle", source.CircleOutputField);
                Assert.Equal("Circle", target.CircleInputParameter); Assert.Equal("", target.LineInputParameter);
                Assert.Equal(recipeRadius, ((GeometryParameters)target.Session.Parameters).Radius);
            });
            await host.Invoke(() => reopened.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() => Check(source, target, radius, recipeRadius));
        }
        finally
        {
            host.Invoke(() => window?.Close());
            await host.Invoke(() => vm.DisposeAsync().AsTask()); await host.Invoke(() => reopened.DisposeAsync().AsTask());
        }
    }

    [Fact]
    public async Task MissingInvalidatedDisconnectedAndFailedCircle_NeverFallsBackToRecipeDiameter()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create); VisionToolNodeBase source = null!, target = null!;
        try
        {
            host.Invoke(() => (source, target) = Setup(vm));
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.Invoke(() => target.ExecuteAsync(CancellationToken.None)));
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                Check(source, target, 30); Assert.NotNull(target.BoundCircle);
                var input = target.InputPorts.Single(p => p.PropertyName == nameof(VisionToolNodeBase.BoundCircle));
                vm.Chart.Connections.Remove(vm.Chart.Connections.Single(c => c.TargetNodeId == target.Id && c.TargetPortId == input.Id));
                Assert.Null(target.BoundCircle); Assert.Null(target.Result); Assert.Equal("Circle", target.CircleInputParameter);
            });
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.Invoke(() => target.ExecuteAsync(CancellationToken.None)));
            host.Invoke(() => target.CircleInputParameter = "");
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                var recipe = Assert.IsType<GeometryResult>(target.Result); Assert.Equal(198, recipe.Diameter); Assert.Null(recipe.AppliedCircle);
                target.CircleInputParameter = "Circle"; target.CircleSourceNodeId = source.Id;
            });
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                source.Session.ClearResult(); Assert.Null(source.CircleOutput); Assert.Null(target.Result); Assert.Null(target.BoundCircle);
                vm.SetImage(Frame(30, blank: true));
            });
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() => { Assert.False(source.Result?.Success == true); Assert.Null(source.CircleOutput); Assert.Null(target.Result); });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task OldFrameAndRemovingSource_AreRejected_AndRunningFieldsAreLocked()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create); VisionToolNodeBase source = null!, target = null!;
        try
        {
            host.Invoke(() => (source, target) = Setup(vm));
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
                Assert.Throws<InvalidOperationException>(() => target.CircleInputParameter = "");
                Assert.Throws<InvalidOperationException>(() => target.CircleSourceNodeId = "");
                vm.Chart.Model.IsRunning = false;
                Assert.Equal("Circle", target.CircleInputParameter); Assert.Equal(source.Id, target.CircleSourceNodeId);
                vm.Chart.Nodes.Remove(vm.Chart.Nodes.Single(n => ReferenceEquals(n.Model, source)));
            });
            await host.Invoke(() => source.DisposeAsync().AsTask());
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.Invoke(() => target.ExecuteAsync(CancellationToken.None)));
            host.Invoke(() => { Assert.Null(target.Result); Assert.Null(target.BoundCircle); Assert.Equal(99, ((GeometryParameters)target.Session.Parameters).Radius); });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task IdenticalPixelsFromDifferentCrops_RequireSameExplicitCoordinateAnchor()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create); VisionToolNodeBase source = null!, target = null!, a = null!, b = null!;
        try
        {
            host.Invoke(() =>
            {
                (source, target) = Setup(vm);
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
                for (var y = 0; y < 100; y++)
                    for (var x = 0; x < 240; x++)
                        if ((x % 80 - 40) * (x % 80 - 40) + (y - 50) * (y - 50) <= 25 * 25) pixels[y * 240 + x] = 255;
                var image = VisionImage.FromBuffer(240, 100, PixelFormat.Mono8, pixels); image.FrameId = 81; vm.SetImage(image);
                VisionImageBinding.Select(source, a); VisionImageBinding.Select(target, b);
            });
            await host.Invoke(() => a.ExecuteAsync(CancellationToken.None)); await host.Invoke(() => b.ExecuteAsync(CancellationToken.None));
            await host.Invoke(() => source.ExecuteAsync(CancellationToken.None));
            host.Invoke(() =>
            {
                Assert.Equal(VisionToolConfiguration.ImageIdentity(a.OutputImage!), VisionToolConfiguration.ImageIdentity(b.OutputImage!));
                Assert.NotNull(source.CircleOutput);
            });
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.Invoke(() => target.ExecuteAsync(CancellationToken.None)));
            host.Invoke(() =>
            {
                Assert.Null(target.Result); Assert.Contains("坐标来源不同", ((VisionToolInstanceWorkspace)target.Session).Editor.CurrentOperationText);
                VisionImageBinding.Select(target, a);
            });
            await host.Invoke(() => target.ExecuteAsync(CancellationToken.None));
            host.Invoke(() => Check(source, target, 25));
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OriginalCard_RejectsMixedCircleAndLineSnapshots(bool asynchronous)
    {
        await host.Invoke(async () =>
        {
            var image = Frame(30); var identity = VisionToolConfiguration.ImageIdentity(image);
            var tool = new GeometryTool(); tool.Parameters.Operation = GeometryOperation.CircleDiameter; tool.Parameters.Radius = 99;
            var adapter = new HalconToolCardAdapter<GeometryParameters, GeometryResult>(tool);
            ToolCardWorkspace workspace = null!;
            using (workspace = new ToolCardWorkspace(adapter, null, () =>
            {
                workspace.ExecutionLineParameter = new("FirstLine", new(new(0, 0), new(3, 4), GeometryUnit.Pixel, identity, "TaskInput"), "TaskInput");
                workspace.ExecutionCircleParameter = new("Circle", new(new(0, 0), 5, GeometryUnit.Pixel, identity, "TaskInput"), "TaskInput");
            }))
            {
                workspace.SetImage(image);
                if (asynchronous) await workspace.RunAsync(); else workspace.Run();
                Assert.Null(workspace.Model.LastResult); Assert.Contains("多个", workspace.Model.StatusText); Assert.Equal(99, tool.Parameters.Radius);
            }
        });
    }

    [Fact]
    public async Task LegacyGeometryAndCircleFlow_WithoutNewStateParametersOrPorts_KeepsOldMathAndDoesNotAutoBind()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create); var reopened = host.Invoke(VisionFlowToolboxTests.Create);
        VisionToolNodeBase geometry = null!; var path = Artifact("legacy-without-circle-binding.flow");
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
                    properties.Remove(nameof(VisionToolNodeBase.CircleOutputField)); properties.Remove(nameof(VisionToolNodeBase.CircleInputParameter));
                    var config = properties["Configuration"]!;
                    if (config["toolIdentity"]!.GetValue<string>() == "Common/Geometry")
                    {
                        config["parameters"]!.AsObject().Remove(nameof(GeometryParameters.CircleCenter));
                        config["parameters"]!.AsObject().Remove(nameof(GeometryParameters.Radius));
                    }
                    foreach (var portsKey in new[] { "customInputPorts", "customOutputPorts" })
                    {
                        var ports = node[portsKey]!.AsArray();
                        foreach (var port in ports.Where(p => p!["boundProperty"]!.GetValue<string>() is "BoundCircle" or "CircleOutput").ToArray())
                            ports.Remove(port);
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
                Assert.Equal("", geometry.CircleInputParameter); Assert.Equal("", source.CircleOutputField);
                Assert.Null(source.CircleOutput); Assert.Equal(GeometryOperation.Distance, ((GeometryParameters)geometry.Session.Parameters).Operation);
                Assert.Single(geometry.InputPorts, p => p.PropertyName == nameof(VisionToolNodeBase.BoundCircle));
                Assert.Single(source.OutputPorts, p => p.PropertyName == nameof(VisionToolNodeBase.CircleOutput));
            });
            await host.Invoke(() => geometry.ExecuteAsync(CancellationToken.None));
            host.Invoke(() => { var result = Assert.IsType<GeometryResult>(geometry.Result); Assert.True(result.Success, result.Message); Assert.Equal(5, result.Distance); Assert.Null(result.AppliedCircle); });
        }
        finally
        {
            await host.Invoke(() => vm.DisposeAsync().AsTask()); await host.Invoke(() => reopened.DisposeAsync().AsTask());
        }
    }

    private static (VisionToolNodeBase Source, VisionToolNodeBase Target) Setup(VisionFlowViewModel vm, int radius = 30)
    {
        var source = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/FindCircle"));
        var p = (CvCircleParameters)source.Session.Parameters;
        p.ExpectedCenter = new(80, 70); p.RadiusMin = radius - 5; p.RadiusMax = radius + 5;
        source.CircleOutputField = "Circle";
        var target = vm.Add(vm.ToolEntries.Single(e => e.Name == "Common/Geometry"));
        var recipe = (GeometryParameters)target.Session.Parameters;
        recipe.Operation = GeometryOperation.CircleDiameter; recipe.CircleCenter = new(1000, 1000); recipe.Radius = 99;
        target.CircleInputParameter = "Circle"; target.CircleSourceNodeId = source.Id;
        vm.Chart.Connections.Add(new()
        {
            SourceNodeId = source.Id, SourcePortId = source.OutputPorts.Single(p => p.Name == "Next_Out").Id,
            TargetNodeId = target.Id, TargetPortId = target.InputPorts.Single(p => p.Name == "Next_IN").Id
        });
        vm.SetImage(Frame(radius)); return (source, target);
    }
    private static VisionImage Frame(int radius, bool blank = false)
    {
        var pixels = new byte[180 * 160];
        if (!blank)
            for (var y = 0; y < 160; y++)
                for (var x = 0; x < 180; x++)
                    if ((x - 80) * (x - 80) + (y - 70) * (y - 70) <= radius * radius) pixels[y * 180 + x] = 255;
        var image = VisionImage.FromBuffer(180, 160, PixelFormat.Mono8, pixels); image.FrameId = 81; return image;
    }
    private static void Check(VisionToolNodeBase source, VisionToolNodeBase target, int expectedRadius, double recipeRadius = 99)
    {
        var circle = Assert.IsType<CvCircleResult>(source.Result); Assert.True(circle.Success, circle.Message);
        var measured = Assert.IsType<GeometryResult>(target.Result); Assert.True(measured.Success, measured.Message);
        Assert.InRange(measured.Diameter, 2 * expectedRadius - 4, 2 * expectedRadius + 4);
        Assert.Equal(2 * circle.Radius, measured.Diameter); Assert.Equal(circle.Center, measured.AppliedCenter);
        Assert.Equal(circle.Radius, measured.AppliedRadius); Assert.Equal(GeometryUnit.Pixel, measured.AppliedCircle!.Unit);
        Assert.Equal(source.ResultImageIdentity, measured.AppliedCircle.ImageIdentity);
        Assert.Equal(recipeRadius, ((GeometryParameters)target.Session.Parameters).Radius);
        Assert.Equal(new Point2D(1000, 1000), ((GeometryParameters)target.Session.Parameters).CircleCenter);
        var table = ((VisionToolInstanceWorkspace)target.Session).Editor.CurrentCard!.TableRows!;
        Assert.Equal(measured.Diameter, table[0]["直径"]); Assert.Equal(measured.AppliedRadius, table[0]["半径"]); Assert.Equal("Pixel", table[0]["单位"]);
    }
    private static void Select(FrameworkElement page, string name, object value)
    {
        Idle(page); var combo = Assert.Single(VisionFlowR0Tests.Descendants(page).OfType<ComboBox>(),
            c => c.DataContext is PropertyItemViewModel r && r.PropertyName == name);
        combo.SelectedValue = value; Idle(page); Assert.Equal(value, combo.SelectedValue);
    }
    private static void Idle(FrameworkElement view) { view.UpdateLayout(); view.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle); }
    private static string Artifact(string name)
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "circle-acceptance"); Directory.CreateDirectory(folder); return Path.Combine(folder, name);
    }
    private static void Snapshot(FrameworkElement view, string name)
    {
        Idle(view); var bitmap = new RenderTargetBitmap((int)view.ActualWidth, (int)view.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(view);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = File.Create(Artifact(name)); png.Save(stream);
    }
}
