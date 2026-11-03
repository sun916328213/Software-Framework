using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Vision.Controls.Contracts;
using Vision.Controls.Models;
using Vision.Controls.Services;
using Vision.Controls.View;
using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Roi;
using Vision.FlowNode;
using Vision.Tools.OpenCv.Inspection;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
public sealed class NodeToolConfigurationTests(WorkbenchAppFixture host)
{
    [Fact]
    public async Task OwnedNodeEditor_UsesRealFiles_WithoutReplacingNodeSessionParametersOrConnections()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".visionproj");
        var copy = path + ".copy.visionproj";
        var picks = new Queue<string?>([path, copy, path]); var creates = 0;
        var vm = host.Invoke(() => VisionFlowToolboxTests.Create(() => creates++, c => c with
        {
            PickProjectPath = _ => picks.Dequeue(), LoadImage = Image
        }));
        Window owner = null!; VisionToolEditorView editor = null!; VisionToolNodeBase node = null!;
        VisionToolInstanceWorkspace workspace = null!; object parameters = null!; string id = "";
        try
        {
            host.Invoke(() =>
            {
                var page = new VisionFlowView { DataContext = vm };
                owner = new() { Content = page, Width = 1400, Height = 850, ShowInTaskbar = false, Left = -10000 };
                owner.Show();
                node = vm.Add(vm.ToolEntries.Single(t => t.Name == "OpenCV/Blob"));
                var sink = vm.Add(vm.ToolEntries.Single(t => t.Name == "OpenCV/Blob"));
                vm.Chart.Connections.Add(new() { SourceNodeId = node.Id,
                    SourcePortId = node.OutputPorts.Single(p => p.Name == "Next_Out").Id,
                    TargetNodeId = sink.Id, TargetPortId = sink.InputPorts.Single(p => p.Name == "Next_IN").Id });
                workspace = Assert.IsType<VisionToolInstanceWorkspace>(node.Session);
                parameters = workspace.Parameters; id = node.Id;
                editor = Assert.IsType<VisionToolEditorView>(page.OpenToolEditor(node)!.Content);
                var commands = Assert.IsAssignableFrom<IVisionProjectCommands>(editor.Session!.ProjectCommands);
                owner.UpdateLayout();
                var buttons = Descendants((Grid)editor.FindName("TopMenuBar")).OfType<Button>().ToArray();
                Assert.Equal(5, buttons.Length); Assert.All(buttons, b => Assert.True(b.IsVisible));
                Assert.Same(commands.ImportProjectCommand, buttons[0].Command);
                Assert.Same(commands.SaveProjectCommand, buttons[1].Command);
                Assert.Same(commands.SaveProjectAsCommand, buttons[2].Command);
                Assert.Same(commands.LoadImageCommand, buttons[3].Command);
                Assert.Same(editor.Session.CurrentCard!.RunCommand, buttons[4].Command);
                commands.LoadImageCommand.Execute(null);
                ((BlobParameters)parameters).MinGray = 74;
                workspace.RoiEditor.Rois.Add(Roi.Rectangle(4, 4, 8, 8));
                workspace.RoiEditor.Selected = workspace.RoiEditor.Rois[0];
                node.ReferenceFixture = new Pose2D(10, 20, 0);
                commands.SaveProjectCommand.Execute(null);
                Assert.True(File.Exists(path)); Assert.Equal(path, commands.CurrentProjectPath);
                ((BlobParameters)parameters).MinGray = 80; commands.SaveProjectCommand.Execute(null);
                ((BlobParameters)parameters).MinGray = 99; commands.SaveProjectAsCommand.Execute(null);
                Assert.True(File.Exists(copy)); Assert.Equal(copy, commands.CurrentProjectPath);
            });
            await host.Invoke(() => editor.Session!.CurrentCard!.RunCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                Assert.NotNull(workspace.Result);
                ((BlobParameters)parameters).MinGray = 21;
                var commands = editor.Session!.ProjectCommands!;
                commands.ImportProjectCommand.Execute(null);
                Assert.Equal(80, ((BlobParameters)parameters).MinGray);
                Assert.Same(parameters, workspace.Parameters); Assert.Same(workspace, node.Session);
                Assert.Same(workspace.Editor, editor.Session); Assert.Equal(id, node.Id);
                Assert.Single(vm.Chart.Connections); Assert.Equal(2, creates); Assert.Empty(picks);
                Assert.Null(workspace.Result); Assert.False(node.HasFixtureReference);
                Assert.Single(workspace.RoiEditor.Rois); Assert.NotNull(workspace.ConfiguredImage);
                Assert.Equal(path, commands.CurrentProjectPath); Assert.True(vm.Chart.Model.IsModified);
                var document = new VisionProjectFileStore().ReadProject(path);
                Assert.DoesNotContain("LastResult", document.Json); Assert.DoesNotContain("ElapsedMs", document.Json);
                owner.OwnedWindows.Cast<Window>().Single().Close(); Assert.True(workspace.Editor.IsSessionActive);
            });
        }
        finally
        {
            host.Invoke(() => owner?.Close()); await host.Invoke(() => vm.DisposeAsync().AsTask());
            File.Delete(path); File.Delete(copy);
        }
    }

    [Fact]
    public async Task CancelInvalidAndWrongToolFiles_LeaveCurrentConfigurationUntouched_AndReadOnlyBlocksCommands()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".visionproj"); string? selected = null;
        var catalog = new VisionBrowserCatalog(OpenCvToolCatalog.Entries, [], _ => throw new NotSupportedException(),
            Image, PickProjectPath: _ => selected);
        var workspace = host.Invoke(() => new VisionToolInstanceWorkspace(catalog, catalog.Tools.Single(t => t.Name == "OpenCV/Blob")));
        try
        {
            host.Invoke(() =>
            {
                var commands = Assert.IsAssignableFrom<IVisionProjectCommands>(workspace.Editor.ProjectCommands);
                var parameters = Assert.IsType<BlobParameters>(workspace.Parameters); parameters.MinGray = 77;
                workspace.SetImage(Image()); var input = workspace.ConfiguredImage;
                commands.ImportProjectCommand.Execute(null); commands.SaveProjectAsCommand.Execute(null);
                Assert.Null(commands.CurrentProjectPath); Assert.Equal(77, parameters.MinGray);
                selected = path; File.WriteAllText(path, "not JSON"); commands.ImportProjectCommand.Execute(null);
                Assert.Equal(77, parameters.MinGray); Assert.Same(input, workspace.ConfiguredImage);
                File.WriteAllText(path, "{\"Tool\":\"OpenCV/Threshold\",\"Parameters\":{\"MinGray\":1}}");
                commands.ImportProjectCommand.Execute(null); Assert.Equal(77, parameters.MinGray);
                File.WriteAllText(path, "{\"Tool\":\"OpenCV/Blob\",\"Parameters\":{\"MinGray\":\"bad\"}}");
                commands.ImportProjectCommand.Execute(null); Assert.Equal(77, parameters.MinGray);
                Assert.Null(commands.CurrentProjectPath); Assert.Same(input, workspace.ConfiguredImage);
                workspace.SetReadOnly(true);
                foreach (var command in new[] { commands.ImportProjectCommand, commands.SaveProjectCommand, commands.SaveProjectAsCommand, commands.LoadImageCommand })
                { Assert.False(command.CanExecute(null)); command.Execute(null); }
                Assert.Equal(77, parameters.MinGray); Assert.Same(input, workspace.ConfiguredImage);
                workspace.SetReadOnly(false); Assert.True(commands.LoadImageCommand.CanExecute(null));
            });
        }
        finally { await host.Invoke(() => workspace.DisposeAsync().AsTask()); File.Delete(path); }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DialogReturningAfterLockOrConfigurationEdit_DoesNotApplyItsImage(bool edit)
    {
        VisionToolInstanceWorkspace workspace = null!;
        var catalog = new VisionBrowserCatalog(OpenCvToolCatalog.Entries, [], _ => throw new NotSupportedException(),
            () => { if (edit) { ((BlobParameters)workspace.Parameters).MinGray = 91; workspace.RefreshParameterEditors(); }
                else workspace.SetReadOnly(true); return Image(); });
        workspace = host.Invoke(() => new VisionToolInstanceWorkspace(catalog, catalog.Tools.Single(t => t.Name == "OpenCV/Blob")));
        try
        {
            host.Invoke(() =>
            {
                workspace.SetImage(Image()); var original = workspace.ConfiguredImage;
                workspace.Editor.ProjectCommands!.LoadImageCommand.Execute(null);
                Assert.Same(original, workspace.ConfiguredImage);
                Assert.Contains("未应用", workspace.Model.ImageInfoText);
                if (edit) Assert.Equal(91, ((BlobParameters)workspace.Parameters).MinGray);
            });
        }
        finally { await host.Invoke(() => workspace.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task BackgroundExecution_DisablesFileCommands_AndNotifiesWhenExecutionReallyCompletes()
    {
        using var probe = new BlockingProbe();
        var catalog = new VisionBrowserCatalog([new("Test/Blocking", "Test", () => (probe, new object(), null))], [],
            _ => throw new NotSupportedException(), Image, PickProjectPath: _ => null);
        var workspace = host.Invoke(() => new VisionToolInstanceWorkspace(catalog, catalog.Tools[0]));
        Task run = Task.CompletedTask; var notifiedEnabled = false;
        try
        {
            run = host.Invoke(() =>
            {
                workspace.SetImage(Image());
                workspace.Editor.ProjectCommands!.SaveProjectCommand.CanExecuteChanged += (_, _) =>
                    notifiedEnabled = workspace.Editor.ProjectCommands!.SaveProjectCommand.CanExecute(null);
                return workspace.RunAsync(CancellationToken.None);
            });
            Assert.True(probe.Started.Wait(TimeSpan.FromSeconds(10)));
            host.Invoke(() =>
            {
                var commands = workspace.Editor.ProjectCommands!;
                foreach (var command in new[] { commands.ImportProjectCommand, commands.SaveProjectCommand, commands.SaveProjectAsCommand, commands.LoadImageCommand })
                { Assert.False(command.CanExecute(null)); command.Execute(null); }
                Assert.Null(commands.CurrentProjectPath); Assert.False(notifiedEnabled);
            });
            probe.Release.Set(); await run;
            host.Invoke(() => { Assert.True(notifiedEnabled); Assert.NotNull(workspace.Result); });
        }
        finally { probe.Release.Set(); await run; await host.Invoke(() => workspace.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task TemplateConfiguration_RoundTripsPixelsAndReference_WithoutLoadingModelsOrReplacingDto()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".visionproj");
        var catalog = new VisionBrowserCatalog(OpenCvToolCatalog.Entries, [], _ => throw new NotSupportedException(),
            Image, PickProjectPath: _ => path);
        var workspace = host.Invoke(() => new VisionToolInstanceWorkspace(catalog, catalog.Tools.Single(t => t.Name == "OpenCV/TemplateMatch")));
        try
        {
            host.Invoke(() =>
            {
                var parameters = workspace.Parameters;
                var template = workspace.Editor.TemplateEditor!;
                template.Capture(Image()); template.RestoreReference(path + ".missing-model");
                workspace.Editor.ProjectCommands!.SaveProjectCommand.Execute(null); Assert.True(File.Exists(path));
                template.ClearCommand.Execute(null); Assert.Null(template.TemplateImage);
                workspace.Editor.ProjectCommands.ImportProjectCommand.Execute(null);
                Assert.Same(parameters, workspace.Parameters); Assert.Equal(Image().Pixels, template.TemplateImage!.Pixels);
                Assert.Equal(path + ".missing-model", template.ModelReferencePath);
                Assert.Contains("不存在", template.Status); Assert.True(template.HasModel); Assert.Null(workspace.Result);
            });
        }
        finally { await host.Invoke(() => workspace.DisposeAsync().AsTask()); File.Delete(path); }
    }

    [Fact]
    public async Task FailedSaveAs_DoesNotAdoptFailurePathOrOverwritePreviousFile()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".visionproj"); var selected = path;
        var catalog = new VisionBrowserCatalog(OpenCvToolCatalog.Entries, [], _ => throw new NotSupportedException(),
            Image, PickProjectPath: _ => selected);
        var workspace = host.Invoke(() => new VisionToolInstanceWorkspace(catalog, catalog.Tools.Single(t => t.Name == "OpenCV/Blob")));
        try
        {
            host.Invoke(() =>
            {
                var commands = workspace.Editor.ProjectCommands!;
                commands.SaveProjectCommand.Execute(null); Assert.True(File.Exists(path)); var original = File.ReadAllBytes(path);
                selected = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "missing.visionproj");
                commands.SaveProjectAsCommand.Execute(null);
                Assert.Equal(path, commands.CurrentProjectPath); Assert.Equal(original, File.ReadAllBytes(path));
                Assert.Contains("失败", workspace.Model.ImageInfoText);
            });
        }
        finally { await host.Invoke(() => workspace.DisposeAsync().AsTask()); File.Delete(path); }
    }
    private sealed class BlockingProbe : Vision.Controls.ViewModels.IExecutableTool
    {
        public ManualResetEventSlim Started { get; } = new();
        public ManualResetEventSlim Release { get; } = new();
        public string Name => "Blocking";
        public long LastElapsedMs => 0;
        public Vision.Core.Results.ValidationResult Validate() => new();
        public Vision.Core.Results.ToolResult Run(VisionImage? image) => new BlobResult();
        public Func<Vision.Core.Results.ToolResult>? PrepareBackgroundRun(VisionImage? image) => () =>
        {
            Started.Set();
            if (!Release.Wait(TimeSpan.FromSeconds(20))) throw new TimeoutException("Test did not release execution.");
            return new BlobResult();
        };
        public void EnsureLoaded() { }
        public void Dispose() { }
    }
    private static VisionImage Image() => VisionImage.FromBuffer(16, 16, Vision.Core.Imaging.PixelFormat.Mono8, Enumerable.Repeat((byte)100, 256).ToArray());
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        { var child = VisualTreeHelper.GetChild(root, i); yield return child; foreach (var nested in Descendants(child)) yield return nested; }
    }
}
