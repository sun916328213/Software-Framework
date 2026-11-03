using System.IO;
using FlowEditor.Controls.Core;
using FlowEditor.Controls.Discovery;
using FlowEditor.Controls.Interfaces;
using FlowEditor.Controls.Nodes;
using FlowEditor.Controls.Services;
using FlowEditor.Controls.Services.Storage;
using FlowEditor.Controls.ViewModels;
using FlowEditor.Runtime;
using Prism.Ioc;
using Vision.Controls.Services;
using Vision.Core.Imaging;
using PixelFormat = Vision.Core.Imaging.PixelFormat;
using Vision.Core.Roi;
using Vision.Core.Geometry;
using Vision.Tools.OpenCv.Matching;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Vision.FlowNode;
using Vision.Tools.OpenCv.Inspection;
using Vision.Tools.OpenCv.Measurement;
using Vision.Tools.OpenCv.Processing;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
[Trait("Category", "Regression")]
public sealed class VisionToolCombinationTests(WorkbenchAppFixture host)
{
    [Fact]
    public async Task CropThresholdMorphologyBlob_UsesEachRealOutput_AndRoundTrips()
    {
        var vm = host.Invoke(Create); var restored = host.Invoke(Create); VisionToolNodeBase[] nodes = [];
        try
        {
            host.Invoke(() => nodes = Setup(vm, false));
            for (var i = 0; i < 3; i++)
            {
                await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
                host.Invoke(() => CheckChain(nodes));
            }
            var path = Artifact("processing-chain.flow");
            host.Invoke(() =>
            {
                Assert.True(vm.Chart.SaveDocument(path)); Assert.True(restored.Chart.LoadDocument(path));
                Assert.All(restored.Chart.Nodes, n => Assert.Null(((VisionToolNodeBase)n.Model).Result));
                var restoredWorkspace = (VisionToolInstanceWorkspace)((VisionToolNodeBase)restored.Chart.Nodes[3].Model).Session;
                Assert.Equal(1, ((BlobParameters)restoredWorkspace.Parameters).MinArea);
                var displayed = restoredWorkspace.Editor.BlobEditor!;
                Assert.Equal(1, displayed.MinArea); // 保存的是1，重开参数面板也必须显示1，不能仍显示构造默认10。
            });
            await host.Invoke(() => restored.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() => CheckChain(restored.Chart.Nodes.Select(n => (VisionToolNodeBase)n.Model).ToArray()));
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); await host.Invoke(() => restored.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task SharedThresholdOutput_FeedsTwoBranches_WithoutMutatingInputOrUnrelatedTool()
    {
        var vm = host.Invoke(Create); VisionToolNodeBase[] nodes = [];
        try
        {
            host.Invoke(() => nodes = Setup(vm, true));
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                CheckChain(nodes);
                Assert.Equal(new double[] { 1, 16, 24 }, Areas(nodes[4]));
                Assert.Equal(new double[] { 1, 16, 24 }, Areas(nodes[5]));
                Assert.Equal(41, Assert.IsType<ThresholdResult>(nodes[1].Result).WhitePixelCount);
                var unrelatedResult = nodes[5].Result;
                vm.Select(nodes[3]); Snapshot(vm, "processing-branches.png");
                nodes[0].Session.ClearResult();
                Assert.All(nodes.Skip(1).Take(4), n => Assert.Null(n.Result));
                Assert.Same(unrelatedResult, nodes[5].Result); // Next顺序不等于图像依赖。
                Snapshot(vm, "invalidated-branch.png");
            });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidatedSource_RetiresBorrowedInputAlongWholeChain_NotOnlyResults(bool parameterEdit)
    {
        var vm = host.Invoke(Create); VisionToolNodeBase[] nodes = [];
        try
        {
            host.Invoke(() => nodes = Setup(vm, false));
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                CheckChain(nodes);
                if (parameterEdit)
                {
                    ((CropParameters)nodes[0].Session.Parameters).X = 11;
                    ((VisionToolInstanceWorkspace)nodes[0].Session).Editor.ParametersChanged();
                }
                else nodes[0].Session.ClearResult();
                foreach (var node in nodes.Skip(1))
                {
                    Assert.Null(node.Result);
                    Assert.Null(node.Session.Image); // 预览不能继续把失效上游的旧24×24输入显示为当前输入。
                    Assert.Equal(48, node.Session.ConfiguredImage!.Width);
                }
            });
            await host.Invoke(() => ((VisionToolInstanceWorkspace)nodes[3].Session).Editor.CurrentCard!.RunCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                Assert.Null(nodes[3].Result); Assert.Null(nodes[3].Session.Image);
                Assert.Contains("尚无本次有效输出", ((VisionToolInstanceWorkspace)nodes[3].Session).Editor.CurrentCard!.StatusText);
            });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CropMatchAndBlob_CaptureReferenceFromActualBoundInput_AndRejectMixedCoordinates(bool sameInput)
    {
        var vm = host.Invoke(Create); var restored = host.Invoke(Create);
        VisionToolNodeBase crop = null!, locator = null!, target = null!;
        var sample = VisionImage.FromBuffer(7, 7, PixelFormat.Mono8, Enumerable.Range(0, 49).Select(i => (byte)((i * 31 + i / 7 * 17) % 170 + 10)).ToArray());
        try
        {
            host.Invoke(() =>
            {
                crop = vm.Add(OpenCvToolCatalog.Entries.Single(e => e.Name == "OpenCV/Crop"));
                locator = vm.Add(OpenCvToolCatalog.Entries.Single(e => e.Name == "OpenCV/TemplateMatch"));
                target = vm.Add(OpenCvToolCatalog.Entries.Single(e => e.Name == "OpenCV/Blob"));
                Connect(vm, crop, locator); Connect(vm, locator, target);
                var p = (CropParameters)crop.Session.Parameters; p.X = 4; p.Y = 4; p.Width = 52; p.Height = 36;
                var blob = ((VisionToolInstanceWorkspace)target.Session).Editor.BlobEditor!; blob.MinGray = 230; blob.MinArea = 1;
                vm.SetImage(MatchFrame(sample, 8, 8));
                if (sameInput) VisionImageBinding.Select(locator, crop);
                VisionImageBinding.Select(target, crop);
                ((VisionToolInstanceWorkspace)locator.Session).Editor.TemplateEditor!.Capture(sample);
                ((VisionToolInstanceWorkspace)target.Session).RoiEditor.AddRectangle(19.5, 8.5, 5, 5);
            });
            await host.Invoke(() => ((VisionToolInstanceWorkspace)crop.Session).Editor.CurrentCard!.RunCommand.ExecuteAsync(null));
            await host.Invoke(() => ((VisionToolInstanceWorkspace)locator.Session).Editor.CurrentCard!.RunCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                Assert.True(locator.Result!.Success);
                VisionFixtureBinding.Select(target, locator);
                if (!sameInput)
                {
                    var error = Assert.Throws<InvalidOperationException>(() => VisionFixtureBinding.CaptureReference(target));
                    Assert.Contains("同一输入图像坐标系", error.Message); Assert.False(target.HasFixtureReference);
                }
                else
                {
                    vm.Select(target); vm.CaptureFixtureReferenceCommand.Execute(null);
                    Assert.True(target.HasFixtureReference, vm.StatusText);
                    Assert.Equal(new Pose2D(7.5, 7.5, 0), target.ReferenceFixture);
                    vm.SetImage(MatchFrame(sample, 20, 15));
                }
            });
            if (!sameInput) return;
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                CheckCropFixture(target);
                var path = Artifact("crop-match-blob.flow"); Assert.True(vm.Chart.SaveDocument(path)); Assert.True(restored.Chart.LoadDocument(path));
            });
            await host.Invoke(() => restored.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                var copy = (VisionToolNodeBase)restored.Chart.Nodes[2].Model; CheckCropFixture(copy); restored.Select(copy);
                Snapshot(restored, "crop-match-blob.png");
            });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); await host.Invoke(() => restored.DisposeAsync().AsTask()); }
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BadIntermediateParameters_StopDependentChain_AndNameMissingImageSource(bool badThreshold)
    {
        var vm = host.Invoke(Create); VisionToolNodeBase[] nodes = [];
        try
        {
            host.Invoke(() => nodes = Setup(vm, true));
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                if (badThreshold) ((ThresholdParameters)nodes[1].Session.Parameters).MinGray = 300;
                else ((MorphologyParameters)nodes[2].Session.Parameters).KernelSize = 2;
                ((VisionToolInstanceWorkspace)nodes[badThreshold ? 1 : 2].Session).Editor.ParametersChanged();
            });
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                Assert.False(nodes[badThreshold ? 1 : 2].Result!.Success); Assert.Null(nodes[3].Result); Assert.Null(nodes[3].Session.Image);
                Assert.Null(nodes[5].Result); // 默认失败停止，不能把上次独立工具结果当成本轮结果。
            });
            var consumer = badThreshold ? nodes[4] : nodes[3];
            await host.Invoke(() => ((VisionToolInstanceWorkspace)consumer.Session).Editor.CurrentCard!.RunCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                Assert.Null(consumer.Result);
                var status = ((VisionToolInstanceWorkspace)consumer.Session).Editor.CurrentCard!.StatusText;
                Assert.Contains(nodes[badThreshold ? 1 : 2].Name, status);
                Assert.Contains("尚无本次有效输出", status);
            });
            if (!badThreshold)
            {
                await host.Invoke(() => ((VisionToolInstanceWorkspace)nodes[4].Session).Editor.CurrentCard!.RunCommand.ExecuteAsync(null));
                host.Invoke(() => Assert.Equal(new double[] { 1, 16, 24 }, Areas(nodes[4]))); // 仍有效的阈值支路可单独执行。
            }
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task GenericDataWire_ImmediatelyRetiresUnboundPreview_UntilConsumerPrepares()
    {
        var vm = host.Invoke(Create); VisionToolNodeBase[] nodes = [];
        try
        {
            host.Invoke(() => nodes = Setup(vm, false));
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                VisionImageBinding.Select(nodes[3], null); Assert.Equal(48, nodes[3].Session.Image!.Width);
                vm.Chart.Connections.Add(new() { SourceNodeId = nodes[2].Id,
                    SourcePortId = nodes[2].OutputPorts.Single(p => p.PropertyName == nameof(VisionToolNodeBase.OutputImage)).Id,
                    TargetNodeId = nodes[3].Id, TargetPortId = nodes[3].InputPorts.Single(p => p.PropertyName == nameof(VisionToolNodeBase.Image)).Id });
                Assert.Null(nodes[3].Session.Image); Assert.Null(nodes[3].Result);
            });
            await host.Invoke(() => ((VisionToolInstanceWorkspace)nodes[3].Session).Editor.CurrentCard!.RunCommand.ExecuteAsync(null));
            host.Invoke(() => Assert.Equal(new double[] { 16, 24 }, Areas(nodes[3])));
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Theory]
    [InlineData(MorphologyOperation.Erode, 12, new double[] { 4, 8 })]
    [InlineData(MorphologyOperation.Dilate, 93, new double[] { 9, 36, 48 })]
    [InlineData(MorphologyOperation.Open, 40, new double[] { 16, 24 })]
    [InlineData(MorphologyOperation.Close, 41, new double[] { 1, 16, 24 })]
    public async Task ProcessingChain_UsesConfiguredMorphology_WithIndependentPixelExpectations(MorphologyOperation operation, int whitePixels, double[] areas)
    {
        var vm = host.Invoke(Create); VisionToolNodeBase[] nodes = [];
        try
        {
            host.Invoke(() => { nodes = Setup(vm, false); ((MorphologyParameters)nodes[2].Session.Parameters).Operation = operation; });
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                Assert.Equal(whitePixels, Assert.IsType<MorphologyResult>(nodes[2].Result).WhitePixelCount);
                Assert.Equal(areas, Areas(nodes[3]));
                Assert.Equal(41, Assert.IsType<ThresholdResult>(nodes[1].Result).WhitePixelCount);
            });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task ValidBlackIntermediateImage_IsNotMissingData_AndNeverFallsBackToOriginal()
    {
        var vm = host.Invoke(Create); VisionToolNodeBase[] nodes = [];
        try
        {
            host.Invoke(() => { nodes = Setup(vm, false); ((ThresholdParameters)nodes[1].Session.Parameters).MinGray = 201; });
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                Assert.Equal(0, Assert.IsType<ThresholdResult>(nodes[1].Result).WhitePixelCount);
                Assert.Equal(0, Assert.IsType<MorphologyResult>(nodes[2].Result).WhitePixelCount);
                Assert.True(nodes[3].Result!.Success); Assert.Empty(Assert.IsType<BlobResult>(nodes[3].Result).Items);
                Assert.Equal(24, nodes[3].Session.Image!.Width); Assert.All(nodes[3].Session.Image!.Pixels, p => Assert.Equal(0, p));
                Assert.Equal(41, nodes[3].Session.ConfiguredImage!.Pixels.Count(p => p == 200));
            });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    private static void CheckCropFixture(VisionToolNodeBase target)
    {
        var item = Assert.Single(Assert.IsType<BlobResult>(target.Result).Items);
        Assert.Equal(9, item.Area); Assert.Equal(new Rect2D(30, 14, 3, 3), item.Bounds);
        Assert.Equal(new Pose2D(19.5, 8.5, 0), ((VisionToolInstanceWorkspace)target.Session).RoiEditor.Selected!.Pose);
        Assert.Equal(52, target.Session.Image!.Width); // 结果为裁剪局部坐标，不冒称任务原图坐标。
    }
    private static VisionImage MatchFrame(VisionImage sample, int x, int y)
    {
        var pixels = new byte[64 * 48]; for (var row = 0; row < 7; row++) Array.Copy(sample.Pixels, row * 7, pixels, (y + row) * 64 + x, 7);
        for (var row = 0; row < 3; row++) for (var col = 0; col < 3; col++) pixels[(y + 3 + row) * 64 + x + 14 + col] = 255;
        return VisionImage.FromBuffer(64, 48, PixelFormat.Mono8, pixels);
    }
    private static void Snapshot(VisionFlowViewModel vm, string name)
    {
        var page = new VisionFlowView { DataContext = vm }; var window = new Window { Content = page, Width = 1500, Height = 850, ShowInTaskbar = false, Left = -10000 };
        try
        {
            window.Show(); page.UpdateLayout(); page.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            var editor = Assert.IsType<Vision.Controls.View.VisionToolEditorView>(page.OpenToolEditor((VisionToolNodeBase)vm.Chart.SelectedNode!.Model)!.Content);
            Assert.Same(vm.SelectedEditor?.InputImage, editor.ImagePanel.ImageViewport.Image);
            var bitmap = new RenderTargetBitmap((int)page.ActualWidth, (int)page.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(page);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = File.Create(Artifact(name)); encoder.Save(stream);
        }
        finally { window.Close(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ThresholdToMeasurement_UsesRealMask_Persists_AndInvalidates(bool circle)
    {
        var vm = host.Invoke(Create); var reopened = host.Invoke(Create); VisionToolNodeBase source = null!, measurement = null!;
        var path = Artifact(circle ? "threshold-circle.flow" : "threshold-line.flow");
        try
        {
            host.Invoke(() =>
            {
                source = vm.Add(vm.ToolEntries.Single(t => t.Name == "OpenCV/Threshold"));
                measurement = vm.Add(vm.ToolEntries.Single(t => t.Name == (circle ? "OpenCV/FindCircle" : "OpenCV/FindLine")));
                Connect(vm, source, measurement); VisionImageBinding.Select(measurement, source);
                var pixels = new byte[200 * 200];
                for (var y = 0; y < 200; y++) for (var x = 0; x < 200; x++)
                    if (circle ? (x - 100) * (x - 100) + (y - 120) * (y - 120) <= 900 : y == 100 && x >= 20 && x <= 180)
                        pixels[y * 200 + x] = 180;
                ((ThresholdParameters)source.Session.Parameters).MinGray = 128;
                if (circle)
                {
                    var p = (FindCircleParameters)measurement.Session.Parameters;
                    p.ExpectedCenter = new(100, 120); p.RadiusMin = 20; p.RadiusMax = 40;
                }
                else
                {
                    var p = (FindLineParameters)measurement.Session.Parameters;
                    p.SearchMode = LineSearchKind.EdgeBand; p.LineStart = new(20, 100); p.LineEnd = new(180, 100);
                }
                vm.SetImage(VisionImage.FromBuffer(200, 200, PixelFormat.Mono8, pixels));
            });
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                CheckMeasurement(source, measurement, circle);
                Assert.True(vm.Chart.SaveDocument(path)); Assert.True(reopened.Chart.LoadDocument(path));
            });
            await host.Invoke(() => vm.DisposeAsync().AsTask());
            await host.Invoke(() => reopened.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                source = (VisionToolNodeBase)reopened.Chart.Nodes[0].Model; measurement = (VisionToolNodeBase)reopened.Chart.Nodes[1].Model;
                CheckMeasurement(source, measurement, circle);
                ((ThresholdParameters)source.Session.Parameters).MinGray = 300;
                ((VisionToolInstanceWorkspace)source.Session).Editor.ParametersChanged();
                Assert.Null(source.Result); Assert.Null(measurement.Result); Assert.Null(measurement.Session.Image);
            });
            await host.Invoke(() => reopened.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() => { Assert.False(source.Result!.Success); Assert.Null(measurement.Result); });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); await host.Invoke(() => reopened.DisposeAsync().AsTask()); }
    }

    private static void CheckMeasurement(VisionToolNodeBase source, VisionToolNodeBase measurement, bool circle)
    {
        Assert.True(source.Result!.Success, source.Result.Message); Assert.True(measurement.Result!.Success, measurement.Result.Message);
        Assert.Same(source.OutputImage, measurement.Session.Image);
        Assert.DoesNotContain((byte)255, source.Session.ConfiguredImage!.Pixels);
        Assert.Contains((byte)255, measurement.Session.Image!.Pixels);
        if (circle)
        {
            var r = Assert.IsType<FindCircleResult>(measurement.Result);
            Assert.InRange(r.Center.DistanceTo(new(100, 120)), 0, 4); Assert.InRange(r.Radius, 27, 33);
        }
        else
        {
            var r = Assert.IsType<FindLineResult>(measurement.Result);
            Assert.InRange(r.Start.Y, 97.5, 102.5); Assert.InRange(r.End.Y, 97.5, 102.5);
            Assert.True(r.Start.X <= 25); Assert.True(r.End.X >= 175); Assert.True(r.EdgePointCount >= 100);
        }
    }

    private static VisionToolNodeBase[] Setup(VisionFlowViewModel vm, bool branches)
    {
        var names = branches ? new[] { "Crop", "Threshold", "Morphology", "Blob", "Blob", "Blob" } : new[] { "Crop", "Threshold", "Morphology", "Blob" };
        var nodes = names.Select(name => vm.Add(OpenCvToolCatalog.Entries.Single(e => e.Name == "OpenCV/" + name))).ToArray();
        for (var i = 1; i < nodes.Length; i++) Connect(vm, nodes[i - 1], nodes[i]);
        VisionImageBinding.Select(nodes[1], nodes[0]); VisionImageBinding.Select(nodes[2], nodes[1]); VisionImageBinding.Select(nodes[3], nodes[2]);
        if (branches)
        {
            VisionImageBinding.Select(nodes[4], nodes[1]);
            for (var i = 0; i < nodes.Length; i++) { nodes[i].X = 40 + i % 2 * 220; nodes[i].Y = 40 + i / 2 * 150; }
        }
        var crop = (CropParameters)nodes[0].Session.Parameters; crop.X = 10; crop.Y = 8; crop.Width = 24; crop.Height = 24;
        ((ThresholdParameters)nodes[1].Session.Parameters).MinGray = 180;
        var morphology = (MorphologyParameters)nodes[2].Session.Parameters; morphology.Operation = MorphologyOperation.Open; morphology.KernelSize = 3;
        foreach (var node in nodes.Skip(3))
        {
            var editor = ((VisionToolInstanceWorkspace)node.Session).Editor.BlobEditor!;
            editor.MinGray = 128; editor.MinArea = 1;
        }
        var pixels = new byte[48 * 40];
        for (var y = 12; y < 16; y++) for (var x = 14; x < 20; x++) pixels[y * 48 + x] = 200;
        for (var y = 22; y < 26; y++) for (var x = 24; x < 28; x++) pixels[y * 48 + x] = 200;
        pixels[10 * 48 + 30] = 200;
        vm.SetImage(VisionImage.FromBuffer(48, 40, PixelFormat.Mono8, pixels));
        return nodes;
    }
    private static void CheckChain(VisionToolNodeBase[] nodes)
    {
        Assert.All(nodes.Take(4), n => Assert.True(n.Result?.Success == true));
        Assert.Equal(24, nodes[1].Session.Image!.Width); Assert.Equal(24, nodes[3].Session.Image!.Width);
        Assert.Equal(41, Assert.IsType<ThresholdResult>(nodes[1].Result).WhitePixelCount);
        Assert.Equal(40, Assert.IsType<MorphologyResult>(nodes[2].Result).WhitePixelCount);
        Assert.Equal(new double[] { 16, 24 }, Areas(nodes[3]));
        var bounds = Assert.IsType<BlobResult>(nodes[3].Result).Items.Select(i => i.Bounds).OrderBy(b => b.X).ToArray();
        Assert.Equal(new Vision.Core.Geometry.Rect2D(4, 4, 6, 4), bounds[0]); Assert.Equal(new Vision.Core.Geometry.Rect2D(14, 14, 4, 4), bounds[1]);
        Assert.Equal(41, nodes[0].Session.ConfiguredImage!.Pixels.Count(v => v == 200));
        Assert.Equal(0, nodes[0].Session.ConfiguredImage!.Pixels.Count(v => v == 255));
    }
    private static double[] Areas(VisionToolNodeBase node) => Assert.IsType<BlobResult>(node.Result).Items.Select(i => i.Area).Order().ToArray();
    private static void Connect(VisionFlowViewModel vm, VisionToolNodeBase source, VisionToolNodeBase target) => vm.Chart.Connections.Add(new()
    {
        SourceNodeId = source.Id, SourcePortId = source.OutputPorts.Single(p => p.Name == "Next_Out").Id,
        TargetNodeId = target.Id, TargetPortId = target.InputPorts.Single(p => p.Name == "Next_IN").Id
    });
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
        var folder = Path.Combine(AppContext.BaseDirectory, "combination-acceptance"); Directory.CreateDirectory(folder); return Path.Combine(folder, name);
    }
    private sealed class Dialogs : IDialogService
    {
        public string? ShowOpenFileDialog(string filter, string? defaultFileName = null) => null;
        public string? ShowSaveFileDialog(string filter, string defaultFileName, string defaultExt) => null;
        public bool? ShowConfirm(string message, string title) => false; public bool ConfirmYesNo(string message, string title) => false;
        public void ShowInfo(string message, string title = "提示") { } public void ShowError(string message, string title = "错误") => throw new InvalidOperationException(message);
    }
}
