using System.Windows;
using System.Windows.Controls;
using FlowEditor.Controls.ViewModels;
using Prism.Ioc;
using Prism.Regions;
using Vision.Controls.Models;
using Vision.Controls.Services;
using Vision.Controls.View;
using Vision.Controls.ViewModels;
using Vision.Core.AI;
using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Results;
using Vision.Core.Roi;
using Vision.FlowNode;
using WPFCustomControls.Workbench;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
[Trait("Category", "Regression")]
public sealed class VisionFlowP1Tests(WorkbenchAppFixture host)
{
    [Fact]
    public void Navigation_UsesRealGenericCanvas_AndReusesPage_WithoutCreatingTools()
    {
        host.Invoke(() =>
        {
            var regions = ContainerLocator.Container.Resolve<IRegionManager>();
            void Navigate(string name)
            {
                NavigationResult? result = null;
                regions.RequestNavigate("VisionRegion", name, r => result = r);
                Assert.NotNull(result); Assert.Null(result.Error); Assert.True(result.Result);
            }
            try
            {
                Navigate(nameof(VisionFlowView));
                var page = Assert.IsType<VisionFlowView>(Assert.Single(regions.Regions["VisionRegion"].ActiveViews));
                page.UpdateLayout();
                var vm = Assert.IsType<VisionFlowViewModel>(page.DataContext);
                Assert.Same(vm.Chart, Assert.IsType<FlowEditor.Controls.Views.FlowChartView>(page.FindName("FlowCanvas")).DataContext);
                Assert.Empty(vm.Chart.Nodes);
                // P2物化适配已就绪，实际宿主现在可保存/打开和复制恢复。
                Assert.True(vm.Chart.SaveCommand.CanExecute(null));
                Assert.True(vm.Chart.CopyCommand.CanExecute(null));
                Assert.True(vm.Chart.PasteCommand.CanExecute(null));
                Assert.True(vm.Chart.LoadCommand.CanExecute(null));
                regions.Regions["VisionRegion"].Deactivate(page);
                Assert.Empty(regions.Regions["VisionRegion"].ActiveViews);
                Navigate(nameof(VisionFlowView));
                Assert.Same(page, Assert.Single(regions.Regions["VisionRegion"].ActiveViews));
                Assert.Same(vm, page.DataContext);
                Assert.NotNull(page.ToolbarAccessory);
            }
            finally { Navigate(nameof(VisionFlowView)); }
        });
    }

    [Fact]
    public async Task ManualAndFlowExecution_ShareOriginalInstancesAndFixturePreparation()
    {
        var cards = new[] { new Card(), new Card() };
        VisionFlowViewModel vm = null!;
        VisionToolNodeBase first = null!; VisionToolNodeBase second = null!;
        VisionToolInstanceWorkspace a = null!; VisionToolInstanceWorkspace b = null!;
        var baseline = Roi.Rectangle(10, 10, 4, 4);
        host.Invoke(() =>
        {
            vm = Create(cards);
            first = (VisionToolNodeBase)vm.Chart.Nodes[0].Model;
            second = (VisionToolNodeBase)vm.Chart.Nodes[1].Model;
            a = (VisionToolInstanceWorkspace)first.Session; b = (VisionToolInstanceWorkspace)second.Session;
            Assert.NotEqual(first.Id, second.Id);
            Assert.NotSame(a.Parameters, b.Parameters);
            Assert.Same(a.Parameters, a.Editor.CurrentParameters);
            a.RoiEditor.Rois.Add(baseline); a.RoiEditor.Selected = baseline;
            first.ReferenceFixture = new(0, 0, 0); first.Fixture = new(5, 3, 0);
            vm.Select(first); Assert.Same(a.Editor, vm.SelectedEditor);
            vm.Select(second); Assert.Same(b.Editor, vm.SelectedEditor);
            vm.Select(first); Assert.Same(a.Editor, vm.SelectedEditor);
            first.IsInEnabled = false;
            vm.Chart.Connections.Add(new()
            {
                SourceNodeId = first.Id, SourcePortId = first.OutputPorts.Single(p => p.Name == "Next_Out").Id,
                TargetNodeId = second.Id, TargetPortId = second.InputPorts.Single(p => p.Name == "Next_IN").Id
            });
        });
        try
        {
            await host.Invoke(() => a.Editor.CurrentCard!.RunCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                Assert.Same(a.Editor.CurrentCard!.LastResult, first.Result);
                Assert.Equal(new Rect2D(13, 11, 4, 4), cards[0].Regions.Single()!.Bounds);
            });
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() =>
            {
                Assert.Equal(2, cards[0].Runs); Assert.Equal(1, cards[1].Runs);
                Assert.Same(first.Result, a.Editor.CurrentCard!.LastResult);
                Assert.Same(second.Result, b.Editor.CurrentCard!.LastResult);
                Assert.All(cards[0].Regions, r => Assert.Equal(new Rect2D(13, 11, 4, 4), r!.Bounds));
                Assert.Equal(new Pose2D(10, 10, 0), baseline.Pose);
                Assert.True(first.RunSequence < second.RunSequence);
                Assert.False(a.Editor.CurrentCard.Model.IsReadOnly);
                Assert.Same(a.Parameters, a.Editor.CurrentParameters);
            });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
        Assert.All(cards, c => Assert.Equal(1, c.Disposes));
        Assert.All(cards.SelectMany(c => c.Results), r => Assert.Equal(1, r.Disposes));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RealClosing_WaitsBothExecutionEntrypoints_AndContinuesAfterCachedPageFailure(bool wholeFlow)
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var resume = new ManualResetEventSlim();
        var result = new Result();
        var active = new Card { Background = () => { started.SetResult(); if (!resume.Wait(TimeSpan.FromSeconds(25))) throw new TimeoutException(); return result; } };
        var idle = new Card();
        var faulty = new Card { DisposeError = new InvalidOperationException("P1缓存页释放失败") };
        var provider = new Provider();
        VisionFlowViewModel vm = null!; VisionFlowViewModel cached = null!;
        VisionToolInstanceWorkspace idleWorkspace = null!;
        VisionFlowView page = null!; Window window = null!; WorkbenchLifetime lifetime = null!; Task execution = null!;
        host.Invoke(() =>
        {
            vm = Create([active, idle]); cached = Create([faulty], provider);
            var firstNode = (VisionToolNodeBase)vm.Chart.Nodes[0].Model;
            var nextNode = (VisionToolNodeBase)vm.Chart.Nodes[1].Model;
            firstNode.IsInEnabled = false;
            vm.Chart.Connections.Add(new()
            {
                SourceNodeId = firstNode.Id, SourcePortId = firstNode.OutputPorts.Single(p => p.Name == "Next_Out").Id,
                TargetNodeId = nextNode.Id, TargetPortId = nextNode.InputPorts.Single(p => p.Name == "Next_IN").Id
            });
            idleWorkspace = (VisionToolInstanceWorkspace)((VisionToolNodeBase)vm.Chart.Nodes[1].Model).Session;
            var regions = new RegionManager(); var region = new Region { Name = "P1Local" }; regions.Regions.Add(region);
            region.Add(new VisionFlowView { DataContext = cached });
            page = new VisionFlowView { DataContext = vm }; region.Add(page); region.Activate(page);
            window = new Window { Content = page, Width = 1400, Height = 800 };
            window.Closed += (_, _) => closed.TrySetResult();
            lifetime = new(window, () => WorkbenchLifetime.CloseRegionAsync(regions, region.Name));
            window.Show(); window.UpdateLayout();
            execution = wholeFlow ? vm.RunFlowCommand.ExecuteAsync(null)
                : ((VisionToolInstanceWorkspace)((VisionToolNodeBase)vm.Chart.Nodes[0].Model).Session).Editor.CurrentCard!.RunCommand.ExecuteAsync(null);
        });
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            host.Invoke(() =>
            {
                vm.Chart.SelectedNode = vm.Chart.Nodes[1]; page.UpdateLayout();
                var editor = Assert.IsType<VisionToolEditorView>(page.OpenToolEditor((VisionToolNodeBase)vm.Chart.SelectedNode!.Model)!.Content);
                Assert.Same(idleWorkspace.Editor, editor.Session);
                Assert.False(editor.Session!.IsEditingEnabled);
                Assert.True(editor.ImagePanel.IsEnabled);
                Assert.True(editor.ImagePanel.ImageViewport.IsEnabled);
                Assert.False(editor.Session.CurrentCard!.RunCommand.CanExecute(null));
                editor.ImagePanel.ImageViewport.ContextMenu!.Items.OfType<MenuItem>().Single(i => Equals(i.Header, "平移")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                Assert.Equal("平移", editor.ImagePanel.ImageViewport.Mode);
                SaveBusySnapshot(page, wholeFlow);
                window.Close();
                Assert.True(window.IsVisible);
                Assert.False(lifetime.BeginCleanup().IsCompleted);
                Assert.Equal(1, faulty.Disposes); Assert.Equal(1, provider.Disposes);
                Assert.Equal(0, active.Disposes); Assert.Equal(0, result.Disposes);
            });
        }
        finally
        {
            resume.Set();
            await execution.WaitAsync(TimeSpan.FromSeconds(20));
            host.Invoke(window.Close); // 前置断言失败也正常关闭测试窗口，不能遗留窗口掩盖原错误。
            await host.Invoke(lifetime.BeginCleanup).WaitAsync(TimeSpan.FromSeconds(20));
            await closed.Task.WaitAsync(TimeSpan.FromSeconds(20));
        }
        Assert.Equal(1, active.Disposes); Assert.Equal(1, idle.Disposes); Assert.Equal(1, result.Disposes);
        var error = await Record.ExceptionAsync(() => host.Invoke(() => cached.DisposeAsync().AsTask()));
        Assert.NotNull(error); Assert.Contains("P1缓存页释放失败", error.ToString());
    }

    [Fact]
    public void NodeAssembly_DoesNotReferenceVisionControlsOrVendorTools()
    {
        var names = typeof(VisionToolNodeBase).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
        Assert.DoesNotContain("Vision.Controls", names);
        Assert.DoesNotContain(names, n => n!.StartsWith("Vision.Tools.") || n.StartsWith("Vision.AI."));
        Assert.Contains("FlowEditor.Controls", names); // 明确当前WPF基类依赖，不宣称纯Core。
    }

    [Fact]
    public async Task FlowFailure_StopsDownstream_AndDefaultGenericDocumentCommandsRemainEnabled()
    {
        var cards = new[] { new Card { Success = false }, new Card() };
        VisionFlowViewModel vm = null!;
        host.Invoke(() =>
        {
            var generic = ContainerLocator.Container.Resolve<FlowChartViewModel>();
            Assert.True(generic.Model.SupportsDocumentEditing);
            Assert.True(generic.SaveCommand.CanExecute(null));
            Assert.True(generic.CopyCommand.CanExecute(null));
            vm = Create(cards);
            var first = vm.Chart.Nodes[0].Model; var second = vm.Chart.Nodes[1].Model;
            Assert.True(first.IsInEnabled);
            vm.Chart.Connections.Add(new()
            {
                SourceNodeId = first.Id, SourcePortId = first.OutputPorts.Single(p => p.Name == "Next_Out").Id,
                TargetNodeId = second.Id, TargetPortId = second.InputPorts.Single(p => p.Name == "Next_IN").Id
            });
        });
        try
        {
            await host.Invoke(() => vm.RunFlowCommand.ExecuteAsync(null));
            host.Invoke(() => { Assert.Equal(1, cards[0].Runs); Assert.Equal(0, cards[1].Runs); Assert.Equal("执行出错", vm.StatusText); });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task CanceledBeforeNodeExecution_DiscardsPreviousResultAndOutput()
    {
        var card = new Card();
        VisionFlowViewModel vm = null!; VisionToolNodeBase node = null!;
        host.Invoke(() => { vm = Create([card]); node = (VisionToolNodeBase)vm.Chart.Nodes[0].Model; });
        try
        {
            await host.Invoke(() => node.ExecuteAsync(CancellationToken.None));
            using var stop = new CancellationTokenSource(); stop.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => host.Invoke(() => node.ExecuteAsync(stop.Token)));
            host.Invoke(() =>
            {
                Assert.Null(node.Result);
                Assert.Equal(1, card.Results.Single().Disposes);
                Assert.Equal(1, card.Runs);
                Assert.All(node.OutputPorts.Where(p => p.PropertyName == nameof(VisionToolNodeBase.Result)), p => Assert.Null(p.Value));
            });
        }
        finally { await host.Invoke(() => vm.DisposeAsync().AsTask()); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RemovedIdleNode_RetiresImmediately_AndKeepsCleanupErrorObservable(bool throws)
    {
        var card = new Card { DisposeError = throws ? new InvalidOperationException("退役清理失败") : null };
        VisionFlowViewModel vm = null!;
        host.Invoke(() => vm = Create([card]));
        Exception? closeError = null;
        try
        {
            host.Invoke(() =>
            {
                vm.Chart.Nodes.RemoveAt(0);
                Assert.Equal(1, card.Disposes);
                Assert.Null(vm.SelectedEditor);
                if (throws) Assert.Contains("退役清理失败", vm.StatusText);
            });
        }
        finally { closeError = await Record.ExceptionAsync(() => host.Invoke(() => vm.DisposeAsync().AsTask())); }
        Assert.Equal(1, card.Disposes);
        if (throws) { Assert.NotNull(closeError); Assert.Contains("退役清理失败", closeError.ToString()); }
        else Assert.Null(closeError);
    }

    [Fact]
    public async Task RemovedRunningNode_WaitsActualCall_AndNeverPublishesLateResult()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var resume = new ManualResetEventSlim();
        var result = new Result();
        var card = new Card { Background = () => { started.SetResult(); if (!resume.Wait(TimeSpan.FromSeconds(20))) throw new TimeoutException(); return result; } };
        VisionFlowViewModel vm = null!; Task run = null!;
        host.Invoke(() =>
        {
            vm = Create([card]);
            run = ((VisionToolInstanceWorkspace)((VisionToolNodeBase)vm.Chart.Nodes[0].Model).Session).Editor.CurrentCard!.RunCommand.ExecuteAsync(null);
        });
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            host.Invoke(() =>
            {
                vm.Chart.Nodes.Clear();
                Assert.Equal(0, card.Disposes);
                Assert.False(vm.IsEditingEnabled);
                Assert.Null(vm.SelectedEditor);
            });
        }
        finally { resume.Set(); await run; await host.Invoke(() => vm.DisposeAsync().AsTask()); }
        Assert.Equal(1, card.Disposes); Assert.Equal(1, result.Disposes);
    }

    private static void SaveBusySnapshot(FrameworkElement page, bool wholeFlow)
    {
        // 96-DPI合成输入的离线布局记录，不代表真实鼠标、显示器DPI或竞品软件验收。
        page.UpdateLayout();
        // 通用画布在Loaded优先级合并绘制连线；UpdateLayout本身不会排空该队列。
        page.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
        var flow = Assert.IsType<FlowEditor.Controls.Views.FlowChartView>(((VisionFlowView)page).FindName("FlowCanvas"));
        Assert.NotEmpty(Assert.IsType<Canvas>(flow.FindName("ConnectionCanvas")).Children.Cast<UIElement>());
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(page.ActualWidth),
            (int)Math.Ceiling(page.ActualHeight), 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(page);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        var folder = System.IO.Path.Combine(AppContext.BaseDirectory, "p1-acceptance");
        System.IO.Directory.CreateDirectory(folder);
        using var stream = System.IO.File.Create(System.IO.Path.Combine(folder, $"busy-flow-{wholeFlow}.png"));
        encoder.Save(stream);
    }

    private VisionFlowViewModel Create(Card[] cards, Provider? provider = null)
    {
        var index = 0;
        var entry = new ToolEntry("受控工具", "Test", () => (cards[index++], new Parameters(), provider == null ? null : _ => { }));
        var catalog = new VisionBrowserCatalog([entry], provider == null ? [] : [new ProviderEntry("fake", true)],
            _ => provider!, () => null);
        var vm = new VisionFlowViewModel(ContainerLocator.Container.Resolve<FlowChartViewModel>(), catalog);
        foreach (var _ in cards) vm.Add(entry);
        vm.SetImage(VisionImage.FromBuffer(40, 40, PixelFormat.Mono8, Enumerable.Repeat((byte)255, 1600).ToArray()));
        return vm;
    }
    private sealed class Parameters { public int Threshold { get; set; } = 128; }
    private sealed class Card : IExecutableTool, ISearchRegionTool
    {
        public string Name => "受控工具"; public long LastElapsedMs => 0;
        public ISearchRegionTool SearchRegionTool => this;
        public SearchRegion? SearchRegion { get; set; }
        public List<SearchRegion?> Regions { get; } = [];
        public List<Result> Results { get; } = [];
        public int Runs { get; private set; }
        public int Disposes { get; private set; }
        public Func<ToolResult>? Background { get; init; }
        public bool Success { get; init; } = true;
        public Exception? DisposeError { get; init; }
        public Vision.Core.Results.ValidationResult Validate() => new();
        public ToolResult Run(VisionImage? image) { Runs++; Regions.Add(SearchRegion); var r = new Result { Success = Success }; Results.Add(r); return r; }
        public Func<ToolResult>? PrepareBackgroundRun(VisionImage? image) => Background;
        public void EnsureLoaded() { }
        public void Dispose() { Disposes++; if (DisposeError != null) throw DisposeError; }
    }
    private sealed class Result : ToolResult { public int Disposes { get; private set; } public override void Dispose() => Disposes++; }
    private sealed class Provider : IAIProvider, IDisposable
    {
        public string Name => "fake"; public AICapabilities Capabilities => default; public bool IsAvailable => true; public int Disposes { get; private set; }
        public IAIModelSession OpenSession(AIModelRequest request) => throw new NotSupportedException();
        public void CloseSession(IAIModelSession session) => throw new NotSupportedException();
        public void Dispose() => Disposes++;
    }
}
