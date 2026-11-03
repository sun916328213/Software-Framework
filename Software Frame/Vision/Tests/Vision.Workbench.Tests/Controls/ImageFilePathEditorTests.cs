using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Vision.Controls.Presentation;
using Vision.Core.Imaging;
using Vision.FlowNode;
using Vision.Tools.OpenCv.Inspection;
using Vision.Tools.OpenCv.Processing;
using System.Windows.Controls;

using Vision.Controls.Services;
using WPFCustomControls.PropertyGrid.Views;
using Vision.Tools.Halcon.Acquisition;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
[Trait("Category", "Regression")]
public sealed class ImageFilePathEditorTests(WorkbenchAppFixture host)
{
    [Fact]
    public async Task ImageFile_UsesInlinePathPicker_InOriginalNodePropertyGridAndFullEditor()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create);
        var restored = host.Invoke(VisionFlowToolboxTests.Create);
        Window window = null!;
        try
        {
            host.Invoke(() =>
            {
                var page = new VisionFlowView { DataContext = vm };
                window = new() { Content = page, Width = 1500, Height = 900, ShowInTaskbar = false, Left = -10000 };
                window.Show();
                var node = vm.Add(vm.ToolEntries.Single(t => t.Name == "HALCON/ImageFile"));
                Idle(page);
                var native = Assert.Single(VisionFlowR0Tests.Descendants(page).OfType<FilePathEditor>());
                var full = page.OpenToolEditor(node)!; Idle(full);
                var picker = Assert.Single(VisionFlowR0Tests.Descendants(full).OfType<FilePathEditor>());
                var dto = Assert.IsType<ImageFileParameters>(node.Session.Parameters);
                vm.Chart.Model.IsModified = false;
                native.PickFile = (filter, current) => { Assert.Contains("*.tiff", filter); Assert.Equal("", current); return null; };
                Click(native); Assert.Equal("", dto.FilePath); Assert.False(vm.Chart.Model.IsModified);
                native.PickFile = (_, _) => @"C:\images\样本 A.png";
                Click(native); Idle(full);
                Assert.Equal(@"C:\images\样本 A.png", dto.FilePath);
                Assert.Equal(dto.FilePath, picker.Text); Assert.True(vm.Chart.Model.IsModified);
                vm.Chart.Model.IsModified = false;
                picker.PickFile = (_, _) => @"C:\images\样本 B.tiff";
                Click(picker); Idle(page);
                Assert.Equal(@"C:\images\样本 B.tiff", dto.FilePath); Assert.Equal(dto.FilePath, native.Text);
                Assert.True(vm.Chart.Model.IsModified); Assert.Null(node.Result);
                Assert.Same(dto, ((VisionToolInstanceWorkspace)node.Session).Editor.CurrentParameters);
                vm.Chart.Model.IsModified = false; var picks = 0;
                picker.PickFile = (_, _) => { picks++; return "must-not-commit.png"; };
                vm.Chart.Model.IsRunning = true; Idle(full); Assert.False(picker.IsEnabled); Click(picker);
                Assert.Equal(0, picks); Assert.Equal(@"C:\images\样本 B.tiff", dto.FilePath);
                vm.Chart.Model.IsRunning = false;
                picker.PickFile = (_, _) => throw new InvalidOperationException("picker failure");
                Click(picker);
                Assert.Contains("picker failure", picker.SelectionError);
                Assert.Equal(@"C:\images\样本 B.tiff", dto.FilePath); Assert.False(vm.Chart.Model.IsModified);
                picker.PickFile = (_, _) => null; Click(picker); Assert.Equal("", picker.SelectionError);
                Assert.Same(native.TryFindResource("CompactTextBox"), ((TextBox)native.FindName("PathBox")).Style);
                Assert.NotNull(((Button)picker.FindName("BrowseButton")).Style);
                Assert.True(vm.Chart.SaveDocument(Artifact("image-file-path.flow")));
                Assert.True(restored.Chart.LoadDocument(Artifact("image-file-path.flow")));
                Assert.Equal(dto.FilePath, Assert.IsType<ImageFileParameters>(((VisionToolNodeBase)Assert.Single(restored.Chart.Nodes).Model).Session.Parameters).FilePath);
                vm.Select(node); Idle(page); Snapshot(page, "image-file-node.png"); Snapshot(full, "image-file-editor.png");
            });
        }
        finally
        {
            host.Invoke(() => window?.Close());
            await host.Invoke(() => vm.DisposeAsync().AsTask()); await host.Invoke(() => restored.DisposeAsync().AsTask());
        }
    }

    [Fact]
    public async Task LateNativeSelection_CannotWriteNewNode_WhileOwnedEditorStillTargetsItsOriginalNode()
    {
        var vm = host.Invoke(VisionFlowToolboxTests.Create); Window window = null!;
        try
        {
            host.Invoke(() =>
            {
                var page = new VisionFlowView { DataContext = vm };
                window = new() { Content = page, Width = 1500, Height = 900, ShowInTaskbar = false, Left = -10000 }; window.Show();
                var entry = vm.ToolEntries.Single(t => t.Name == "HALCON/ImageFile");
                var a = vm.Add(entry); var b = vm.Add(entry); vm.Select(a); Idle(page);
                var dtoA = (ImageFileParameters)a.Session.Parameters; var dtoB = (ImageFileParameters)b.Session.Parameters;
                var native = Assert.Single(VisionFlowR0Tests.Descendants(page).OfType<FilePathEditor>());
                var full = page.OpenToolEditor(a)!; Idle(full);
                var borrowed = Assert.Single(VisionFlowR0Tests.Descendants(full).OfType<FilePathEditor>());
                vm.Chart.Model.IsModified = false;
                native.PickFile = (_, _) => { vm.Select(b); Idle(page); return "late-native.png"; };
                Click(native);
                Assert.Equal("", dtoA.FilePath); Assert.Equal("", dtoB.FilePath); Assert.False(vm.Chart.Model.IsModified);
                borrowed.PickFile = (_, _) => "only-a.png"; Click(borrowed);
                Assert.Equal("only-a.png", dtoA.FilePath); Assert.Equal("", dtoB.FilePath);
                vm.Chart.Model.IsModified = false;
                borrowed.PickFile = (_, _) => { vm.Chart.Model.IsRunning = true; Idle(full); return "late-running.png"; };
                Click(borrowed);
                Assert.Equal("only-a.png", dtoA.FilePath); Assert.Equal("", dtoB.FilePath); Assert.False(vm.Chart.Model.IsModified);
                vm.Chart.Model.IsRunning = false;
            });
        }
        finally { host.Invoke(() => window?.Close()); await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task SelectedRealFile_UsesOriginalHostDecoder_ThenActualThresholdBlobChain()
    {
        // 这是“路径编辑→原宿主解码→OpenCV”组合，不冒充HALCON ImageFile原生执行。
        var vm = host.Invoke(VisionFlowToolboxTests.Create); var flow = host.Invoke(VisionFlowToolboxTests.Create);
        Window window = null!; VisionToolNodeBase threshold = null!, blob = null!;
        try
        {
            host.Invoke(() =>
            {
                var page = new VisionFlowView { DataContext = vm };
                window = new() { Content = page, Width = 1500, Height = 900, ShowInTaskbar = false, Left = -10000 }; window.Show();
                var fileNode = vm.Add(vm.ToolEntries.Single(t => t.Name == "HALCON/ImageFile")); Idle(page);
                var pixels = new byte[24 * 20];
                for (var y = 6; y < 10; y++) for (var x = 7; x < 12; x++) pixels[y * 24 + x] = 180;
                var image = VisionImage.FromBuffer(24, 20, Vision.Core.Imaging.PixelFormat.Mono8, pixels);
                var path = Artifact("实际 文件.png");
                var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(VisionDisplay.ToBitmap(image)));
                using (var stream = File.Create(path)) png.Save(stream);
                var picker = Assert.Single(VisionFlowR0Tests.Descendants(page).OfType<FilePathEditor>());
                picker.PickFile = (_, _) => path; Click(picker);
                var catalog = Vision.Controls.VisionCatalogFactory.CreateCatalog();
                var decoded = catalog.LoadImageByPath!(((ImageFileParameters)fileNode.Session.Parameters).FilePath)!;
                Assert.Equal(pixels, decoded.Pixels); Assert.Equal(path, decoded.Source);
                // ImageFile目前没有公共图像输出，不伪造它的结果/连线。按原任务输入入口送入真实后续算法。
                threshold = flow.Add(flow.ToolEntries.Single(t => t.Name == "OpenCV/Threshold"));
                blob = flow.Add(flow.ToolEntries.Single(t => t.Name == "OpenCV/Blob"));
                ((ThresholdParameters)threshold.Session.Parameters).MinGray = 128;
                ((VisionToolInstanceWorkspace)blob.Session).Editor.BlobEditor!.MinArea = 1;
                flow.Chart.Connections.Add(new()
                {
                    SourceNodeId = threshold.Id, SourcePortId = threshold.OutputPorts.Single(p => p.Name == "Next_Out").Id,
                    TargetNodeId = blob.Id, TargetPortId = blob.InputPorts.Single(p => p.Name == "Next_IN").Id
                });
                VisionImageBinding.Select(blob, threshold); flow.SetImage(decoded);
            });
            await host.Invoke(() => flow.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                Assert.Equal(20, Assert.IsType<ThresholdResult>(threshold.Result).WhitePixelCount);
                Assert.Same(threshold.OutputImage, blob.Session.Image);
                var item = Assert.Single(Assert.IsType<BlobResult>(blob.Result).Items);
                Assert.Equal(20, item.Area); Assert.Equal(new Vision.Core.Geometry.Rect2D(7, 6, 5, 4), item.Bounds);
                Assert.DoesNotContain((byte)255, blob.Session.ConfiguredImage!.Pixels);
            });
        }
        finally
        {
            host.Invoke(() => window?.Close());
            await host.Invoke(() => vm.DisposeAsync().AsTask()); await host.Invoke(() => flow.DisposeAsync().AsTask());
        }
    }

    private static string Artifact(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "image-file-acceptance"); Directory.CreateDirectory(path); return Path.Combine(path, name);
    }
    private static void Snapshot(FrameworkElement element, string name)
    {
        Idle(element); var bitmap = new RenderTargetBitmap((int)element.ActualWidth, (int)element.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(element);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = File.Create(Artifact(name)); encoder.Save(stream);
    }

    private static void Click(FilePathEditor editor) => ((Button)editor.FindName("BrowseButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static void Idle(FrameworkElement element)
    {
        element.UpdateLayout(); element.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }
}
