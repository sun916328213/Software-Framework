using System.IO;
using System.Windows;
using System.Windows.Controls;
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
using Vision.Controls.View;
using Vision.Controls.ViewModels;
using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Results;
using Vision.Core.Roi;
using Vision.FlowNode;
using Vision.Tools.OpenCv.Inspection;

using ShapeResult = Vision.Tools.Halcon.Matching.ShapeMatchResult;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
[Trait("Category", "Regression")]
public sealed class VisionFlowP3Tests(WorkbenchAppFixture host)
{
    [Fact]
    public async Task ActualPage_TemplateMatchToBlob_FollowsTranslationWithoutDrift_AndRoundTrips()
    {
        VisionFlowViewModel vm = null!, restored = null!; VisionToolNodeBase locator = null!, target = null!;
        VisionFlowView page = null!; Window window = null!;
        var template = VisionImage.FromBuffer(7, 7, PixelFormat.Mono8,
            Enumerable.Range(0, 49).Select(i => (byte)((i * 31 + i / 7 * 17) % 170 + 10)).ToArray());
        var catalog = ActualCatalog(); var baseline = Roi.Rectangle(23.5, 12.5, 5, 5);
        host.Invoke(() =>
        {
            vm = Create(catalog); restored = Create(catalog);
            locator = vm.Add(catalog.Tools[0]); target = vm.Add(catalog.Tools[1]); Connect(vm, locator, target);
            vm.SetImage(Frame(template, 8, 8));
            ((VisionToolInstanceWorkspace)locator.Session).Editor.TemplateEditor!.Capture(template);
            var workspace = (VisionToolInstanceWorkspace)target.Session;
            workspace.RoiEditor.Rois.Add(baseline); workspace.RoiEditor.Selected = baseline;
            vm.Select(target);
            page = new VisionFlowView { DataContext = vm }; window = new Window { Content = page, Width = 1500, Height = 850 }; window.Show(); window.UpdateLayout();
        });
        try
        {
            await host.Invoke(() => ((VisionToolInstanceWorkspace)locator.Session).Editor.CurrentCard!.RunCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                var selector = VisionFlowR0Tests.Descendants(page).OfType<ComboBox>().Single(c =>
                    c.DataContext is WPFCustomControls.PropertyGrid.Core.ViewModels.PropertyItemViewModel { PropertyName: "FixtureSourceNodeId" });
                selector.SelectedValue = locator.Id; Idle(page);
                Assert.Equal(locator.Id, vm.SelectedFixtureSource);
                Assert.Empty(vm.PropertyGridVM.ExtraActions);
                var menu = Assert.IsType<MenuItem>(page.FindName("CaptureFixtureMenuItem"));
                Assert.Same(vm.CaptureFixtureReferenceCommand, menu.Command);
                Assert.True(menu.Command!.CanExecute(null)); menu.Command.Execute(null);
                Assert.True(target.HasFixtureReference);
                Assert.Equal(new Pose2D(11.5, 11.5, 0), target.ReferenceFixture);
                vm.SetImage(Frame(template, 20, 15));
            });
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() => CheckTranslation(target, baseline));
            await host.Invoke(() => ((VisionToolInstanceWorkspace)target.Session).Editor.CurrentCard!.RunCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                CheckTranslation(target, baseline); vm.Select(target);
                var editor = Assert.IsType<VisionToolEditorView>(page.OpenToolEditor(target)!.Content);
                editor.Session!.SelectedImageView = ImageViewKind.Result; Idle(page);
                Assert.Equal(new Rect2D(33, 17, 5, 5), editor.ImagePanel.ImageViewport.ExecutionRoi!.Bounds);
                Snapshot(page, "template-blob-follow.png");
                var path = Path.Combine(ArtifactFolder(), "follow.flow");
                Assert.True(vm.Chart.SaveDocument(path)); Assert.True(restored.Chart.LoadDocument(path));
                var copied = (VisionToolNodeBase)restored.Chart.Nodes[1].Model;
                Assert.True(copied.HasFixtureReference); Assert.Equal(target.ReferenceFixture, copied.ReferenceFixture);
                Assert.Equal(2, restored.Chart.Connections.Count); Assert.Null(copied.Result);
            });
            await host.Invoke(() => restored.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() => CheckTranslation((VisionToolNodeBase)restored.Chart.Nodes[1].Model, baseline));
            host.Invoke(locator.Session.ClearResult);
            host.Invoke(() => Assert.Null(target.Result)); // 上游失效立即撤下下游旧结果，而不是等用户下次运行。
        }
        finally
        {
            host.Invoke(window.Close); await host.Invoke(() => vm.DisposeAsync().AsTask()); await host.Invoke(() => restored.DisposeAsync().AsTask());
        }
    }

    [Theory]
    [InlineData(90, 20, 30)]
    [InlineData(-90, 20, 10)]
    [InlineData(180, 10, 20)]
    public async Task ShapeResultContract_RotatesActualSearchRegion_WithoutInvokingHalcon(double angle, double expectedX, double expectedY)
    {
        var probe = new PoseCard { CenterX = 20, CenterY = 20 }; var catalog = ProbeCatalog(probe); VisionFlowViewModel vm = null!; VisionToolNodeBase a = null!, b = null!;
        var baseline = Roi.Rectangle(30, 20, 4, 2);
        host.Invoke(() =>
        {
            vm = Create(catalog); a = vm.Add(catalog.Tools[0]); b = vm.Add(catalog.Tools[1]); Connect(vm, a, b);
            vm.SetImage(VisionImage.FromBuffer(40, 40, PixelFormat.Mono8, Enumerable.Repeat((byte)255, 1600).ToArray()));
            var w = (VisionToolInstanceWorkspace)b.Session; w.RoiEditor.Rois.Add(baseline); w.RoiEditor.Selected = baseline;
            vm.Select(b); vm.SelectedFixtureSource = a.Id;
        });
        try
        {
            await RunCard(a); host.Invoke(() => vm.CaptureFixtureReferenceCommand.Execute(null));
            probe.Angle = angle;
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                var w = (VisionToolInstanceWorkspace)b.Session;
                Assert.Equal(new Pose2D(expectedX, expectedY, angle), w.Model.ExecutionSearchRoi!.Pose);
                Assert.Equal(8, Assert.Single(Assert.IsType<BlobResult>(b.Result).Items).Area);
                Assert.Equal(new Pose2D(30, 20, 0), baseline.Pose);
                Assert.Equal(0, probe.NativeCalls);
            });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Theory]
    [InlineData("scale")]
    [InlineData("empty")]
    [InlineData("index")]
    [InlineData("nan")]
    [InlineData("frame")]
    [InlineData("timestamp")]
    [InlineData("mutated")]
    public async Task InvalidOrStaleFixture_RejectsInsteadOfReusingOldPose(string invalid)
    {
        var probe = new PoseCard(); var catalog = ProbeCatalog(probe); VisionFlowViewModel vm = null!; VisionToolNodeBase a = null!, b = null!;
        host.Invoke(() =>
        {
            vm = Create(catalog); a = vm.Add(catalog.Tools[0]); b = vm.Add(catalog.Tools[1]); Connect(vm, a, b);
            vm.SetImage(VisionImage.FromBuffer(40, 40, PixelFormat.Mono8, Enumerable.Repeat((byte)255, 1600).ToArray()));
            var w = (VisionToolInstanceWorkspace)b.Session; var roi = Roi.Rectangle(20, 10, 4, 2); w.RoiEditor.Rois.Add(roi); w.RoiEditor.Selected = roi;
            vm.Select(b); vm.SelectedFixtureSource = a.Id;
        });
        try
        {
            await RunCard(a); host.Invoke(() => vm.CaptureFixtureReferenceCommand.Execute(null));
            await RunCard(b); host.Invoke(() => Assert.NotNull(b.Result));
            host.Invoke(() =>
            {
                if (invalid == "scale") probe.Scale = 1.2;
                if (invalid == "empty") probe.Empty = true;
                if (invalid == "nan") probe.Angle = double.NaN;
                if (invalid == "index") vm.SelectedFixtureIndex = 2;
            });
            if (invalid is "scale" or "empty" or "nan") await RunCard(a);
            host.Invoke(() =>
            {
                if (invalid == "frame") { var image = Vision.Core.Tools.VisionToolConfiguration.Snapshot(b.Session.Image)!; image.FrameId++; b.Session.SetImage(image); }
                if (invalid == "timestamp") { var image = Vision.Core.Tools.VisionToolConfiguration.Snapshot(b.Session.Image)!; image.Timestamp = image.Timestamp.AddTicks(1); b.Session.SetImage(image); }
                if (invalid == "mutated") a.Session.Image!.Pixels[0] = 0;
            });
            await RunCard(b);
            host.Invoke(() =>
            {
                Assert.Null(b.Result);
                Assert.Null(((VisionToolInstanceWorkspace)b.Session).Model.ExecutionSearchRoi);
                Assert.Contains("执行异常", ((VisionToolInstanceWorkspace)b.Session).Editor.CurrentCard!.StatusText);
            });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task ExplicitSecondMatchSelection_UsesThatCandidate_AndSelectionChangeRequiresNewReference()
    {
        var probe = new PoseCard { Second = true }; var catalog = ProbeCatalog(probe);
        VisionFlowViewModel vm = null!; VisionToolNodeBase a = null!, b = null!;
        host.Invoke(() =>
        {
            vm = Create(catalog); a = vm.Add(catalog.Tools[0]); b = vm.Add(catalog.Tools[1]); Connect(vm, a, b);
            vm.SetImage(VisionImage.FromBuffer(40, 40, PixelFormat.Mono8, Enumerable.Repeat((byte)255, 1600).ToArray()));
            var w = (VisionToolInstanceWorkspace)b.Session; var roi = Roi.Rectangle(20, 10, 4, 2); w.RoiEditor.Rois.Add(roi); w.RoiEditor.Selected = roi;
            vm.Select(b); vm.SelectedFixtureSource = a.Id; vm.SelectedFixtureIndex = 1;
        });
        try
        {
            await RunCard(a); host.Invoke(() => vm.CaptureFixtureReferenceCommand.Execute(null));
            host.Invoke(() => Assert.Equal(new Pose2D(30, 10, 0), b.ReferenceFixture));
            probe.SecondShiftY = 10; await RunCard(a); await RunCard(b);
            host.Invoke(() =>
            {
                Assert.Equal(new Pose2D(20, 20, 0), ((VisionToolInstanceWorkspace)b.Session).Model.ExecutionSearchRoi!.Pose);
                vm.SelectedFixtureIndex = 0; Assert.False(b.HasFixtureReference); Assert.Null(b.Result);
            });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task UpstreamInvalidationDuringTargetCall_RetiresOverlayAndLateResult()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var resume = new ManualResetEventSlim(); var late = new TrackedResult();
        var pose = new PoseCard(); var blocked = new BlockedSearchCard(() => { started.SetResult(); if (!resume.Wait(TimeSpan.FromSeconds(20))) throw new TimeoutException(); return late; });
        var catalog = new VisionBrowserCatalog(
            [new ToolEntry("Test/Pose", "Test", () => (pose, new object(), null)) { ProducesFixture = true },
             new ToolEntry("Test/BlockedSearch", "Test", () => (blocked, new object(), null))],
            [], _ => throw new NotSupportedException(), () => null);
        VisionFlowViewModel vm = null!; VisionToolNodeBase a = null!, b = null!; Task run = Task.CompletedTask;
        host.Invoke(() =>
        {
            vm = Create(catalog); a = vm.Add(catalog.Tools[0]); b = vm.Add(catalog.Tools[1]); Connect(vm, a, b);
            vm.SetImage(VisionImage.FromBuffer(40, 40, PixelFormat.Mono8, new byte[1600]));
            var w = (VisionToolInstanceWorkspace)b.Session; var roi = Roi.Rectangle(20, 10, 4, 2); w.RoiEditor.Rois.Add(roi); w.RoiEditor.Selected = roi;
            vm.Select(b); vm.SelectedFixtureSource = a.Id;
        });
        try
        {
            await RunCard(a); host.Invoke(() => vm.CaptureFixtureReferenceCommand.Execute(null));
            run = RunCard(b); await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            host.Invoke(() =>
            {
                Assert.NotNull(((VisionToolInstanceWorkspace)b.Session).Model.ExecutionSearchRoi);
                a.Session.ClearResult();
                Assert.Null(((VisionToolInstanceWorkspace)b.Session).Model.ExecutionSearchRoi);
            });
        }
        finally { resume.Set(); await run; await host.Invoke(() => vm.DisposeAsync().AsTask()); }
        Assert.Null(b.Result); Assert.Equal(1, late.Disposes);
    }

    private sealed class TrackedResult : ToolResult { public int Disposes { get; private set; } public override void Dispose() => Disposes++; }
    private sealed class BlockedSearchCard(Func<ToolResult> execute) : IExecutableTool, ISearchRegionTool
    {
        public string Name => "Blocked"; public long LastElapsedMs => 0; public ISearchRegionTool SearchRegionTool => this;
        public SearchRegion? SearchRegion { get; set; }
        public Vision.Core.Results.ValidationResult Validate() => new();
        public ToolResult Run(VisionImage? image) => execute();
        public Func<ToolResult>? PrepareBackgroundRun(VisionImage? image) => execute;
        public void EnsureLoaded() { } public void Dispose() { }
    }

    private Task RunCard(VisionToolNodeBase node) => host.Invoke(() => ((VisionToolInstanceWorkspace)node.Session).Editor.CurrentCard!.RunCommand.ExecuteAsync(null));
    private static void CheckTranslation(VisionToolNodeBase target, Roi baseline)
    {
        var item = Assert.Single(Assert.IsType<BlobResult>(target.Result).Items);
        Assert.Equal(9, item.Area); Assert.Equal(new Rect2D(34, 18, 3, 3), item.Bounds);
        Assert.Equal(new Pose2D(23.5, 12.5, 0), baseline.Pose);
        Assert.Equal(new Rect2D(33, 17, 5, 5), ((VisionToolInstanceWorkspace)target.Session).Model.ExecutionSearchRoi!.Bounds);
    }
    private static VisionImage Frame(VisionImage template, int x, int y)
    {
        var pixels = new byte[64 * 48];
        for (var row = 0; row < 7; row++) Array.Copy(template.Pixels, row * 7, pixels, (y + row) * 64 + x, 7);
        for (var row = 0; row < 3; row++) for (var col = 0; col < 3; col++) pixels[(y + 3 + row) * 64 + x + 14 + col] = 255;
        return VisionImage.FromBuffer(64, 48, PixelFormat.Mono8, pixels, source: "offline-fixture");
    }
    private static VisionBrowserCatalog ActualCatalog()
    {
        var template = OpenCvToolCatalog.Entries.Single(e => e.Name == "OpenCV/TemplateMatch");
        var blob = BlobEntry();
        return new([template, blob], [], _ => throw new NotSupportedException(), () => null);
    }
    private static ToolEntry BlobEntry() => new("OpenCV/Blob", "OpenCV", () =>
    {
        var tool = new BlobTool(); tool.Parameters.MinGray = 230; tool.Parameters.MinArea = 1; tool.Parameters.Features = [];
        return (new OpenCvToolCardAdapter<BlobParameters, BlobResult>(tool), tool.Parameters, null);
    });
    private static VisionBrowserCatalog ProbeCatalog(PoseCard probe) => new(
        [new ToolEntry("Test/Pose", "Test", () => (probe, new object(), null)) { ProducesFixture = true }, BlobEntry()], [],
        _ => throw new NotSupportedException(), () => null);
    private VisionFlowViewModel Create(VisionBrowserCatalog tools)
    {
        var nodes = new FlowNodeCatalog([typeof(NodeModel).Assembly, typeof(VisionToolNodeBase).Assembly]);
        var factory = new VisionNodeFactory(c => new VisionToolInstanceWorkspace(tools, tools.Tools.Single(t => t.Name == c.ToolIdentity), c.Provider), new ReflectionNodeFactory(null, nodes), nodes);
        var dialogs = new Dialogs();
        var service = new FlowChartService(new VisionTaskDocumentStore(new JsonFlowDocumentStore()), new FlowChartExecutionEngine(), dialogs,
            uiDispatcher: new FlowEditor.Controls.Presentation.Threading.WpfUiDispatcher(), documentMapper: new FlowDocumentMapper(factory, nodes));
        return new(new FlowChartViewModel(service, ContainerLocator.Container.Resolve<IExportService>(), dialogs, new ConnectionService()), tools, true);
    }
    private static void Connect(VisionFlowViewModel vm, VisionToolNodeBase a, VisionToolNodeBase b) => vm.Chart.Connections.Add(new()
    {
        SourceNodeId = a.Id, SourcePortId = a.OutputPorts.Single(p => p.Name == "Next_Out").Id,
        TargetNodeId = b.Id, TargetPortId = b.InputPorts.Single(p => p.Name == "Next_IN").Id
    });
    private static string ArtifactFolder() { var path = Path.Combine(AppContext.BaseDirectory, "p3-acceptance"); Directory.CreateDirectory(path); return path; }
    private static void Idle(FrameworkElement element) { element.UpdateLayout(); element.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle); }
    private static void Snapshot(FrameworkElement page, string name)
    {
        var bmp = new System.Windows.Media.Imaging.RenderTargetBitmap((int)page.ActualWidth, (int)page.ActualHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32); bmp.Render(page);
        var png = new System.Windows.Media.Imaging.PngBitmapEncoder(); png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bmp));
        using var stream = File.Create(Path.Combine(ArtifactFolder(), name)); png.Save(stream);
    }
    private sealed class PoseCard : IExecutableTool
    {
        public bool Second { get; set; } public double SecondShiftY { get; set; }
        public double CenterX { get; set; } = 10; public double CenterY { get; set; } = 10;
        public double Angle { get; set; } public double Scale { get; set; } = 1; public bool Empty { get; set; }
        public int NativeCalls => 0;
        public string Name => "Pose"; public long LastElapsedMs => 0;
        public Vision.Core.Results.ValidationResult Validate() => new(); public void EnsureLoaded() { } public void Dispose() { }
        public ToolResult Run(VisionImage? image)
        {
            var result = new ShapeResult { Matches = Empty ? [] : [new() { Center = new(CenterX, CenterY), AngleDeg = Angle, Scale = Scale, Score = 1 }] };
            if (Second && !Empty) result.Matches.Add(new() { Center = new(30, 10 + SecondShiftY), AngleDeg = Angle, Scale = Scale, Score = 0.9 });
            return result;
        }
    }
    private sealed class Dialogs : IDialogService
    {
        public string? ShowOpenFileDialog(string filter, string? defaultFileName = null) => null;
        public string? ShowSaveFileDialog(string filter, string defaultFileName, string defaultExt) => null;
        public bool? ShowConfirm(string message, string title) => false; public bool ConfirmYesNo(string message, string title) => false;
        public void ShowInfo(string message, string title = "提示") { } public void ShowError(string message, string title = "错误") => throw new InvalidOperationException(message);
    }
}
