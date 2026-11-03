using System.IO;
using System.Text.Json.Nodes;
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
using Vision.Controls.Services;
using Vision.Controls.ViewModels;
using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Results;
using Vision.Core.Roi;
using Vision.FlowNode;
using BlobTool = Vision.Tools.OpenCv.Inspection.BlobTool;
using BlobResult = Vision.Tools.OpenCv.Inspection.BlobResult;
using BlobParameters = Vision.Tools.OpenCv.Inspection.BlobParameters;
using CropTool = Vision.Tools.OpenCv.Processing.CropTool;
using CropParameters = Vision.Tools.OpenCv.Processing.CropParameters;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
[Trait("Category", "Regression")]
public sealed class VisionFlowP2Tests(WorkbenchAppFixture host)
{
    [Fact]
    public async Task GenericFlow_RoundTripsOriginalPixelStrideRoiIdentityAndSelection_WithoutExecuting()
    {
        VisionFlowViewModel original = null!, restored = null!;
        VisionToolNodeBase a = null!, b = null!;
        var path = TempPath();
        var pixels = Enumerable.Range(0, 64).Select(i => (byte)i).ToArray();
        var image = VisionImage.FromBuffer(3, 4, PixelFormat.Mono16, pixels, 16, "offline-padded16");
        image.FrameId = 42; image.Timestamp = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var catalog = Catalog();
        host.Invoke(() =>
        {
            original = Create(catalog); restored = Create(catalog);
            a = original.Add(catalog.Tools[1]); b = original.Add(catalog.Tools[1]);
            original.SetImage(image);
            var workspace = (VisionToolInstanceWorkspace)a.Session;
            ((BlobParameters)workspace.Parameters).MinGray = 143;
            workspace.RoiEditor.Rois.Add(Roi.Circle(1, 1, 1)); workspace.RoiEditor.Selected = workspace.RoiEditor.Rois[0];
            original.Select(b);
            Assert.True(original.Chart.SaveDocument(path));
            Assert.True(restored.Chart.LoadDocument(path));
            var nodes = restored.Chart.Nodes.Select(n => (VisionToolNodeBase)n.Model).ToArray();
            Assert.Equal(new[] { a.Id, b.Id }, nodes.Select(n => n.Id));
            Assert.Equal(143, ((BlobParameters)nodes[0].Session.Parameters).MinGray);
            Assert.Same(nodes[1], restored.Chart.SelectedNode!.Model);
            Assert.Same(nodes[1].Session.Parameters, restored.SelectedEditor!.CurrentParameters);
            Assert.Single(((VisionToolInstanceWorkspace)nodes[0].Session).RoiEditor.Rois);
            foreach (var node in nodes)
            {
                Assert.Null(node.Result); Assert.Equal(0, node.Count);
                Assert.Equal(pixels, node.Session.ConfiguredImage!.Pixels);
                Assert.Equal(16, node.Session.ConfiguredImage.Stride);
                Assert.Equal(PixelFormat.Mono16, node.Session.ConfiguredImage.Format);
                Assert.Equal(42, node.Session.ConfiguredImage.FrameId);
                Assert.Equal(image.Timestamp, node.Session.ConfiguredImage.Timestamp);
                Assert.Equal("offline-padded16", node.Session.ConfiguredImage.Source);
            }
            Assert.NotSame(nodes[0].Session.Parameters, nodes[1].Session.Parameters);
            Assert.DoesNotContain("lastResult", File.ReadAllText(path), StringComparison.OrdinalIgnoreCase);
        });
        await host.Invoke(() => original.DisposeAsync().AsTask());
        await host.Invoke(() => restored.DisposeAsync().AsTask());
    }

    [Fact]
    public async Task ExplicitCropOutputBinding_ChangesRealDownstreamPixels_PersistsAndAlsoWorksForManualExecution()
    {
        var catalog = Catalog(); var path = TempPath();
        VisionFlowViewModel vm = null!, restored = null!;
        VisionToolNodeBase crop = null!, blob = null!;
        host.Invoke(() =>
        {
            vm = Create(catalog); restored = Create(catalog);
            crop = vm.Add(catalog.Tools[0]); blob = vm.Add(catalog.Tools[1]);
            vm.SetImage(VisionImage.FromBuffer(10, 10, PixelFormat.Mono8, Enumerable.Repeat((byte)255, 100).ToArray()));
            ConnectControl(vm, crop, blob);
        });
        try
        {
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                Assert.Equal(100, Assert.Single(Assert.IsType<BlobResult>(blob.Result).Items).Area);
                vm.Select(blob); vm.SelectedImageSource = crop.Id;
                Assert.Equal(crop.Id, vm.SelectedImageSource);
            });
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                Assert.Equal(12, Assert.Single(Assert.IsType<BlobResult>(blob.Result).Items).Area);
                Assert.Equal(4, blob.Session.Image!.Width);
                Assert.Contains("4×3", ((VisionToolInstanceWorkspace)blob.Session).Editor.ImageInfoText);
                Assert.Equal(10, blob.Session.ConfiguredImage!.Width);
                Assert.True(vm.Chart.SaveDocument(path));
                Assert.True(restored.Chart.LoadDocument(path));
                Assert.Equal(2, restored.Chart.Connections.Count);
                Assert.All(restored.Chart.Nodes, n => Assert.Null(((VisionToolNodeBase)n.Model).Result));
            });
            await host.Invoke(() => restored.RunFlowCommand.ExecuteAsync(null));
            VisionToolNodeBase restoredCrop = null!, restoredBlob = null!;
            host.Invoke(() =>
            {
                restoredCrop = (VisionToolNodeBase)restored.Chart.Nodes[0].Model; restoredBlob = (VisionToolNodeBase)restored.Chart.Nodes[1].Model;
                Assert.Equal(12, Assert.Single(Assert.IsType<BlobResult>(restoredBlob.Result).Items).Area);
                CaptureBindingPage(restored, restoredCrop.Id);
            });
            var card = host.Invoke(() => ((VisionToolInstanceWorkspace)restoredBlob.Session).Editor.CurrentCard!);
            await host.Invoke(() => card.RunCommand.ExecuteAsync(null));
            host.Invoke(() => Assert.Equal(12, Assert.Single(Assert.IsType<BlobResult>(restoredBlob.Result).Items).Area));
            host.Invoke(restoredCrop.Session.ClearResult);
            await host.Invoke(() => card.RunCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                Assert.Null(restoredBlob.Result); Assert.Contains("尚无本次有效输出", card.StatusText);
                restored.Select(restoredBlob); restored.SelectedImageSource = "";
            });
            await host.Invoke(() => card.RunCommand.ExecuteAsync(null));
            host.Invoke(() => Assert.Equal(100, Assert.Single(Assert.IsType<BlobResult>(restoredBlob.Result).Items).Area));
        }
        finally
        {
            await host.Invoke(() => vm.DisposeAsync().AsTask());
            await host.Invoke(() => restored.DisposeAsync().AsTask());
        }
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("dangling")]
    [InlineData("hash")]
    [InlineData("parameters")]
    [InlineData("single-tool")]
    [InlineData("types")]
    public async Task InvalidCandidate_KeepsOldInstancesAndParameters(string damage)
    {
        var catalog = Catalog(); var path = TempPath();
        VisionFlowViewModel vm = null!; VisionToolNodeBase old = null!; Dialogs dialogs = null!;
        host.Invoke(() =>
        {
            vm = Create(catalog, out dialogs); old = vm.Add(catalog.Tools[1]);
            vm.Add(catalog.Tools[1]);
            vm.SetImage(VisionImage.FromBuffer(8, 8, PixelFormat.Mono8, new byte[64]));
            Assert.True(vm.Chart.SaveDocument(path));
            var json = JsonNode.Parse(File.ReadAllText(path))!;
            var configuration = json["nodes"]![1]!["properties"]!["Configuration"]!;
            if (damage == "unknown") configuration["toolIdentity"] = "missing/tool";
            if (damage == "hash") configuration["imageSha256"] = "broken";
            if (damage == "parameters") configuration["parameters"]!["minGray"] = "not-an-integer";
            if (damage == "types")
            {
                json["connections"]!.AsArray().Add(new JsonObject
                {
                    ["sourceNodeId"] = old.Id, ["sourcePortId"] = old.OutputPorts.Single(p => p.Name == "Next_Out").Id,
                    ["targetNodeId"] = vm.Chart.Nodes[1].Id,
                    ["targetPortId"] = vm.Chart.Nodes[1].Model.InputPorts.Single(p => p.PropertyName == nameof(VisionToolNodeBase.Image)).Id
                });
            }
            if (damage == "dangling")
            {
                json["connections"]!.AsArray().Add(new JsonObject
                {
                    ["sourceNodeId"] = old.Id, ["sourcePortId"] = "missing",
                    ["targetNodeId"] = "missing", ["targetPortId"] = "missing"
                });
            }
            File.WriteAllText(path, damage == "single-tool" ? "{\"Version\":1,\"Tool\":\"OpenCV/Blob\",\"Parameters\":{}}" : json.ToJsonString());
            Assert.False(vm.Chart.LoadDocument(path));
            Assert.Same(old, vm.Chart.Nodes[0].Model);
            Assert.False(((VisionToolInstanceWorkspace)old.Session).Model.IsClosed);
            Assert.Equal(2, vm.Chart.Nodes.Count);
            Assert.NotNull(dialogs.Error);
        });
        await host.Invoke(() => vm.DisposeAsync().AsTask());
    }

    [Fact]
    public void GenericStore_SerializationFailure_DoesNotTruncatePreviousFile()
    {
        var path = TempPath(); File.WriteAllText(path, "previous");
        Assert.ThrowsAny<Exception>(() => new JsonFlowDocumentStore().Save(path, new { Invalid = double.NaN }));
        Assert.Equal("previous", File.ReadAllText(path));
    }

    [Fact]
    public async Task LockedDestinationAndCanceledDialogs_KeepFileAndCurrentInstances()
    {
        var path = TempPath(); var catalog = Catalog(); VisionFlowViewModel vm = null!;
        host.Invoke(() =>
        {
            vm = Create(catalog);
            var node = vm.Add(catalog.Tools[1]); Assert.True(vm.Chart.SaveDocument(path));
            var bytes = File.ReadAllBytes(path);
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                Assert.False(vm.Chart.SaveDocument(path));
            Assert.Equal(bytes, File.ReadAllBytes(path));
            vm.Chart.LoadCommand.Execute(null); vm.Chart.SaveAsCommand.Execute(null);
            Assert.Same(node, vm.Chart.Nodes.Single().Model);
        });
        await host.Invoke(() => vm.DisposeAsync().AsTask());
    }

    [Fact]
    public async Task RestoringTemplateSampleAndModelReference_DoesNotLoadOrTrain()
    {
        var created = new List<TemplateCard>();
        var entry = new ToolEntry("Test/Template", "Test", () =>
        {
            var card = new TemplateCard(); created.Add(card); return (card, new TemplateParameters(), null);
        });
        var catalog = new VisionBrowserCatalog([entry], [], _ => throw new NotSupportedException(), () => null);
        VisionFlowViewModel a = null!, b = null!; var path = TempPath();
        host.Invoke(() =>
        {
            a = Create(catalog); b = Create(catalog);
            var node = a.Add(entry); var session = (VisionToolInstanceWorkspace)node.Session;
            created[0].Template.SetTemplate(VisionImage.FromBuffer(2, 2, PixelFormat.Mono8, [1, 2, 3, 4]));
            session.Editor.TemplateEditor!.RestoreReference("missing-native-model.shm");
            Assert.True(a.Chart.SaveDocument(path));
            Assert.True(b.Chart.LoadDocument(path));
            Assert.Equal(2, created.Count);
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, created[1].Template.TemplateImage!.Pixels);
            Assert.All(created, c => { Assert.Equal(0, c.Prepares); Assert.Equal(0, c.Runs); Assert.Equal(0, c.Template.NativeCalls); });
            Assert.Contains("不存在", ((VisionToolInstanceWorkspace)((VisionToolNodeBase)b.Chart.Nodes[0].Model).Session).Editor.TemplateEditor!.Status);
        });
        await host.Invoke(() => a.DisposeAsync().AsTask()); await host.Invoke(() => b.DisposeAsync().AsTask());
    }

    private sealed class TemplateParameters { public int Score { get; set; } = 80; }
    private sealed class TemplateCard : IExecutableTool
    {
        public Template Template { get; } = new();
        public Vision.Core.Presentation.ITemplateTool TemplateTool => Template;
        public string Name => "Template"; public long LastElapsedMs => 0;
        public int Prepares { get; private set; } public int Runs { get; private set; }
        public Vision.Core.Results.ValidationResult Validate() => new();
        public ToolResult Run(VisionImage? image) { Runs++; return new(); }
        public void EnsureLoaded() => Prepares++;
        public void Dispose() { }
    }
    private sealed class Template : Vision.Core.Presentation.ITemplateTool
    {
        public bool RequiresTraining => true; public bool HasModel => false; public bool NeedsTraining => true;
        public VisionImage? TemplateImage { get; private set; }
        public IReadOnlyList<RegionContour> ModelContours => [];
        public object? TrainingParameters => null; public string ModelFileFilter => "*.shm";
        public int NativeCalls { get; private set; }
        public void SetTemplate(VisionImage image) => TemplateImage = image;
        public void MarkTrainingDirty() { }
        public void Train() => NativeCalls++;
        public void SaveModel(string path) => NativeCalls++;
        public void LoadModel(string path) => NativeCalls++;
        public void ClearModel() { }
    }

    [Fact]
    public async Task GenericCopyPaste_CreatesIndependentRoiAndParameterInstances()
    {
        var catalog = Catalog(); VisionFlowViewModel vm = null!;
        host.Invoke(() =>
        {
            vm = Create(catalog); var original = vm.Add(catalog.Tools[1]);
            var source = (VisionToolInstanceWorkspace)original.Session;
            source.RoiEditor.Rois.Add(Roi.Rectangle(10, 10, 4, 4)); source.RoiEditor.Selected = source.RoiEditor.Rois[0];
            vm.Select(original); vm.Chart.CopyCommand.Execute(null); vm.Chart.PasteCommand.Execute(null);
            var copy = (VisionToolInstanceWorkspace)((VisionToolNodeBase)vm.Chart.Nodes[1].Model).Session;
            Assert.NotSame(source.Parameters, copy.Parameters);
            Assert.NotSame(source.RoiEditor.Rois[0], copy.RoiEditor.Rois[0]);
            copy.RoiEditor.Rois[0].Pose = new(20, 20, 0);
            Assert.Equal(new Pose2D(10, 10, 0), source.RoiEditor.Rois[0].Pose);
        });
        await host.Invoke(() => vm.DisposeAsync().AsTask());
    }

    [Fact]
    public async Task EmptyTask_RoundTripsInputBeforeAnyToolIsAdded()
    {
        var catalog = Catalog(); var path = TempPath(); VisionFlowViewModel a = null!, b = null!;
        host.Invoke(() =>
        {
            a = Create(catalog); b = Create(catalog);
            a.SetImage(VisionImage.FromBuffer(7, 5, PixelFormat.Mono8, Enumerable.Repeat((byte)42, 35).ToArray(), source: "empty-task"));
            Assert.True(a.Chart.SaveDocument(path)); Assert.True(b.Chart.LoadDocument(path));
            Assert.Empty(b.Chart.Nodes);
            var node = b.Add(catalog.Tools[1]);
            Assert.NotNull(node.Session.ConfiguredImage);
            Assert.Equal(7, node.Session.ConfiguredImage!.Width);
            Assert.Equal("empty-task", node.Session.ConfiguredImage.Source);
        });
        await host.Invoke(() => a.DisposeAsync().AsTask()); await host.Invoke(() => b.DisposeAsync().AsTask());
    }

    [Fact]
    public async Task LargeInput_RoundTripsTwoInstances_WithDocumentedDuplicateSnapshotCost()
    {
        var catalog = Catalog(); var path = TempPath(); VisionFlowViewModel a = null!, b = null!;
        var image = VisionImage.FromBuffer(2048, 1024, PixelFormat.Mono8, Enumerable.Repeat((byte)73, 2048 * 1024).ToArray());
        host.Invoke(() =>
        {
            a = Create(catalog); b = Create(catalog); a.Add(catalog.Tools[1]); a.Add(catalog.Tools[1]); a.SetImage(image);
            Assert.True(a.Chart.SaveDocument(path)); Assert.True(b.Chart.LoadDocument(path));
            Assert.All(b.Chart.Nodes, n => Assert.Equal(Vision.Core.Tools.VisionToolConfiguration.Hash(image),
                Vision.Core.Tools.VisionToolConfiguration.Hash(((VisionToolNodeBase)n.Model).Session.ConfiguredImage!)));
            Assert.All(b.Chart.Nodes, n => Assert.Null(((VisionToolNodeBase)n.Model).Result));
            System.Diagnostics.Trace.WriteLine($"P2 large input: pixels={image.Pixels.Length}, document={new FileInfo(path).Length}");
        });
        await host.Invoke(() => a.DisposeAsync().AsTask()); await host.Invoke(() => b.DisposeAsync().AsTask());
    }

    private static void CaptureBindingPage(VisionFlowViewModel vm, string sourceId)
    {
        var page = new Vision.Controls.View.VisionFlowView { DataContext = vm };
        var window = new System.Windows.Window { Content = page, Width = 1400, Height = 800 };
        try
        {
            vm.Chart.Model.ShowDataConnections = true;
            window.Show(); window.UpdateLayout();
            page.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
            var selector = VisionFlowR0Tests.Descendants(page).OfType<System.Windows.Controls.ComboBox>().Single(c => c.DataContext is WPFCustomControls.PropertyGrid.Core.ViewModels.PropertyItemViewModel { PropertyName: "ImageSourceNodeId" });
            Assert.Equal(sourceId, selector.SelectedValue);
            Assert.Equal(sourceId, vm.SelectedImageSource); // 加载/绑定控件不能把既有来源静默切回任务输入。
            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)page.ActualWidth, (int)page.ActualHeight,
                96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            bitmap.Render(page);
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            var folder = Path.Combine(AppContext.BaseDirectory, "p2-acceptance"); Directory.CreateDirectory(folder);
            using var stream = File.Create(Path.Combine(folder, "crop-blob-bound.png")); encoder.Save(stream);
        }
        finally { window.Close(); }
    }

    private static void ConnectControl(VisionFlowViewModel vm, VisionToolNodeBase a, VisionToolNodeBase b) => vm.Chart.Connections.Add(new()
    {
        SourceNodeId = a.Id, SourcePortId = a.OutputPorts.Single(p => p.Name == "Next_Out").Id,
        TargetNodeId = b.Id, TargetPortId = b.InputPorts.Single(p => p.Name == "Next_IN").Id
    });
    private VisionFlowViewModel Create(VisionBrowserCatalog catalog) => Create(catalog, out _);
    private VisionFlowViewModel Create(VisionBrowserCatalog tools, out Dialogs dialogs)
    {
        var nodes = new FlowNodeCatalog([typeof(NodeModel).Assembly, typeof(VisionToolNodeBase).Assembly]);
        var factory = new VisionNodeFactory(c =>
        {
            var entry = tools.Tools.SingleOrDefault(e => e.Name == c.ToolIdentity) ?? throw new InvalidDataException("未知工具");
            return new VisionToolInstanceWorkspace(tools, entry, c.Provider);
        }, new ReflectionNodeFactory(null, nodes), nodes);
        dialogs = new Dialogs();
        var service = new FlowChartService(new VisionTaskDocumentStore(new JsonFlowDocumentStore()), new FlowChartExecutionEngine(), dialogs,
            uiDispatcher: new FlowEditor.Controls.Presentation.Threading.WpfUiDispatcher(),
            documentMapper: new FlowDocumentMapper(factory, nodes));
        var chart = new FlowChartViewModel(service, ContainerLocator.Container.Resolve<IExportService>(), dialogs, new ConnectionService());
        return new(chart, tools, documentReady: true);
    }
    private static VisionBrowserCatalog Catalog()
    {
        var crop = new ToolEntry("OpenCV/Crop", "OpenCV", () =>
        {
            var tool = new CropTool(); tool.Parameters.X = 2; tool.Parameters.Y = 1; tool.Parameters.Width = 4; tool.Parameters.Height = 3;
            return (new OpenCvToolCardAdapter<CropParameters, CropResult>(tool), tool.Parameters, null);
        }) { ProducesOutputImage = true };
        var blob = new ToolEntry("OpenCV/Blob", "OpenCV", () =>
        {
            var tool = new BlobTool(); tool.Parameters.MinGray = 128; tool.Parameters.Features = [];
            return (new OpenCvToolCardAdapter<BlobParameters, BlobResult>(tool), tool.Parameters, null);
        });
        return new([crop, blob], [], _ => throw new NotSupportedException(), () => null);
    }
    private static string TempPath()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "p2-documents"); Directory.CreateDirectory(dir);
        return Path.Combine(dir, Guid.NewGuid().ToString("N") + ".flow");
    }
    private sealed class Dialogs : IDialogService
    {
        public string? Error { get; private set; }
        public string? ShowOpenFileDialog(string filter, string? defaultFileName = null) => null;
        public string? ShowSaveFileDialog(string filter, string defaultFileName, string defaultExt) => null;
        public bool? ShowConfirm(string message, string title) => false;
        public bool ConfirmYesNo(string message, string title) => false;
        public void ShowInfo(string message, string title = "提示") { }
        public void ShowError(string message, string title = "错误") => Error = message;
    }
}
