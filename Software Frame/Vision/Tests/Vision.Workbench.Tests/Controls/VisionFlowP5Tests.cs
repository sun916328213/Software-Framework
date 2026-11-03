using System.IO;
using System.Windows;
using FlowEditor.Controls.Core;
using FlowEditor.Controls.Discovery;
using FlowEditor.Controls.Interfaces;
using FlowEditor.Controls.Nodes;
using FlowEditor.Controls.Services;
using FlowEditor.Controls.Services.Storage;
using FlowEditor.Controls.ViewModels;
using FlowEditor.Runtime;
using Prism.Ioc;
using Vision.Core.Geometry;
using Vision.Core.Roi;
using Vision.FlowNode;
using Vision.Tools.AI;
using Vision.Tests.Tools.AI;
using BlobTool = Vision.Tools.OpenCv.Inspection.BlobTool;
using BlobParameters = Vision.Tools.OpenCv.Inspection.BlobParameters;
using BlobResult = Vision.Tools.OpenCv.Inspection.BlobResult;
using Vision.Controls.Models;
using Vision.Controls.Services;
using Vision.Controls.ViewModels;
using Vision.Core.Imaging;
using Vision.Core.Presentation;
using Vision.Tools.OpenCv.Matching;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
[Trait("Category", "Regression")]
public sealed class VisionFlowP5Tests(WorkbenchAppFixture host)
{
    [Theory]
    [InlineData("capture")]
    [InlineData("clear")]
    public async Task ChangedTemplate_DoesNotPersistAReferenceToThePreviousModel(string change)
    {
        VisionToolInstanceWorkspace workspace = null!;
        var path = Artifact("reference-" + change + ".vtemplate");
        host.Invoke(() => workspace = TemplateWorkspace((_, _) => path));
        try
        {
            host.Invoke(() =>
            {
                var editor = workspace.Editor.TemplateEditor!;
                Assert.True(editor.Capture(Sample())); editor.Save();
                Assert.Equal(path, editor.ModelReferencePath); Assert.True(File.Exists(path));
                if (change == "capture") Assert.True(editor.Capture(Sample(30))); else editor.Clear();
                Assert.Equal("", editor.ModelReferencePath);
                Assert.Equal("", workspace.ExportConfiguration().ModelReference);
                Assert.True(File.Exists(path)); // 撤销引用不删除用户已保存的模型。
            });
        }
        finally { await host.Invoke(() => workspace.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task ReadOnlyTask_DisablesTemplateCommands_AndDirectCaptureCannotMutateModel()
    {
        VisionToolInstanceWorkspace workspace = null!;
        host.Invoke(() => workspace = TemplateWorkspace((_, _) => Artifact("unused.vtemplate")));
        try
        {
            host.Invoke(() =>
            {
                var editor = workspace.Editor.TemplateEditor!; Assert.True(editor.Capture(Sample()));
                var original = editor.TemplateImage; var notifications = 0;
                editor.ClearCommand.CanExecuteChanged += (_, _) => notifications++;
                workspace.SetReadOnly(true);
                Assert.False(editor.ClearCommand.CanExecute(null)); Assert.False(editor.LoadCommand.CanExecute(null)); Assert.False(editor.SaveCommand.CanExecute(null));
                Assert.False(editor.Capture(Sample(30))); editor.Clear(); Assert.Same(original, editor.TemplateImage);
                Assert.True(notifications > 0);
                workspace.SetReadOnly(false); Assert.True(editor.ClearCommand.CanExecute(null)); editor.Clear(); Assert.Null(editor.TemplateImage);
            });
        }
        finally { await host.Invoke(() => workspace.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task ModalModelPicker_RechecksTaskLockBeforeLoading_AndPreservesCurrentResult()
    {
        var path = Artifact("modal-load.vtemplate");
        using (var tool = new TemplateMatchTool()) { ((ITemplateTool)tool).SetTemplate(Sample(30)); ((ITemplateTool)tool).SaveModel(path); }
        VisionToolInstanceWorkspace workspace = null!;
        host.Invoke(() => workspace = TemplateWorkspace((_, _) => { workspace.SetReadOnly(true); return path; }));
        try
        {
            host.Invoke(() =>
            {
                workspace.SetImage(Sample()); var editor = workspace.Editor.TemplateEditor!;
                Assert.True(editor.Capture(Sample())); workspace.Editor.CurrentCard!.Run(); Assert.True(workspace.Result!.Success);
                var original = editor.TemplateImage; var result = workspace.Result;
                editor.Load();
                Assert.Same(original, editor.TemplateImage); Assert.Same(result, workspace.Result); Assert.Equal("", editor.ModelReferencePath);
            });
        }
        finally { await host.Invoke(() => workspace.DisposeAsync().AsTask()); }
    }

    [Theory]
    [InlineData("dirty")]
    [InlineData("train")]
    public void ShapeTrainingContract_UnsavedChangesRetireReference_WithoutRuntime(string change)
    {
        host.Invoke(() =>
        {
            using var tool = new TemplateEditorWorkflowTests.FakeTemplateTool();
            using var editor = new TemplateEditorViewModel(tool, () => { }, null);
            editor.Capture(Sample()); editor.Train(); editor.RestoreReference(Artifact("contract-only.vshape"));
            if (change == "dirty") editor.TrainingParametersChanged(); else editor.Train();
            Assert.Equal("", editor.ModelReferencePath); Assert.True(editor.HasModel);
            Assert.Equal(change == "dirty", editor.NeedsTraining);
        });
    }

    [Theory]
    [InlineData("AI/Classification", true)]
    [InlineData("AI/Classification", false)]
    [InlineData("AI/Detection", true)]
    [InlineData("AI/Detection", false)]
    [InlineData("AI/Segmentation", true)]
    [InlineData("AI/Segmentation", false)]
    [InlineData("AI/AnomalyDetection", true)]
    [InlineData("AI/AnomalyDetection", false)]
    public async Task FourAITasks_RequireExplicitLoad_AfterIdentityChangeAndFlowReopen(string identity, bool changeDevice)
    {
        var provider = new FakeAIProvider();
        var entry = AIToolCatalog.Entries.Single(e => e.Name == identity);
        var catalog = new VisionBrowserCatalog([entry], [new("Fake", true)], _ => provider, () => null);
        VisionFlowViewModel vm = null!, restored = null!; VisionToolNodeBase node = null!; VisionToolInstanceWorkspace workspace = null!;
        host.Invoke(() =>
        {
            vm = Create(catalog); restored = Create(catalog); node = vm.Add(entry); workspace = (VisionToolInstanceWorkspace)node.Session;
            ((AIModelParameters)workspace.Parameters).ModelPath = "contract.model";
            vm.SetImage(VisionImage.FromBuffer(64, 64, PixelFormat.Mono8, new byte[4096]));
        });
        try
        {
            host.Invoke(() =>
            {
                var card = workspace.Editor.CurrentCard!;
                card.Run(); Assert.False(workspace.Result!.Success); Assert.Equal(0, provider.OpenSessionCalls);
                card.EnsureLoadedCommand.Execute(null); card.Run(); Assert.True(workspace.Result!.Success);
                Assert.NotNull(card.TableRows); Assert.Equal(1, provider.OpenSessionCalls);
                var parameters = (AIModelParameters)workspace.Parameters;
                if (changeDevice) parameters.DeviceId = "GPU-contract"; else parameters.ModelPath = "different.model";
                workspace.Editor.ParametersChanged(); Assert.Null(workspace.Result);
                card.Run(); Assert.False(workspace.Result!.Success); Assert.Equal(1, provider.OpenSessionCalls);
                card.EnsureLoadedCommand.Execute(null); card.Run(); Assert.True(workspace.Result!.Success);
                Assert.Equal(2, provider.OpenSessionCalls); Assert.Equal(1, provider.CloseSessionCalls);
                Assert.Equal(parameters.ModelPath, provider.LastRequest!.ModelPath); Assert.Equal(parameters.DeviceId, provider.LastRequest.DeviceId);
                var path = Artifact(identity.Replace('/', '-') + "-" + changeDevice + ".flow");
                Assert.True(vm.Chart.SaveDocument(path)); Assert.True(restored.Chart.LoadDocument(path));
                Assert.Equal(2, provider.OpenSessionCalls); // 恢复配置不读取模型或自动创建推理会话。
                var copied = (VisionToolInstanceWorkspace)((VisionToolNodeBase)restored.Chart.Nodes[0].Model).Session;
                Assert.Null(copied.Result); Assert.Equal(parameters.ModelPath, ((AIModelParameters)copied.Parameters).ModelPath);
                copied.Editor.CurrentCard!.Run(); Assert.False(copied.Result!.Success); Assert.Equal(2, provider.OpenSessionCalls);
                copied.Editor.CurrentCard.EnsureLoadedCommand.Execute(null); copied.Editor.CurrentCard.Run();
                Assert.True(copied.Result!.Success); Assert.Equal(3, provider.OpenSessionCalls);
            });
        }
        finally
        {
            await host.Invoke(() => vm.DisposeAsync().AsTask()); await host.Invoke(() => restored.DisposeAsync().AsTask());
        }
        Assert.Equal(3, provider.CloseSessionCalls);
    }

    [Fact]
    public async Task ActualModelFile_ToTwoMatches_ToSelectedFixtureAndBlob_RoundTripsWithoutLoadingReference()
    {
        var modelPath = Artifact("two-matches.vtemplate"); var flowPath = Artifact("two-matches.flow");
        string? pick = modelPath;
        var matchEntry = OpenCvToolCatalog.Entries.Single(e => e.Name == "OpenCV/TemplateMatch");
        var blobEntry = new ToolEntry("OpenCV/Blob", "OpenCV", () =>
        {
            var tool = new BlobTool(); tool.Parameters.MinGray = 230; tool.Parameters.MinArea = 1; tool.Parameters.Features = [];
            return (new OpenCvToolCardAdapter<BlobParameters, BlobResult>(tool), tool.Parameters, null);
        });
        var catalog = new VisionBrowserCatalog([matchEntry, blobEntry], [], _ => throw new NotSupportedException(), () => null, (_, _) => pick);
        var sample = VisionImage.FromBuffer(7, 7, PixelFormat.Mono8, Enumerable.Range(0, 49).Select(i => (byte)((i * 31 + i / 7 * 17) % 170 + 10)).ToArray());
        VisionFlowViewModel vm = null!, restored = null!; VisionToolNodeBase locator = null!, target = null!; VisionToolInstanceWorkspace matching = null!;
        host.Invoke(() =>
        {
            vm = Create(catalog); restored = Create(catalog); locator = vm.Add(matchEntry); target = vm.Add(blobEntry);
            vm.Chart.Connections.Add(new() { SourceNodeId = locator.Id, SourcePortId = locator.OutputPorts.Single(p => p.Name == "Next_Out").Id,
                TargetNodeId = target.Id, TargetPortId = target.InputPorts.Single(p => p.Name == "Next_IN").Id });
            vm.SetImage(TwoMatchFrame(sample, 0, 0)); matching = (VisionToolInstanceWorkspace)locator.Session;
            var parameters = (TemplateMatchParameters)matching.Parameters;
            parameters.Method = TemplateMatchMethod.SqDiffNormed; parameters.MinScore = 0.95; parameters.MaxMatches = 2;
            Assert.True(matching.Editor.TemplateEditor!.Capture(sample)); matching.Editor.TemplateEditor.Save(); Assert.True(File.Exists(modelPath));
            matching.Editor.TemplateEditor.Clear(); matching.Editor.CurrentCard!.Run(); Assert.False(matching.Result!.Success);
            matching.Editor.TemplateEditor.Load(); Assert.Equal(modelPath, matching.Editor.TemplateEditor.ModelReferencePath);
            Assert.False(matching.Editor.TemplateEditor.TrainCommand.CanExecute(null)); // 不把OpenCV取样叫作训练。
        });
        try
        {
            await host.Invoke(() => matching.Editor.CurrentCard!.RunCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                var result = Assert.IsType<TemplateMatchResult>(locator.Result); Assert.Equal(2, result.Matches.Count);
                Assert.Equal(new Rect2D(8, 8, 7, 7), result.Matches[0].Bounds); Assert.Equal(new Rect2D(35, 25, 7, 7), result.Matches[1].Bounds);
                Assert.True(result.Matches[1].Score > 0.99); Assert.True(result.Matches[0].Score > result.Matches[1].Score);
                matching.Editor.CurrentCard!.Results.SelectId(2); Assert.Equal(2, matching.Editor.CurrentCard.Results.SelectedId);
                var w = (VisionToolInstanceWorkspace)target.Session; var roi = Roi.Rectangle(50.5, 29.5, 5, 5); w.RoiEditor.Rois.Add(roi); w.RoiEditor.Selected = roi;
                vm.Select(target); vm.SelectedFixtureSource = locator.Id; vm.SelectedFixtureIndex = 1; vm.CaptureFixtureReferenceCommand.Execute(null);
                Assert.Equal(new Pose2D(38.5, 28.5, 0), target.ReferenceFixture); Assert.True(vm.Chart.SaveDocument(flowPath));
                File.Move(modelPath, modelPath + ".held", overwrite: true);
                try
                {
                    Assert.True(restored.Chart.LoadDocument(flowPath));
                    var copied = (VisionToolInstanceWorkspace)((VisionToolNodeBase)restored.Chart.Nodes[0].Model).Session;
                    Assert.True(copied.Editor.TemplateEditor!.HasModel); Assert.Null(copied.Result);
                    Assert.Contains("引用不存在", copied.Editor.TemplateEditor.Status); // 使用内嵌像素，不偷偷读取外部模型。
                    Assert.Equal(modelPath, copied.Editor.TemplateEditor.ModelReferencePath);
                }
                finally { File.Move(modelPath + ".held", modelPath, overwrite: true); }
                restored.SetImage(TwoMatchFrame(sample, 3, 2));
            });
            await host.Invoke(() => restored.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                var copiedTarget = (VisionToolNodeBase)restored.Chart.Nodes[1].Model;
                var item = Assert.Single(Assert.IsType<BlobResult>(copiedTarget.Result).Items);
                Assert.Equal(9, item.Area); Assert.Equal(new Rect2D(52, 30, 3, 3), item.Bounds);
                Assert.Equal(new Pose2D(50.5, 29.5, 0), ((VisionToolInstanceWorkspace)copiedTarget.Session).RoiEditor.Selected!.Pose);
                restored.Select(copiedTarget);
                var page = new VisionFlowView { DataContext = restored }; var window = new Window { Content = page, Width = 1500, Height = 850, ShowInTaskbar = false, Left = -10000 };
                try
                {
                    window.Show(); page.UpdateLayout(); page.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                    var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)page.ActualWidth, (int)page.ActualHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32); bitmap.Render(page);
                    var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                    using var stream = File.Create(Artifact("model-fixture-blob.png")); encoder.Save(stream);
                }
                finally { window.Close(); }
                var copiedSource = (VisionToolInstanceWorkspace)((VisionToolNodeBase)restored.Chart.Nodes[0].Model).Session;
                var reference = copiedSource.Editor.TemplateEditor!.ModelReferencePath; var original = copiedSource.Editor.TemplateEditor.TemplateImage;
                var corrupt = Artifact("broken.vtemplate"); File.WriteAllText(corrupt, "{\"Version\":99}"); pick = corrupt;
                copiedSource.Editor.TemplateEditor.Load(); Assert.Contains("操作失败", copiedSource.Editor.TemplateEditor.Status);
                Assert.Same(original, copiedSource.Editor.TemplateEditor.TemplateImage); Assert.Equal(reference, copiedSource.Editor.TemplateEditor.ModelReferencePath);
                Assert.Null(copiedSource.Result); Assert.Null(copiedTarget.Result); // 加载失败不能继续呈现依赖旧匹配的下游结果。
            });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); await host.Invoke(() => restored.DisposeAsync().AsTask()); }
    }

    private static VisionImage TwoMatchFrame(VisionImage sample, int dx, int dy)
    {
        var pixels = new byte[80 * 64];
        foreach (var (x, y) in new[] { (8 + dx, 8 + dy), (35 + dx, 25 + dy) })
            for (var row = 0; row < 7; row++) Array.Copy(sample.Pixels, row * 7, pixels, (y + row) * 80 + x, 7);
        pixels[(25 + dy) * 80 + 35 + dx] += 40; // 第二个独立合成候选稍低分，排序有明确依据。
        for (var row = 0; row < 3; row++) for (var col = 0; col < 3; col++) pixels[(28 + dy + row) * 80 + 49 + dx + col] = 255;
        return VisionImage.FromBuffer(80, 64, PixelFormat.Mono8, pixels);
    }
    private static VisionFlowViewModel Create(VisionBrowserCatalog catalog)
    {
        var nodes = new FlowNodeCatalog([typeof(NodeModel).Assembly, typeof(VisionToolNodeBase).Assembly]);
        var factory = new VisionNodeFactory(c => new VisionToolInstanceWorkspace(catalog, catalog.Tools.Single(t => t.Name == c.ToolIdentity), c.Provider), new ReflectionNodeFactory(null, nodes), nodes);
        var dialogs = new Dialogs();
        var service = new FlowChartService(new VisionTaskDocumentStore(new JsonFlowDocumentStore()), new FlowChartExecutionEngine(), dialogs,
            uiDispatcher: new FlowEditor.Controls.Presentation.Threading.WpfUiDispatcher(), documentMapper: new FlowDocumentMapper(factory, nodes));
        return new(new FlowChartViewModel(service, ContainerLocator.Container.Resolve<IExportService>(), dialogs, new ConnectionService()), catalog, true);
    }
    private sealed class Dialogs : IDialogService
    {
        public string? ShowOpenFileDialog(string filter, string? defaultFileName = null) => null;
        public string? ShowSaveFileDialog(string filter, string defaultFileName, string defaultExt) => null;
        public bool? ShowConfirm(string message, string title) => false; public bool ConfirmYesNo(string message, string title) => false;
        public void ShowInfo(string message, string title = "提示") { } public void ShowError(string message, string title = "错误") => throw new InvalidOperationException(message);
    }

    private static VisionToolInstanceWorkspace TemplateWorkspace(Func<string, bool, string?> pick)
    {
        var entry = OpenCvToolCatalog.Entries.Single(e => e.Name == "OpenCV/TemplateMatch");
        return new(new([entry], [], _ => throw new NotSupportedException(), () => null, pick), entry);
    }
    private static VisionImage Sample(int offset = 0) => VisionImage.FromBuffer(3, 3, PixelFormat.Mono8,
        new byte[] { 10, 70, 20, 90, 30, 110, 50, 150, 40 }.Select(v => (byte)(v + offset)).ToArray());
    private static string Artifact(string name)
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "p5-acceptance"); Directory.CreateDirectory(folder); return Path.Combine(folder, name);
    }
}
