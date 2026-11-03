using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FlowEditor.Controls.Core;
using FlowEditor.Controls.Discovery;
using FlowEditor.Controls.Interfaces;
using FlowEditor.Controls.Nodes;
using FlowEditor.Controls.Services;
using FlowEditor.Controls.Services.Storage;
using FlowEditor.Controls.ViewModels;
using FlowEditor.Runtime;
using Prism.Ioc;
using Vision.Controls.Models;
using Vision.Controls.Presentation;
using Vision.Controls.Services;
using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.FlowNode;
using Vision.Tools.OpenCv.Inspection;
using Vision.Tools.OpenCv.Processing;

using ImageFormat = Vision.Core.Imaging.PixelFormat;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
[Trait("Category", "Regression")]
public sealed class VisionBasicToolsE1Tests(WorkbenchAppFixture host)
{
    [Theory]
    [InlineData(ImageFormat.Bgr24)]
    [InlineData(ImageFormat.Mono16)]
    public async Task ConvertFilterThresholdBlobStatistics_UseActualOutputs_AndSaveCloseReopen(ImageFormat format)
    {
        var vm = host.Invoke(Create); var restored = host.Invoke(Create); VisionToolNodeBase[] nodes = [];
        try
        {
            host.Invoke(() => nodes = Setup(vm, format));
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                Check(nodes); vm.Select(nodes[4]); Snapshot(vm, $"{format}-statistics.png");
                Assert.True(vm.Chart.SaveDocument(Artifact($"{format}.flow")));
            });
            await host.Invoke(() => vm.DisposeAsync().AsTask());
            host.Invoke(() =>
            {
                Assert.True(((VisionToolInstanceWorkspace)nodes[4].Session).Model.IsClosed);
                Assert.True(restored.Chart.LoadDocument(Artifact($"{format}.flow")));
                Assert.All(restored.Chart.Nodes, n => Assert.Null(((VisionToolNodeBase)n.Model).Result));
                var conversion = Assert.IsType<ImageConvertParameters>(((VisionToolNodeBase)restored.Chart.Nodes[0].Model).Session.Parameters);
                Assert.Equal(10000, conversion.Mono16Minimum); Assert.Equal(20000, conversion.Mono16Maximum);
                Assert.Equal(ImageFilterMode.Median, Assert.IsType<ImageFilterParameters>(((VisionToolNodeBase)restored.Chart.Nodes[1].Model).Session.Parameters).Mode);
                Assert.Equal(64, Assert.IsType<GrayStatisticsParameters>(((VisionToolNodeBase)restored.Chart.Nodes[4].Model).Session.Parameters).HistogramBinWidth);
                Assert.Equal(format, ((VisionToolNodeBase)restored.Chart.Nodes[0].Model).Session.ConfiguredImage!.Format);
            });
            await host.Invoke(() => restored.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                var reopened = restored.Chart.Nodes.Select(n => (VisionToolNodeBase)n.Model).ToArray();
                Check(reopened); Assert.NotSame(nodes[0].Session, reopened[0].Session);
                restored.Select(reopened[0]);
                ((ImageConvertParameters)reopened[0].Session.Parameters).Mono16Maximum = 0;
                ((VisionToolInstanceWorkspace)reopened[0].Session).Editor.ParametersChanged();
                Assert.All(reopened, n => Assert.Null(n.Result));
                Assert.All(reopened.Skip(1), n => Assert.Null(n.Session.Image));
                Assert.Null(((VisionToolInstanceWorkspace)reopened[4].Session).Editor.CurrentCard!.TableRows);
            });
            await host.Invoke(() => restored.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                Assert.False(((VisionToolNodeBase)restored.Chart.Nodes[0].Model).Result!.Success);
                Assert.All(restored.Chart.Nodes.Skip(1), n => Assert.Null(((VisionToolNodeBase)n.Model).Result));
            });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); await host.Invoke(() => restored.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task StatisticsTable_IsReadOnly_EmptyRoiClearsRowsAndHistogram_NotSuccessfulZero()
    {
        var vm = host.Invoke(Create); VisionToolNodeBase statistics = null!;
        try
        {
            host.Invoke(() =>
            {
                statistics = vm.Add(OpenCvToolCatalog.Entries.Single(e => e.Name == "OpenCV/GrayStatistics"));
                vm.SetImage(VisionImage.FromBuffer(2, 2, ImageFormat.Mono8, [0, 2, 4, 6]));
            });
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                var workspace = (VisionToolInstanceWorkspace)statistics.Session;
                var table = workspace.Editor.CurrentCard!.TableRows!;
                Assert.Equal(6, table.Count); Assert.False(table.AllowEdit); Assert.False(table.AllowNew);
                Assert.Equal("平均灰度", table[3]["统计项"]); Assert.Equal(3d, table[3]["数值"]);
                Assert.Equal(3d, table[4]["数值"]); Assert.Equal(Math.Sqrt(5), (double)table[5]["数值"], 8);
                var details = workspace.Editor.CurrentCard.ResultDetails;
                Assert.Contains("总体标准差", details); Assert.Contains("256档", details); Assert.Contains("6: 1", details);
                workspace.RoiEditor.AddRectangle(100, 100, 2, 2);
                Assert.Null(workspace.Editor.CurrentCard.TableRows);
            });
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                var result = Assert.IsType<GrayStatisticsResult>(statistics.Result); Assert.False(result.Success); Assert.Empty(result.Histogram);
                Assert.Null(((VisionToolInstanceWorkspace)statistics.Session).Editor.CurrentCard!.TableRows);
                Assert.Contains("没有有效像素", result.Message);
            });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task StatisticsSearchRoi_FollowsCurrentMatchedInput_WithoutMovingReferenceRoi()
    {
        var vm = host.Invoke(Create); VisionToolNodeBase locator = null!, statistics = null!;
        VisionImage Frame(int dx, int dy)
        {
            byte[] sample = [20, 100, 40, 150, 30, 210, 70, 180, 50]; var pixels = new byte[16 * 12];
            for (var y = 0; y < 3; y++) for (var x = 0; x < 3; x++) pixels[(y + 2 + dy) * 16 + x + 2 + dx] = sample[y * 3 + x];
            for (var y = 3; y < 5; y++) for (var x = 9; x < 11; x++) pixels[(y + dy) * 16 + x + dx] = 255;
            return VisionImage.FromBuffer(16, 12, ImageFormat.Mono8, pixels);
        }
        try
        {
            host.Invoke(() =>
            {
                locator = vm.Add(OpenCvToolCatalog.Entries.Single(e => e.Name == "OpenCV/TemplateMatch"));
                statistics = vm.Add(OpenCvToolCatalog.Entries.Single(e => e.Name == "OpenCV/GrayStatistics"));
                vm.Chart.Connections.Add(new() { SourceNodeId = locator.Id, SourcePortId = locator.OutputPorts.Single(p => p.Name == "Next_Out").Id,
                    TargetNodeId = statistics.Id, TargetPortId = statistics.InputPorts.Single(p => p.Name == "Next_IN").Id });
                vm.SetImage(Frame(0, 0)); var source = (VisionToolInstanceWorkspace)locator.Session;
                source.RoiEditor.AddRectangle(3.5, 3.5, 3, 3); source.Editor.SetTemplateFromRoiCommand.Execute(null);
                ((VisionToolInstanceWorkspace)statistics.Session).RoiEditor.AddRectangle(9.5, 3.5, 2, 2);
            });
            await host.Invoke(() => locator.Session.RunAsync(CancellationToken.None));
            host.Invoke(() =>
            {
                Assert.True(locator.Result?.Success == true); vm.Select(statistics); Assert.True(vm.CanFollowFixture);
                vm.SelectedFixtureSource = locator.Id; vm.CaptureFixtureReferenceCommand.Execute(null);
                Assert.True(statistics.HasFixtureReference); vm.SetImage(Frame(2, 1));
            });
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                var result = Assert.IsType<GrayStatisticsResult>(statistics.Result); Assert.True(result.Success, result.Message);
                Assert.Equal(4, result.PixelCount); Assert.Equal(255, result.Mean); Assert.Equal(0, result.StandardDeviation);
                var workspace = (VisionToolInstanceWorkspace)statistics.Session;
                Assert.Equal(new Pose2D(9.5, 3.5, 0), workspace.RoiEditor.Selected!.Pose);
                Assert.Equal(new Pose2D(11.5, 4.5, 0), workspace.Model.ExecutionSearchRoi!.Pose);
            });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task E1SecondBatch_SegmentationChainsRoundTrip_AndInvalidationDoesNotReuseOldMask(bool color)
    {
        var vm = host.Invoke(Create); var restored = host.Invoke(Create); VisionToolNodeBase[] nodes = [];
        var file = color ? "e1b-color" : "e1b-adaptive";
        try
        {
            host.Invoke(() => nodes = SetupSegmentation(vm, color));
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                CheckSegmentation(nodes, color); vm.Select(nodes[color ? 0 : 3]); Snapshot(vm, file + ".png");
                Assert.True(vm.Chart.SaveDocument(Artifact(file + ".flow")));
            });
            await host.Invoke(() => vm.DisposeAsync().AsTask());
            host.Invoke(() =>
            {
                Assert.True(((VisionToolInstanceWorkspace)nodes[0].Session).Model.IsClosed);
                Assert.True(restored.Chart.LoadDocument(Artifact(file + ".flow")));
                Assert.All(restored.Chart.Nodes, n => Assert.Null(((VisionToolNodeBase)n.Model).Result));
                nodes = restored.Chart.Nodes.Select(n => (VisionToolNodeBase)n.Model).ToArray();
                if (color)
                {
                    var p = Assert.IsType<ColorSegmentParameters>(nodes[0].Session.Parameters);
                    Assert.Equal(ColorSegmentSpace.Hsv, p.Space); Assert.Equal(170, p.FirstMinimum); Assert.Equal(10, p.FirstMaximum);
                }
                else
                {
                    var p = Assert.IsType<IntensityTransformParameters>(nodes[0].Session.Parameters);
                    Assert.Equal(IntensityTransformMode.Gamma, p.Mode); Assert.Equal(2, p.Gamma);
                    var a = Assert.IsType<AdaptiveThresholdParameters>(nodes[1].Session.Parameters);
                    Assert.Equal(AdaptiveThresholdMode.Gaussian, a.Mode); Assert.Equal(3, a.BlockSize); Assert.Equal(0, a.Offset); Assert.False(a.Invert);
                }
            });
            await host.Invoke(() => restored.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() => CheckSegmentation(nodes, color));
            if (color)
            {
                host.Invoke(() =>
                {
                    var p = (ColorSegmentParameters)nodes[0].Session.Parameters; p.FirstMinimum = 50; p.FirstMaximum = 70;
                    ((VisionToolInstanceWorkspace)nodes[0].Session).Editor.ParametersChanged();
                });
                await host.Invoke(() => restored.RunFlowCommand.ExecuteAsync(null));
                host.Invoke(() =>
                {
                    Assert.All(nodes, n => Assert.True(n.Result?.Success == true));
                    Assert.Equal(0, Assert.IsType<ColorSegmentResult>(nodes[0].Result).WhitePixelCount);
                    Assert.Empty(Assert.IsType<BlobResult>(nodes[2].Result).Items);
                    Assert.Equal(120, Assert.IsType<GrayStatisticsResult>(nodes[3].Result).PixelCount);
                    Assert.Equal(0, Assert.IsType<GrayStatisticsResult>(nodes[3].Result).Mean);
                });
            }
            host.Invoke(() =>
            {
                if (color)
                {
                    var p = (ColorSegmentParameters)nodes[0].Session.Parameters;
                    p.Space = ColorSegmentSpace.Rgb; p.FirstMinimum = 200; p.FirstMaximum = 10;
                }
                else ((AdaptiveThresholdParameters)nodes[1].Session.Parameters).BlockSize = 4;
                ((VisionToolInstanceWorkspace)nodes[color ? 0 : 1].Session).Editor.ParametersChanged();
                Assert.All(nodes.Skip(color ? 0 : 1), n => Assert.Null(n.Result));
                Assert.All(nodes.Skip(color ? 1 : 2), n => Assert.Null(n.Session.Image));
                Assert.Null(((VisionToolInstanceWorkspace)nodes[3].Session).Editor.CurrentCard!.TableRows);
            });
            await host.Invoke(() => restored.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                Assert.False(nodes[color ? 0 : 1].Result!.Success);
                Assert.All(nodes.Skip(color ? 1 : 2), n => Assert.Null(n.Result));
            });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); await host.Invoke(() => restored.DisposeAsync().AsTask()); }
    }
    private static VisionToolNodeBase[] SetupSegmentation(VisionFlowViewModel vm, bool color)
    {
        var names = color ? new[] { "ColorSegment", "Morphology", "Blob", "GrayStatistics" }
            : new[] { "IntensityTransform", "AdaptiveThreshold", "Blob", "GrayStatistics" };
        var nodes = names.Select(name =>
        {
            var item = vm.ToolboxCategories.SelectMany(c => c.Items).Single(i => i.DisplayName == "OpenCV/" + name);
            vm.AddPaletteCommand.Execute(item); return (VisionToolNodeBase)vm.Chart.Nodes.Last().Model;
        }).ToArray();
        for (var i = 1; i < nodes.Length; i++)
        {
            vm.Chart.Connections.Add(new() { SourceNodeId = nodes[i - 1].Id, SourcePortId = nodes[i - 1].OutputPorts.Single(p => p.Name == "Next_Out").Id,
                TargetNodeId = nodes[i].Id, TargetPortId = nodes[i].InputPorts.Single(p => p.Name == "Next_IN").Id });
            VisionImageBinding.Select(nodes[i], nodes[i == 3 ? 1 : i - 1]);
        }
        ((VisionToolInstanceWorkspace)nodes[2].Session).Editor.BlobEditor!.MinArea = 1;
        foreach (var (node, i) in nodes.Select((node, i) => (node, i))) { node.X = 30 + i % 2 * 220; node.Y = 30 + i / 2 * 150; }
        if (color)
        {
            ((MorphologyParameters)nodes[1].Session.Parameters).Operation = MorphologyOperation.Open;
            var pixels = new byte[12 * 10 * 3];
            for (var y = 0; y < 10; y++) for (var x = 0; x < 12; x++)
            {
                var red = x >= 4 && x <= 6 && y >= 3 && y <= 5 || x == 1 && y == 1;
                var offset = (y * 12 + x) * 3;
                pixels[offset] = red ? (byte)(x % 2 == 0 ? 0 : 10) : (byte)255;
                pixels[offset + 2] = red ? (byte)255 : (byte)0;
            }
            vm.SetImage(VisionImage.FromBuffer(12, 10, ImageFormat.Bgr24, pixels));
        }
        else
        {
            var p = (IntensityTransformParameters)nodes[0].Session.Parameters; p.Mode = IntensityTransformMode.Gamma; p.Gamma = 2;
            var a = (AdaptiveThresholdParameters)nodes[1].Session.Parameters; a.Mode = AdaptiveThresholdMode.Gaussian; a.BlockSize = 3; a.Offset = 0;
            var pixels = Enumerable.Repeat((byte)10, 81).ToArray(); pixels[40] = 100;
            vm.SetImage(VisionImage.FromBuffer(9, 9, ImageFormat.Mono8, pixels));
        }
        return nodes;
    }
    private static void CheckSegmentation(VisionToolNodeBase[] nodes, bool color)
    {
        Assert.All(nodes, n => Assert.True(n.Result?.Success == true, n.Result?.Message));
        var expected = color ? 9 : 1; var count = color ? 120 : 81;
        if (color)
        {
            Assert.Equal(10, Assert.IsType<ColorSegmentResult>(nodes[0].Result).WhitePixelCount);
            Assert.Equal(9, Assert.IsType<MorphologyResult>(nodes[1].Result).WhitePixelCount);
            var table = ((VisionToolInstanceWorkspace)nodes[0].Session).Editor.CurrentCard!.TableRows!;
            Assert.False(table.AllowEdit); Assert.Equal(10, table[0]["前景像素数"]);
        }
        else
        {
            Assert.Equal(39, Assert.IsType<IntensityTransformResult>(nodes[0].Result).OutputImage!.Pixels[40]);
            Assert.Equal(1, Assert.IsType<AdaptiveThresholdResult>(nodes[1].Result).WhitePixelCount);
        }
        Assert.Equal(expected, Assert.Single(Assert.IsType<BlobResult>(nodes[2].Result).Items).Area);
        var statistics = Assert.IsType<GrayStatisticsResult>(nodes[3].Result);
        Assert.Equal(count, statistics.PixelCount); Assert.Equal(255d * expected / count, statistics.Mean, 8);
        Assert.Equal(expected, statistics.Histogram[255]); Assert.Equal(count - expected, statistics.Histogram[0]);
        Assert.Same(nodes[1].OutputImage, nodes[3].Session.Image);
    }

    private static VisionToolNodeBase[] Setup(VisionFlowViewModel vm, ImageFormat format)
    {
        var nodes = new[] { "ImageConvert", "ImageFilter", "Threshold", "Blob", "GrayStatistics" }.Select(name =>
        {
            // 与实际左侧工具箱使用同一项、同一添加命令，不为测试另建节点工厂。
            var item = vm.ToolboxCategories.SelectMany(c => c.Items).Single(i => i.DisplayName == "OpenCV/" + name);
            Assert.True(vm.AddPaletteCommand.CanExecute(item)); vm.AddPaletteCommand.Execute(item);
            return (VisionToolNodeBase)vm.Chart.Nodes.Last().Model;
        }).ToArray();
        for (var i = 1; i < nodes.Length; i++) vm.Chart.Connections.Add(new()
        {
            SourceNodeId = nodes[i - 1].Id, SourcePortId = nodes[i - 1].OutputPorts.Single(p => p.Name == "Next_Out").Id,
            TargetNodeId = nodes[i].Id, TargetPortId = nodes[i].InputPorts.Single(p => p.Name == "Next_IN").Id
        });
        VisionImageBinding.Select(nodes[1], nodes[0]); VisionImageBinding.Select(nodes[2], nodes[1]);
        VisionImageBinding.Select(nodes[3], nodes[2]); VisionImageBinding.Select(nodes[4], nodes[1]);
        var convert = (ImageConvertParameters)nodes[0].Session.Parameters; convert.Mono16Minimum = 10000; convert.Mono16Maximum = 20000;
        ((ImageFilterParameters)nodes[1].Session.Parameters).Mode = ImageFilterMode.Median;
        ((ThresholdParameters)nodes[2].Session.Parameters).MinGray = 100;
        ((VisionToolInstanceWorkspace)nodes[3].Session).Editor.BlobEditor!.MinArea = 1;
        ((GrayStatisticsParameters)nodes[4].Session.Parameters).HistogramBinWidth = 64;
        ((VisionToolInstanceWorkspace)nodes[4].Session).RoiEditor.AddRectangle(4, 4, 9, 9);
        foreach (var (node, i) in nodes.Select((node, i) => (node, i))) { node.X = 30 + i % 2 * 220; node.Y = 30 + i / 2 * 150; }
        var bpp = format == ImageFormat.Mono16 ? 2 : 3; var stride = 9 * bpp + 2;
        var pixels = Enumerable.Repeat((byte)99, stride * 9).ToArray();
        for (var y = 0; y < 9; y++) for (var x = 0; x < 9; x++)
        {
            var foreground = x >= 3 && x <= 5 && y >= 3 && y <= 5 || x == 1 && y == 1;
            var offset = y * stride + x * bpp;
            if (format == ImageFormat.Mono16)
            {
                var value = foreground ? 15000 : 10000; pixels[offset] = (byte)(value & 255); pixels[offset + 1] = (byte)(value >> 8);
            }
            else for (var c = 0; c < 3; c++) pixels[offset + c] = foreground ? (byte)128 : (byte)0;
        }
        var image = VisionImage.FromBuffer(9, 9, format, pixels, stride); image.FrameId = 41; vm.SetImage(image);
        return nodes;
    }
    private static void Check(VisionToolNodeBase[] nodes)
    {
        Assert.All(nodes, n => Assert.True(n.Result?.Success == true, n.Result?.Message));
        Assert.Equal(10, Assert.IsType<ImageConvertResult>(nodes[0].Result).OutputImage!.Pixels.Count(p => p == 128));
        var filtered = Assert.IsType<ImageFilterResult>(nodes[1].Result).OutputImage!;
        Assert.Equal(5, filtered.Pixels.Count(p => p == 128)); Assert.Equal(41, filtered.FrameId);
        Assert.Equal(nodes[0].Session.ConfiguredImage!.Timestamp, filtered.Timestamp);
        Assert.Equal(5, Assert.IsType<ThresholdResult>(nodes[2].Result).WhitePixelCount);
        Assert.Equal(5, Assert.Single(Assert.IsType<BlobResult>(nodes[3].Result).Items).Area);
        var statistics = Assert.IsType<GrayStatisticsResult>(nodes[4].Result);
        Assert.Equal(81, statistics.PixelCount); Assert.Equal(0, statistics.Minimum); Assert.Equal(128, statistics.Maximum);
        Assert.Equal(0, statistics.Median); Assert.Equal(640d / 81, statistics.Mean, 9);
        Assert.Equal(64, statistics.HistogramBinWidth); Assert.Equal(4, statistics.Histogram.Length);
        Assert.Equal(76, statistics.Histogram[0]); Assert.Equal(5, statistics.Histogram[2]);
        Assert.Same(filtered, nodes[4].Session.Image); Assert.False(nodes[4].Session.SupportsOutputImage);
    }
    private static void Snapshot(VisionFlowViewModel vm, string name)
    {
        var page = new VisionFlowView { DataContext = vm }; var window = new Window { Content = page, Width = 1600, Height = 900, ShowInTaskbar = false, Left = -10000 };
        try
        {
            window.Show(); page.UpdateLayout(); page.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            var editor = Assert.IsType<Vision.Controls.View.VisionToolEditorView>(page.OpenToolEditor((VisionToolNodeBase)vm.Chart.SelectedNode!.Model)!.Content);
            ((TabItem)editor.DetailsPanel.FindName("ResultsTab")).IsSelected = true; page.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)page.ActualWidth, (int)page.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(page);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = File.Create(Artifact(name)); encoder.Save(stream);
        }
        finally { window.Close(); }
    }
    private static VisionFlowViewModel Create()
    {
        var catalog = new VisionBrowserCatalog(OpenCvToolCatalog.Entries, [], _ => throw new NotSupportedException(), () => null);
        var nodes = new FlowNodeCatalog([typeof(NodeModel).Assembly, typeof(VisionToolNodeBase).Assembly]);
        var factory = new VisionNodeFactory(c => new VisionToolInstanceWorkspace(catalog, catalog.Tools.Single(t => t.Name == c.ToolIdentity), c.Provider), new ReflectionNodeFactory(null, nodes), nodes);
        var dialogs = new Dialogs();
        var service = new FlowChartService(new VisionTaskDocumentStore(new JsonFlowDocumentStore()), new FlowChartExecutionEngine(), dialogs,
            uiDispatcher: new FlowEditor.Controls.Presentation.Threading.WpfUiDispatcher(), documentMapper: new FlowDocumentMapper(factory, nodes));
        return new(new FlowChartViewModel(service, ContainerLocator.Container.Resolve<IExportService>(), dialogs, new ConnectionService()), catalog, true);
    }
    private static string Artifact(string name)
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "e1-acceptance"); Directory.CreateDirectory(folder); return Path.Combine(folder, name);
    }
    private sealed class Dialogs : IDialogService
    {
        public string? ShowOpenFileDialog(string filter, string? defaultFileName = null) => null;
        public string? ShowSaveFileDialog(string filter, string defaultFileName, string defaultExt) => null;
        public bool? ShowConfirm(string message, string title) => false; public bool ConfirmYesNo(string message, string title) => false;
        public void ShowInfo(string message, string title = "提示") { } public void ShowError(string message, string title = "错误") => throw new InvalidOperationException(message);
    }
}
