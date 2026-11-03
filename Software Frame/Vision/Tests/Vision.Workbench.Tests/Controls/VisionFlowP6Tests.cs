using System.IO;
using FlowEditor.Controls.Core;
using FlowEditor.Controls.Discovery;
using FlowEditor.Controls.Interfaces;
using FlowEditor.Controls.Nodes;
using FlowEditor.Controls.Services;
using FlowEditor.Controls.Services.Storage;
using FlowEditor.Controls.ViewModels;
using FlowEditor.Runtime;
using Prism.Regions;
using CommunityToolkit.Mvvm.Input;
using Vision.Controls.Models;
using Vision.Controls.View;
using Vision.Controls.ViewModels;
using Vision.Core.Geometry;
using Vision.Core.Roi;
using Vision.FlowNode;
using WPFCustomControls.Workbench;
using BlobTool = Vision.Tools.OpenCv.Inspection.BlobTool;
using BlobParameters = Vision.Tools.OpenCv.Inspection.BlobParameters;
using BlobResult = Vision.Tools.OpenCv.Inspection.BlobResult;
using System.Reflection;
using Vision.Controls.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Prism.Ioc;
using Vision.Controls.Presentation;
using Vision.Core.Imaging;

using ImageFormat = Vision.Core.Imaging.PixelFormat;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
[Trait("Category", "Regression")]
public sealed class VisionFlowP6Tests(WorkbenchAppFixture host)
{
    [Fact]
    public void RealStartup_UsesIndependentVisionFlow_WithoutStandaloneDebugRoutes()
    {
        host.Invoke(() =>
        {
            Assert.Equal(typeof(VisionFlowView), host.InitialActiveViewType);
            var shell = (MainWindowViewModel)Application.Current.MainWindow.DataContext;
            Assert.Equal(new[] { nameof(VisionFlowView) }, shell.NavItems.Select(p => p.ViewName));
            var registry = (IContainerRegistry)ContainerLocator.Container;
            Assert.False(registry.IsRegistered(typeof(object), "VisionBrowserView"));
            Assert.True(registry.IsRegistered(typeof(object), nameof(VisionToolEditorView)));
            Assert.False(registry.IsRegistered(typeof(object), "VisionTaskView"));
            var vm = ContainerLocator.Container.Resolve<VisionFlowViewModel>();
            Assert.Empty(vm.Chart.Nodes); Assert.True(vm.Chart.Model.SupportsDocumentEditing);
            Assert.Equal(28, vm.ToolEntries.Count);
            Assert.Equal(35, vm.ToolboxCategories.Sum(category => category.TotalCount));
        });
    }

    [Fact]
    public void TaskPage_ExposesOriginalSaveCommand_NotSingleToolProjectCommands()
    {
        host.Invoke(() =>
        {
            var vm = ContainerLocator.Container.Resolve<VisionFlowViewModel>();
            var page = new VisionFlowView { DataContext = vm };
            var window = new Window { Content = page, Width = 1300, Height = 800, ShowInTaskbar = false, Left = -10000 };
            try
            {
                window.Show(); window.UpdateLayout();
                var save = Assert.Single(Descendants(page).OfType<Button>(), b => System.Windows.Automation.AutomationProperties.GetName(b) == "保存当前页面流程 (Ctrl+S)");
                Assert.Same(vm.Chart.SaveCommand, save.Command);
                Assert.Null(page.FindName("ToolEditor"));
                Assert.Same(vm.PropertyGridVM, ((WPFCustomControls.PropertyGrid.Views.PropertyGridHost)page.FindName("NodeProperties")).DataContext);
            }
            finally { window.Close(); }
        });
    }

    [Theory]
    [InlineData(ImageFormat.Mono8)]
    [InlineData(ImageFormat.Mono16)]
    [InlineData(ImageFormat.Bgr24)]
    [InlineData(ImageFormat.Bgra32)]
    public void ActualHostFileLoader_PreservesDepthAndChannels_WithoutGrayConversion(ImageFormat format)
    {
        host.Invoke(() =>
        {
            var bpp = format switch { ImageFormat.Mono16 => 2, ImageFormat.Bgr24 => 3, ImageFormat.Bgra32 => 4, _ => 1 };
            var pixels = Enumerable.Range(0, 6 * bpp).Select(i => (byte)(i * 11 + 3)).ToArray();
            var image = VisionImage.FromBuffer(3, 2, format, pixels);
            var path = Artifact("input-" + format + ".png");
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(VisionDisplay.ToBitmap(image)));
            using (var stream = File.Create(path)) encoder.Save(stream);
            var decoded = HostCatalog().LoadImageByPath!(path)!;
            Assert.Equal(format, decoded.Format); Assert.Equal(pixels, decoded.Pixels);
            Assert.Equal(3, decoded.Width); Assert.Equal(2, decoded.Height); Assert.Equal(path, decoded.Source);
            var panel = new VisionImagePanel(); panel.ImageViewport.Image = decoded; panel.ImageViewport.ActualPixels(); panel.ImageViewport.ZoomIn();
            var exportedPath = Artifact("export-" + format + ".png"); panel.SaveImage(exportedPath);
            var exported = HostCatalog().LoadImageByPath!(exportedPath)!;
            Assert.Equal(format, exported.Format); Assert.Equal(pixels, exported.Pixels);
        });
    }

    [Fact]
    public void UnsupportedFloatFile_IsRejectedInsteadOfSilentlyReducingBitDepth()
    {
        host.Invoke(() =>
        {
            var path = Artifact("unsupported-float.tif");
            using var mat = new OpenCvSharp.Mat(2, 2, OpenCvSharp.MatType.CV_32FC1, OpenCvSharp.Scalar.All(1.5));
            Assert.True(OpenCvSharp.Cv2.ImWrite(path, mat));
            Assert.Throws<NotSupportedException>(() => HostCatalog().LoadImageByPath!(path));
        });
    }

    [Fact]
    public async Task ActualPage_FileToModelToFixture_SaveHostCloseReopenAndRun_PreservesTask()
    {
        var flowPath = Artifact("host-roundtrip.flow"); var modelPath = Artifact("host-template.vtemplate");
        var originalImagePath = Artifact("scene-reference.png"); var movedImagePath = Artifact("scene-moved.png");
        var sample = VisionImage.FromBuffer(7, 7, ImageFormat.Mono8, Enumerable.Range(0, 49).Select(i => (byte)((i * 31 + i / 7 * 17) % 170 + 10)).ToArray());
        VisionFlowViewModel vm = null!, restored = null!; VisionToolNodeBase locator = null!, target = null!;
        (VisionFlowView page, Window window, WorkbenchLifetime lifetime, Task closed) first = (null!, null!, null!, Task.CompletedTask), second = (null!, null!, null!, Task.CompletedTask);
        var dialogs = new Dialogs(flowPath); string imagePath = originalImagePath;
        host.Invoke(() =>
        {
            WriteImage(Frame(sample, 8, 8), originalImagePath); WriteImage(Frame(sample, 20, 15), movedImagePath);
            var loader = HostCatalog().LoadImageByPath!;
            var match = OpenCvToolCatalog.Entries.Single(t => t.Name == "OpenCV/TemplateMatch");
            var blob = new ToolEntry("OpenCV/Blob", "OpenCV", () =>
            {
                var tool = new BlobTool(); tool.Parameters.MinGray = 230; tool.Parameters.MinArea = 1; tool.Parameters.Features = [];
                return (new OpenCvToolCardAdapter<BlobParameters, BlobResult>(tool), tool.Parameters, null);
            });
            var catalog = new VisionBrowserCatalog([match, blob], [], _ => throw new NotSupportedException(), () => loader(imagePath), (_, _) => modelPath);
            vm = Create(catalog, dialogs); restored = Create(catalog, dialogs); first = Open(vm);
            AddFromToolbox(first.page, match.Name); locator = (VisionToolNodeBase)vm.Chart.Nodes[0].Model;
            AddFromToolbox(first.page, blob.Name); target = (VisionToolNodeBase)vm.Chart.Nodes[1].Model;
            vm.Chart.Connections.Add(new() { SourceNodeId = locator.Id, SourcePortId = locator.OutputPorts.Single(p => p.Name == "Next_Out").Id,
                TargetNodeId = target.Id, TargetPortId = target.InputPorts.Single(p => p.Name == "Next_IN").Id });
            Click(first.page, "加载任务图像"); Assert.Equal(originalImagePath, locator.Session.Image!.Source);
            vm.Select(locator); var source = (VisionToolInstanceWorkspace)locator.Session;
            source.RoiEditor.AddRectangle(11.5, 11.5, 7, 7);
            source.Editor.SetTemplateFromRoiCommand.Execute(null); Assert.Equal(sample.Pixels, source.Editor.TemplateEditor!.TemplateImage!.Pixels);
            source.Editor.TemplateEditor.SaveCommand.Execute(null); Assert.True(File.Exists(modelPath));
            source.Editor.TemplateEditor.ClearCommand.Execute(null); Assert.False(source.Editor.TemplateEditor.HasModel);
            source.Editor.TemplateEditor.LoadCommand.Execute(null); Assert.True(source.Editor.TemplateEditor.HasModel);
        });
        try
        {
            await host.Invoke(() => ((VisionToolInstanceWorkspace)locator.Session).Editor.CurrentCard!.RunCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                vm.Select(target); var w = (VisionToolInstanceWorkspace)target.Session; w.RoiEditor.AddRectangle(23.5, 12.5, 5, 5);
                vm.SelectedFixtureSource = locator.Id; Click(first.page, "设为定位基准"); Assert.True(target.HasFixtureReference);
                imagePath = movedImagePath; Click(first.page, "加载任务图像");
            });
            await host.Invoke(() => Run(first.page));
            host.Invoke(() =>
            {
                CheckTarget(target); Click(first.page, "保存任务"); Assert.True(File.Exists(flowPath));
                Assert.Equal(1, dialogs.SaveCalls); Click(first.page, "保存任务"); Assert.Equal(1, dialogs.SaveCalls);
                Snapshot(first.page, "before-close.png");
                first.window.Close();
            });
            await host.Invoke(first.lifetime.BeginCleanup).WaitAsync(TimeSpan.FromSeconds(20));
            await first.closed.WaitAsync(TimeSpan.FromSeconds(20));
            host.Invoke(() =>
            {
                Assert.True(((VisionToolInstanceWorkspace)locator.Session).Model.IsClosed);
                Assert.True(((VisionToolInstanceWorkspace)target.Session).Model.IsClosed); Assert.Null(target.Result);
                Assert.Empty(vm.Chart.Nodes); second = Open(restored); Click(second.page, "打开任务");
                Assert.Equal(2, restored.Chart.Nodes.Count); Assert.Equal(2, restored.Chart.Connections.Count);
                Assert.All(restored.Chart.Nodes, n => Assert.Null(((VisionToolNodeBase)n.Model).Result));
                var source = (VisionToolInstanceWorkspace)((VisionToolNodeBase)restored.Chart.Nodes[0].Model).Session;
                Assert.True(source.Editor.TemplateEditor!.HasModel); Assert.Equal(modelPath, source.Editor.TemplateEditor.ModelReferencePath);
                Assert.Equal(movedImagePath, source.Image!.Source); Assert.NotSame(source, locator.Session);
            });
            await host.Invoke(() => Run(second.page));
            host.Invoke(() =>
            {
                var restoredTarget = (VisionToolNodeBase)restored.Chart.Nodes[1].Model;
                CheckTarget(restoredTarget); Assert.True(restoredTarget.HasFixtureReference); Assert.Equal(target.ReferenceFixture, restoredTarget.ReferenceFixture);
                restored.Select(restoredTarget); Snapshot(second.page, "after-reopen.png");
            });
        }
        finally
        {
            if (first.window != null) { host.Invoke(first.window.Close); await host.Invoke(first.lifetime.BeginCleanup); }
            if (second.window != null) { host.Invoke(second.window.Close); await host.Invoke(second.lifetime.BeginCleanup); }
            if (vm != null) await host.Invoke(() => vm.DisposeAsync().AsTask());
            if (restored != null) await host.Invoke(() => restored.DisposeAsync().AsTask());
        }
    }

    private static (VisionFlowView page, Window window, WorkbenchLifetime lifetime, Task closed) Open(VisionFlowViewModel vm)
    {
        var page = new VisionFlowView { DataContext = vm }; var regions = new RegionManager(); var region = new Region { Name = Guid.NewGuid().ToString("N") };
        regions.Regions.Add(region); region.Add(page); region.Activate(page);
        var window = new Window { Content = page, Width = 1500, Height = 850, ShowInTaskbar = false, Left = -10000 };
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); window.Closed += (_, _) => closed.TrySetResult();
        var lifetime = new WorkbenchLifetime(window, () => WorkbenchLifetime.CloseRegionAsync(regions, region.Name));
        window.Show(); Idle(page); return (page, window, lifetime, closed.Task);
    }
    private static void AddFromToolbox(VisionFlowView page, string name)
    {
        Idle(page);
        var vm = (VisionFlowViewModel)page.DataContext;
        var item = Assert.Single(vm.ToolboxCategories.SelectMany(c => c.Items), p => p.DisplayName == name);
        var command = ((FlowEditor.Controls.Views.FlowToolboxView)page.FindName("Toolbox")).AddNodeCommand!;
        Assert.True(command.CanExecute(item)); command.Execute(item); Idle(page);
    }
    private static void Click(VisionFlowView page, string content)
    {
        Idle(page);
        var vm = (VisionFlowViewModel)page.DataContext;
        if (content is "加载任务图像" or "设为定位基准")
        {
            Assert.Empty(vm.PropertyGridVM.ExtraActions);
            var menu = Assert.IsType<MenuItem>(page.FindName(content == "加载任务图像" ? "LoadTaskImageMenuItem" : "CaptureFixtureMenuItem"));
            var command = menu.Command!;
            Assert.True(command.CanExecute(null)); command.Execute(null); Idle(page); return;
        }
        var name = content switch { "执行流程" => "当前流程执行", "保存任务" => "保存当前页面流程 (Ctrl+S)", "打开任务" => "打开文件 (Ctrl+O)", _ => content };
        var button = Assert.Single(Descendants(page).OfType<Button>(), b => System.Windows.Automation.AutomationProperties.GetName(b) == name);
        Assert.True(button.IsEnabled); Assert.True(button.Command!.CanExecute(button.CommandParameter)); button.Command.Execute(button.CommandParameter); Idle(page);
    }
    private static Task Run(VisionFlowView page)
    {
        var button = Assert.Single(Descendants(page).OfType<Button>(), b => System.Windows.Automation.AutomationProperties.GetName(b) == "当前流程执行");
        Assert.True(button.IsEnabled); return Assert.IsAssignableFrom<IAsyncRelayCommand>(button.Command).ExecuteAsync(null);
    }
    private static void Idle(FrameworkElement page) { page.UpdateLayout(); page.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle); }
    private static void CheckTarget(VisionToolNodeBase target)
    {
        var item = Assert.Single(Assert.IsType<BlobResult>(target.Result).Items); Assert.Equal(9, item.Area); Assert.Equal(new Rect2D(34, 18, 3, 3), item.Bounds);
        var workspace = (VisionToolInstanceWorkspace)target.Session;
        Assert.Equal(new Pose2D(23.5, 12.5, 0), workspace.RoiEditor.Selected!.Pose); Assert.Equal(new Rect2D(33, 17, 5, 5), workspace.Model.ExecutionSearchRoi!.Bounds);
    }
    private static VisionImage Frame(VisionImage sample, int x, int y)
    {
        var pixels = new byte[64 * 48]; for (var row = 0; row < 7; row++) Array.Copy(sample.Pixels, row * 7, pixels, (y + row) * 64 + x, 7);
        for (var row = 0; row < 3; row++) for (var col = 0; col < 3; col++) pixels[(y + 3 + row) * 64 + x + 14 + col] = 255;
        return VisionImage.FromBuffer(64, 48, ImageFormat.Mono8, pixels);
    }
    private static void WriteImage(VisionImage image, string path)
    {
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(VisionDisplay.ToBitmap(image))); using var stream = File.Create(path); encoder.Save(stream);
    }
    private static void Snapshot(FrameworkElement page, string name)
    {
        Idle(page); var bitmap = new RenderTargetBitmap((int)page.ActualWidth, (int)page.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(page);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = File.Create(Artifact(name)); encoder.Save(stream);
    }
    private static VisionFlowViewModel Create(VisionBrowserCatalog catalog, IDialogService dialogs)
    {
        var nodes = new FlowNodeCatalog([typeof(NodeModel).Assembly, typeof(VisionToolNodeBase).Assembly]);
        var factory = new VisionNodeFactory(c => new VisionToolInstanceWorkspace(catalog, catalog.Tools.Single(t => t.Name == c.ToolIdentity), c.Provider), new ReflectionNodeFactory(null, nodes), nodes);
        var service = new FlowChartService(new VisionTaskDocumentStore(new JsonFlowDocumentStore()), new FlowChartExecutionEngine(), dialogs,
            uiDispatcher: new FlowEditor.Controls.Presentation.Threading.WpfUiDispatcher(), documentMapper: new FlowDocumentMapper(factory, nodes));
        return new(new FlowChartViewModel(service, ContainerLocator.Container.Resolve<IExportService>(), dialogs, new ConnectionService()), catalog, true);
    }
    private sealed class Dialogs(string path) : IDialogService
    {
        public int SaveCalls { get; private set; }
        public string? ShowOpenFileDialog(string filter, string? defaultFileName = null) => path;
        public string? ShowSaveFileDialog(string filter, string defaultFileName, string defaultExt) { SaveCalls++; return path; }
        public bool? ShowConfirm(string message, string title) => false; public bool ConfirmYesNo(string message, string title) => false;
        public void ShowInfo(string message, string title = "提示") { } public void ShowError(string message, string title = "错误") => throw new InvalidOperationException(message);
    }

    private static VisionBrowserCatalog HostCatalog() => Vision.Controls.VisionCatalogFactory.CreateCatalog();
    private static string Artifact(string name)
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "p6-acceptance"); Directory.CreateDirectory(folder); return Path.Combine(folder, name);
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}
