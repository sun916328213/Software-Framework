using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using Vision.Controls.Services;
using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Tools;
using Vision.FlowNode;
using Vision.Tools.Halcon.Measurement;
using Vision.Tools.OpenCv.Measurement;
using Vision.Tools.OpenCv.Processing;

using WPFCustomControls.PropertyGrid.Core.ViewModels;
using PixelFormat = Vision.Core.Imaging.PixelFormat;
using CvLine = Vision.Tools.OpenCv.Measurement.FindLineParameters;
using CvResult = Vision.Tools.OpenCv.Measurement.FindLineResult;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
[Trait("Category", "Regression")]
public sealed class VisionLineBindingTests(WorkbenchAppFixture host)
{
    [Fact]
    public async Task OriginalCatalog_OffersCommonGeometry_AndExplicitLineFields()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create);
        try
        {
            host.Invoke(() =>
            {
                Assert.Single(vm.ToolEntries, e => e.Name == "Common/Geometry");
                var node = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/FindLine"));
                Assert.Same(node, vm.PropertyGridVM.TargetObject);
                Assert.Single(vm.PropertyGridVM.Groups.SelectMany(g => g.Items), r => r.PropertyName == "LineOutputField");
            });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualLineToDistance_UsesOriginalControlsAndCommand_PersistsRecipeNotRuntime_AndReopens(bool calipers)
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create); var reopened = host.Invoke(VisionFlowToolboxTests.Create);
        Window window = null!; VisionToolNodeBase source = null!, target = null!;
        var mode = calipers ? "calipers" : "band"; var path = Artifact($"line-distance-{mode}.flow");
        try
        {
            host.Invoke(() =>
            {
                var page = new VisionFlowView { DataContext = vm };
                window = new() { Content = page, Width = 1500, Height = 950, ShowInTaskbar = false, Left = -10000 }; window.Show();
                (source, target) = Setup(vm);
                if (calipers)
                {
                    var parameters = (CvLine)source.Session.Parameters;
                    parameters.SearchMode = LineSearchKind.Calipers; parameters.CaliperHalfLength = 12;
                    parameters.CaliperHalfWidth = 2; parameters.Transition = Vision.Tools.OpenCv.Measurement.EdgeTransition.Negative; // 向右路径的既有法向朝上，条带上沿是白→黑。
                    parameters.EdgeSelection = Vision.Tools.OpenCv.Measurement.CaliperEdgeSelection.First;
                }
                vm.Select(source); Idle(page); Select(page, "LineOutputField", "Line");
                vm.Select(target); Idle(page);
                Select(page, "LineInputParameter", "FirstLine"); Select(page, "LineSourceNodeId", source.Id);
                source.Name = "实际找线"; Idle(page); vm.Chart.Model.IsModified = false;
            });
            await host.Invoke(() => ((IAsyncRelayCommand)Assert.Single(VisionFlowR0Tests.Descendants(window).OfType<Button>(),
                b => System.Windows.Automation.AutomationProperties.GetName(b) == "当前流程执行").Command).ExecuteAsync(null));
            await host.Invoke(async () =>
            {
                Check(source, target);
                Assert.False(vm.Chart.Model.IsModified);
                var config = VisionToolConfiguration.FromState(target.CaptureDocumentState());
                Assert.Equal(1000, config.Parameters!.Value.GetProperty("FirstStart").GetProperty("X").GetDouble());
                Assert.True(vm.Chart.SaveDocument(path));
                var page = (VisionFlowView)window.Content;
                var combo = Assert.Single(VisionFlowR0Tests.Descendants(page).OfType<ComboBox>(),
                    c => c.DataContext is PropertyItemViewModel r && r.PropertyName == "LineSourceNodeId");
                await Task.Delay(350); Idle(page);
                Assert.Equal(source.Id, combo.SelectedValue); Assert.NotNull(combo.SelectedItem);
                Assert.Contains(VisionFlowR0Tests.Descendants(combo).OfType<TextBlock>(), t => t.Text.Contains(source.Name));
                Snapshot(page, $"line-binding-{mode}.png");
                var full = page.OpenToolEditor(target)!;
                var editor = (Vision.Controls.View.VisionToolEditorView)full.Content;
                ((TabItem)editor.DetailsPanel.FindName("ResultsTab")).IsSelected = true;
                Snapshot(full, $"measured-distance-{mode}.png"); full.Close(); window.Close();
            });
            await host.Invoke(() => vm.DisposeAsync().AsTask());
            host.Invoke(() =>
            {
                Assert.True(((VisionToolInstanceWorkspace)target.Session).Model.IsClosed);
                Assert.True(reopened.Chart.LoadDocument(path));
                source = reopened.Chart.Nodes.Select(n => n.Model).OfType<VisionToolNodeBase>().Single(n => n.ToolIdentity == "OpenCV/FindLine");
                target = reopened.Chart.Nodes.Select(n => n.Model).OfType<VisionToolNodeBase>().Single(n => n.ToolIdentity == "Common/Geometry");
                Assert.Null(source.LineOutput); Assert.Null(target.Result);
                Assert.Equal(source.Id, target.LineSourceNodeId);
                Assert.Equal("Line", source.LineOutputField); Assert.Equal("FirstLine", target.LineInputParameter);
                Assert.Equal(new Point2D(1000, 1000), ((GeometryParameters)target.Session.Parameters).FirstStart);
            });
            await host.Invoke(() => reopened.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() => Check(source, target));
        }
        finally
        {
            host.Invoke(() => window?.Close());
            await host.Invoke(() => vm.DisposeAsync().AsTask()); await host.Invoke(() => reopened.DisposeAsync().AsTask());
        }
    }

    [Fact]
    public async Task MissingFailedAndDisconnectedLine_NeverReturnsRecipeFive_OnlyExplicitUnbindRestoresIt()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create); VisionToolNodeBase source = null!, target = null!;
        try
        {
            host.Invoke(() => (source, target) = Setup(vm));
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.Invoke(() => target.ExecuteAsync(CancellationToken.None)));
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                Check(source, target); Assert.NotNull(target.BoundLine);
                var input = target.InputPorts.Single(p => p.PropertyName == nameof(VisionToolNodeBase.BoundLine));
                vm.Chart.Connections.Remove(vm.Chart.Connections.Single(c => c.TargetNodeId == target.Id && c.TargetPortId == input.Id));
                Assert.Null(target.BoundLine); Assert.Null(target.Result); // 原画布断线也必须立即撤输入，不只属性下拉。
                target.LineSourceNodeId = source.Id;
                source.Session.ClearResult(); Assert.Null(target.Result); Assert.Null(target.BoundLine);
            });
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.Invoke(() => target.ExecuteAsync(CancellationToken.None)));
            host.Invoke(() =>
            {
                target.LineSourceNodeId = ""; Assert.Equal("FirstLine", target.LineInputParameter);
            });
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                Assert.Null(target.Result); Assert.Contains("有效连接", ((VisionToolInstanceWorkspace)target.Session).Editor.CurrentOperationText);
                target.LineInputParameter = "";
            });
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                Assert.Equal(5, Assert.IsType<GeometryResult>(target.Result).Distance);
                target.LineInputParameter = "FirstLine"; target.LineSourceNodeId = source.Id;
                vm.SetImage(Frame(blank: true));
            });
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() => { Assert.False(source.Result?.Success == true); Assert.Null(source.LineOutput); Assert.Null(target.Result); });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task RemovingSource_AndRunningEdits_DoNotLeaveUsableGeometry()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create); VisionToolNodeBase source = null!, target = null!;
        try
        {
            host.Invoke(() => (source, target) = Setup(vm));
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                vm.Chart.Model.IsRunning = true;
                Assert.Throws<InvalidOperationException>(() => target.LineInputParameter = "");
                Assert.Throws<InvalidOperationException>(() => target.LineSourceNodeId = "");
                vm.Chart.Model.IsRunning = false;
                Assert.Equal("FirstLine", target.LineInputParameter); Assert.Equal(source.Id, target.LineSourceNodeId);
                vm.Chart.Nodes.Remove(vm.Chart.Nodes.Single(n => ReferenceEquals(n.Model, source)));
            });
            await host.Invoke(() => source.DisposeAsync().AsTask());
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.Invoke(() => target.ExecuteAsync(CancellationToken.None)));
            host.Invoke(() => { Assert.Null(target.Result); Assert.Equal("FirstLine", target.LineInputParameter); });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task SamePixelsButDifferentFrame_IsRejected()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create); VisionToolNodeBase source = null!, target = null!;
        try
        {
            host.Invoke(() => (source, target) = Setup(vm));
            await host.Invoke(() => source.ExecuteAsync(CancellationToken.None));
            host.Invoke(() =>
            {
                Assert.NotNull(source.LineOutput);
                var other = VisionImageSnapshot.Copy(source.Session.Image!); other.FrameId++;
                target.Session.SetImage(other);
            });
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.Invoke(() => target.ExecuteAsync(CancellationToken.None)));
            host.Invoke(() => { Assert.Null(target.Result); Assert.Contains("同一帧", ((VisionToolInstanceWorkspace)target.Session).Editor.CurrentOperationText); });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task DistinctCropsWithIdenticalPixels_CannotPretendSameCoordinates_ButSameExplicitCropCan()
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
                    var p = (CropParameters)crop.Session.Parameters; p.X = 0; p.Y = 0; p.Width = 200; p.Height = 140;
                }
                ((CropParameters)b.Session.Parameters).X = 5;
                // 水平条带在X方向恒定：不同裁剪原点也会产生同像素/hash/尺寸。
                vm.SetImage(Frame(fullWidth: true));
                VisionImageBinding.Select(source, a); VisionImageBinding.Select(target, b);
            });
            await host.Invoke(() => a.ExecuteAsync(CancellationToken.None)); await host.Invoke(() => b.ExecuteAsync(CancellationToken.None));
            await host.Invoke(() => source.ExecuteAsync(CancellationToken.None));
            host.Invoke(() =>
            {
                Assert.Equal(VisionToolConfiguration.ImageIdentity(a.OutputImage!), VisionToolConfiguration.ImageIdentity(b.OutputImage!));
                Assert.NotNull(source.LineOutput);
            });
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.Invoke(() => target.ExecuteAsync(CancellationToken.None)));
            host.Invoke(() =>
            {
                Assert.Null(target.Result); Assert.Contains("坐标来源不同", ((VisionToolInstanceWorkspace)target.Session).Editor.CurrentOperationText);
                VisionImageBinding.Select(target, a);
            });
            await host.Invoke(() => target.ExecuteAsync(CancellationToken.None));
            host.Invoke(() => Check(source, target));
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    private static (VisionToolNodeBase Source, VisionToolNodeBase Target) Setup(VisionFlowViewModel vm)
    {
        var source = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/FindLine"));
        var p = (CvLine)source.Session.Parameters;
        p.SearchMode = LineSearchKind.EdgeBand; p.LineStart = new(20, 80); p.LineEnd = new(180, 80); p.BandHalfWidth = 4;
        p.MinEdgePoints = 40; p.CannyLow = 30; p.CannyHigh = 80;
        source.LineOutputField = "Line";
        var target = vm.Add(vm.ToolEntries.Single(e => e.Name == "Common/Geometry"));
        var recipe = (GeometryParameters)target.Session.Parameters;
        recipe.FirstStart = new(1000, 1000); recipe.FirstEnd = new(1003, 1004);
        target.LineInputParameter = "FirstLine"; target.LineSourceNodeId = source.Id;
        vm.Chart.Connections.Add(new()
        {
            SourceNodeId = source.Id, SourcePortId = source.OutputPorts.Single(p => p.Name == "Next_Out").Id,
            TargetNodeId = target.Id, TargetPortId = target.InputPorts.Single(p => p.Name == "Next_IN").Id
        });
        vm.SetImage(Frame()); return (source, target);
    }
    private static VisionImage Frame(bool blank = false, bool fullWidth = false)
    {
        var pixels = new byte[220 * 160];
        if (!blank)
            for (var y = 80; y <= 120; y++)
                for (var x = fullWidth ? 0 : 20; x <= (fullWidth ? 219 : 180); x++) pixels[y * 220 + x] = 255;
        var image = VisionImage.FromBuffer(220, 160, PixelFormat.Mono8, pixels); image.FrameId = 71; return image;
    }
    private static void Check(VisionToolNodeBase source, VisionToolNodeBase target)
    {
        var line = Assert.IsType<CvResult>(source.Result); Assert.True(line.Success, line.Message);
        var measured = Assert.IsType<GeometryResult>(target.Result); Assert.True(measured.Success, measured.Message);
        Assert.InRange(measured.Distance, 158, 163); // 独立合成预期：X=20..180的水平线段，边缘拟合允许端点一像素差。
        Assert.Equal(line.Start, measured.AppliedStart); Assert.Equal(line.End, measured.AppliedEnd);
        Assert.Equal(Vision.Core.Results.GeometryUnit.Pixel, measured.AppliedLine!.Unit);
        Assert.Equal(source.ResultImageIdentity, measured.AppliedLine.ImageIdentity);
        Assert.Equal(new Point2D(1000, 1000), ((GeometryParameters)target.Session.Parameters).FirstStart);
        var table = ((VisionToolInstanceWorkspace)target.Session).Editor.CurrentCard!.TableRows!;
        Assert.Equal(measured.Distance, table[0]["距离"]); Assert.Equal("Pixel", table[0]["单位"]);
    }
    private static void Select(FrameworkElement page, string name, string value)
    {
        Idle(page); var combo = Assert.Single(VisionFlowR0Tests.Descendants(page).OfType<ComboBox>(),
            c => c.DataContext is PropertyItemViewModel r && r.PropertyName == name);
        combo.SelectedValue = value; Idle(page); Assert.Equal(value, combo.SelectedValue);
    }
    private static void Idle(FrameworkElement view) { view.UpdateLayout(); view.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle); }
    private static string Artifact(string name)
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "r1b-acceptance"); Directory.CreateDirectory(folder); return Path.Combine(folder, name);
    }
    private static void Snapshot(FrameworkElement view, string name)
    {
        Idle(view); var bitmap = new RenderTargetBitmap((int)view.ActualWidth, (int)view.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(view);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = File.Create(Artifact(name)); png.Save(stream);
    }
}
