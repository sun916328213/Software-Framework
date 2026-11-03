using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using Vision.Controls.Presentation;
using Vision.Controls.Services;
using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.FlowNode;
using Vision.Tools.OpenCv.Acquisition;
using Vision.Tools.OpenCv.Inspection;
using Vision.Tools.OpenCv.Processing;

using WPFCustomControls.PropertyGrid.Views;
using ImageFormat = Vision.Core.Imaging.PixelFormat;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
[Trait("Category", "Regression")]
public sealed class VisionFileAcquisitionTests(WorkbenchAppFixture host)
{
    [Fact]
    public async Task FileSource_HasOriginalPublicImageOutput_WithoutTaskInput()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create);
        try
        {
            host.Invoke(() =>
            {
                foreach (var name in new[] { "OpenCV/ImageFile", "HALCON/ImageFile" })
                {
                    var source = vm.Add(Assert.Single(vm.ToolEntries, e => e.Name == name));
                    Assert.True(source.Session.SupportsOutputImage);
                    Assert.Single(source.OutputPorts, p => p.PropertyName == nameof(VisionToolNodeBase.OutputImage));
                    Assert.Null(source.Session.ConfiguredImage);
                    Assert.IsType<VisionToolInstanceWorkspace>(source.Session);
                }
            });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task LegacyHalconFileFlow_WithoutPublicOutputPort_RestoresOldParametersAndAddsCurrentPort()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create); var reopened = host.Invoke(VisionFlowToolboxTests.Create);
        try
        {
            host.Invoke(() =>
            {
                var source = vm.Add(vm.ToolEntries.Single(e => e.Name == "HALCON/ImageFile"));
                ((Vision.Tools.Halcon.Acquisition.ImageFileParameters)source.Session.Parameters).FilePath = "legacy.png";
                var path = Artifact("legacy-halcon-file.flow"); Assert.True(vm.Chart.SaveDocument(path));
                var store = new FlowEditor.Controls.Services.Storage.JsonFlowDocumentStore();
                var document = store.Load<FlowEditor.Core.Models.FlowChartDocumentModel>(path);
                var node = Assert.Single(document.Nodes);
                node.CustomOutputPorts = node.CustomOutputPorts.Where(p => p.BoundProperty != nameof(VisionToolNodeBase.OutputImage)).ToList();
                node.PortIdMap.Remove("OUT:输出图像"); store.Save(path, document);
                Assert.True(reopened.Chart.LoadDocument(path));
                var restored = (VisionToolNodeBase)Assert.Single(reopened.Chart.Nodes).Model;
                Assert.Equal(source.Id, restored.Id);
                Assert.Equal("legacy.png", ((Vision.Tools.Halcon.Acquisition.ImageFileParameters)restored.Session.Parameters).FilePath);
                Assert.Single(restored.OutputPorts, p => p.PropertyName == nameof(VisionToolNodeBase.OutputImage));
                Assert.Null(restored.Result); Assert.Null(restored.OutputImage);
            });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); await host.Invoke(() => reopened.DisposeAsync().AsTask()); }
    }

    [Theory]
    [InlineData(ImageFormat.Mono16)]
    [InlineData(ImageFormat.Bgr24)]
    public async Task ActualFileConvertThresholdBlob_UsesNativeCommands_AndClosesReopensInvalidates(ImageFormat format)
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create); var reopened = host.Invoke(VisionFlowToolboxTests.Create);
        Window window = null!, full = null!; VisionToolNodeBase[] nodes = []; var path = Artifact(format + ".flow");
        try
        {
            host.Invoke(() =>
            {
                var page = new VisionFlowView { DataContext = vm };
                window = new() { Content = page, Width = 1500, Height = 900, ShowInTaskbar = false, Left = -10000 }; window.Show();
                var input = Frame(format); var file = Artifact("中文 输入 " + format + ".png");
                WriteImage(input, file);
                var source = vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/ImageFile")); Idle(page);
                var picker = Assert.Single(VisionFlowR0Tests.Descendants(page).OfType<FilePathEditor>());
                picker.PickFile = (_, _) => file; ((Button)picker.FindName("BrowseButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal(file, ((ImageFileParameters)source.Session.Parameters).FilePath);
                nodes = new[] { source }.Concat(new[] { "ImageConvert", "Threshold", "Blob" }.Select(name => vm.Add(vm.ToolEntries.Single(e => e.Name == "OpenCV/" + name)))).ToArray();
                for (var i = 1; i < nodes.Length; i++)
                {
                    vm.Chart.Connections.Add(new()
                    {
                        SourceNodeId = nodes[i - 1].Id, SourcePortId = nodes[i - 1].OutputPorts.Single(p => p.Name == "Next_Out").Id,
                        TargetNodeId = nodes[i].Id, TargetPortId = nodes[i].InputPorts.Single(p => p.Name == "Next_IN").Id
                    });
                    VisionImageBinding.Select(nodes[i], nodes[i - 1]);
                }
                var p = (ImageConvertParameters)nodes[1].Session.Parameters; p.Mono16Minimum = 10000; p.Mono16Maximum = 20000;
                ((ThresholdParameters)nodes[2].Session.Parameters).MinGray = 100;
                ((VisionToolInstanceWorkspace)nodes[3].Session).Editor.BlobEditor!.MinArea = 1;
                Assert.All(nodes, n => Assert.Null(n.Session.ConfiguredImage));
                for (var i = 0; i < nodes.Length; i++) { nodes[i].X = 30 + i % 2 * 240; nodes[i].Y = 30 + i / 2 * 160; }
            });
            // 真正的原容器当前页命令，不引入测试/视觉专用执行路径。
            await host.Invoke(() => ((IAsyncRelayCommand)Assert.Single(VisionFlowR0Tests.Descendants(window).OfType<Button>(), b => System.Windows.Automation.AutomationProperties.GetName(b) == "当前流程执行").Command).ExecuteAsync(null));
            host.Invoke(() =>
            {
                Check(nodes, format); Assert.True(vm.Chart.SaveDocument(path));
                vm.Select(nodes[0]); var page = (FrameworkElement)window.Content; Idle(page); Snapshot(page, format + "-source.png");
                full = ((VisionFlowView)page).OpenToolEditor(nodes[0])!; Idle(full);
                ((VisionToolInstanceWorkspace)nodes[0].Session).Model.SelectedImageView = Vision.Controls.Models.ImageViewKind.Result;
            });
            await host.Invoke(async () =>
            {
                // 等原MaterialDesign展开动画结束再留图；不以截图计时作为算法/性能断言。
                await Task.Delay(350);
                Snapshot(full, format + "-editor.png"); full.Close(); window.Close();
            });
            await host.Invoke(() => vm.DisposeAsync().AsTask());
            host.Invoke(() =>
            {
                Assert.True(((VisionToolInstanceWorkspace)nodes[0].Session).Model.IsClosed);
                Assert.True(reopened.Chart.LoadDocument(path));
                nodes = reopened.Chart.Nodes.Select(n => (VisionToolNodeBase)n.Model).ToArray();
                Assert.All(nodes, n => Assert.Null(n.Result));
                // 源即使存在不支持的任务输入也只读取文件；下游必须用真实连线而非任务图。
                reopened.SetImage(VisionImage.FromBuffer(1, 1, ImageFormat.Float32, [0, 0, 0, 0]));
            });
            await host.Invoke(() => reopened.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                Check(nodes, format);
                var p = (ImageFileParameters)nodes[0].Session.Parameters;
                WriteImage(VisionImage.FromBuffer(9, 9, format, new byte[81 * (format == ImageFormat.Mono16 ? 2 : 3)]), p.FilePath);
            });
            await host.Invoke(() => reopened.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                Assert.All(nodes, n => Assert.True(n.Result!.Success, n.Result.Message));
                Assert.All(nodes[0].OutputImage!.Pixels, b => Assert.Equal(0, b));
                Assert.Equal(0, Assert.IsType<ThresholdResult>(nodes[2].Result).WhitePixelCount);
                Assert.Empty(Assert.IsType<BlobResult>(nodes[3].Result).Items);
                reopened.Select(nodes[0]);
                var row = reopened.PropertyGridVM.Groups.SelectMany(g => g.AllItems).Single(r => r.PropertyName == "FilePath");
                row.Value = Artifact("missing-" + format + ".png");
                Assert.All(nodes, n => Assert.Null(n.Result)); Assert.All(nodes.Skip(1), n => Assert.Null(n.Session.Image));
            });
            await host.Invoke(() => reopened.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                Assert.False(nodes[0].Result!.Success); Assert.Null(nodes[0].OutputImage);
                Assert.All(nodes.Skip(1), n => Assert.Null(n.Result));
                Assert.All(nodes.Skip(1), n => Assert.Null(n.Session.Image));
            });
        }
        finally
        {
            host.Invoke(() => window?.Close());
            await host.Invoke(() => vm.DisposeAsync().AsTask()); await host.Invoke(() => reopened.DisposeAsync().AsTask());
        }
    }

    private static VisionImage Frame(ImageFormat format)
    {
        var bpp = format == ImageFormat.Mono16 ? 2 : 3; var pixels = new byte[9 * 9 * bpp];
        for (var y = 0; y < 9; y++) for (var x = 0; x < 9; x++)
        {
            var foreground = x >= 2 && x <= 5 && y >= 3 && y <= 6; var index = (y * 9 + x) * bpp;
            if (format == ImageFormat.Mono16)
            { var v = foreground ? 20000 : 10000; pixels[index] = (byte)(v & 255); pixels[index + 1] = (byte)(v >> 8); }
            else for (var c = 0; c < 3; c++) pixels[index + c] = foreground ? (byte)180 : (byte)0;
        }
        return VisionImage.FromBuffer(9, 9, format, pixels);
    }
    private static void Check(VisionToolNodeBase[] nodes, ImageFormat format)
    {
        Assert.All(nodes, n => Assert.True(n.Result?.Success == true, n.Result?.Message));
        var file = Assert.IsType<ImageFileResult>(nodes[0].Result); Assert.Equal(format, file.OutputImage!.Format);
        Assert.Equal(Frame(format).Pixels, file.OutputImage.Pixels);
        for (var i = 1; i < nodes.Length; i++) Assert.Same(nodes[i - 1].OutputImage, nodes[i].Session.Image);
        Assert.Equal(16, Assert.IsType<ThresholdResult>(nodes[2].Result).WhitePixelCount);
        var blob = Assert.Single(Assert.IsType<BlobResult>(nodes[3].Result).Items);
        Assert.Equal(16, blob.Area); Assert.Equal(new Rect2D(2, 3, 4, 4), blob.Bounds);
        Assert.NotSame(file.OutputImage!.Pixels, nodes[1].OutputImage!.Pixels);
    }
    private static void WriteImage(VisionImage image, string path)
    {
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(VisionDisplay.ToBitmap(image)));
        using var stream = File.Create(path); png.Save(stream);
    }
    private static void Idle(FrameworkElement view) { view.UpdateLayout(); view.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle); }
    private static string Artifact(string name)
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "a1-acceptance"); Directory.CreateDirectory(folder); return Path.Combine(folder, name);
    }
    private static void Snapshot(FrameworkElement view, string name)
    {
        Idle(view); var image = new RenderTargetBitmap((int)view.ActualWidth, (int)view.ActualHeight, 96, 96, PixelFormats.Pbgra32); image.Render(view);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(image)); using var stream = File.Create(Artifact(name)); png.Save(stream);
    }
}
