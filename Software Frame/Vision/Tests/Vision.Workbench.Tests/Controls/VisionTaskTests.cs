using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using FlowEditor.Controls.Presentation.Threading;
using Prism.Ioc;
using Prism.Regions;
using Vision.Controls.Models;
using Vision.Controls.Services;
using Vision.Controls.View;
using Vision.Controls.ViewModels;
using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Results;
using Vision.Core.Roi;
using Cv = Vision.Tools.OpenCv.Inspection;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
[Trait("Category", "Contract")]
public sealed class VisionTaskTests(WorkbenchAppFixture host)
{
    [Fact]
    public async Task IndependentInstances_KeepOriginalParametersRoisResults_WhenSelectingRealEditor()
    {
        VisionTaskViewModel task = null!;
        Window window = null!;
        VisionToolEditorView page = null!;
        var image = VisionImage.FromBuffer(40, 40, PixelFormat.Mono8, Enumerable.Repeat((byte)255, 1600).ToArray());
        host.Invoke(() =>
        {
            var entry = new ToolEntry("OpenCV/Blob", "OpenCV", () =>
            {
                var tool = new Cv.BlobTool();
                tool.Parameters.Features = [];
                return (new OpenCvToolCardAdapter<Cv.BlobParameters, Cv.BlobResult>(tool), tool.Parameters, null);
            });
            task = new(Catalog(entry), new WpfUiDispatcher());
            var first = task.Add(entry);
            var second = task.Add(entry);
            Assert.NotEqual(first.Id, second.Id);
            Assert.NotSame(first.Editor.CurrentParameters, second.Editor.CurrentParameters);
            Assert.IsType<Cv.BlobParameters>(first.Editor.CurrentParameters).MinGray = 128;
            Assert.IsType<Cv.BlobParameters>(second.Editor.CurrentParameters).MaxGray = 0;
            first.Editor.RoiEditor.Rois.Add(Roi.Rectangle(10, 10, 4, 4));
            first.Editor.RoiEditor.Selected = first.Editor.RoiEditor.Rois[0];
            task.SetImage(image);
            page = new VisionToolEditorView { DataContext = task };
            page.SetBinding(VisionToolEditorView.SessionProperty, new Binding(nameof(task.SelectedEditor)));
            window = new Window { Content = page, Width = 1200, Height = 700 };
            window.Show(); window.UpdateLayout();
        });
        try
        {
            await host.Invoke(() => task.RunTaskAsync());
            host.Invoke(() =>
            {
                Assert.Equal("任务执行完成", task.StatusText);
                var first = task.Nodes[0]; var second = task.Nodes[1];
                var parameters = first.Editor.CurrentParameters;
                var result = first.Result;
                Assert.Equal(16, Assert.Single(Assert.IsType<Cv.BlobResult>(result).Items).Area);
                Assert.Empty(Assert.IsType<Cv.BlobResult>(second.Result).Items);
                Assert.True(first.RunSequence < second.RunSequence);
                task.SelectedNode = first;
                window.UpdateLayout();
                var editor = page;
                Assert.Same(first.Editor, editor.Session);
                task.SelectedNode = second; window.UpdateLayout();
                Assert.Same(second.Editor, editor.Session);
                task.SelectedNode = first; window.UpdateLayout();
                Assert.Same(parameters, editor.Session.CurrentParameters);
                Assert.Same(result, editor.Session.CurrentCard!.LastResult);
                Assert.Single(first.Editor.RoiEditor.Rois);
                Assert.Empty(second.Editor.RoiEditor.Rois);
                Assert.All(image.Pixels, p => Assert.Equal(255, p));
            });
        }
        finally { host.Invoke(() => { window.Close(); task.Dispose(); }); }
    }

    [Fact]
    public async Task ReorderChangesActualEngineOrder_AndRemovalReleasesOnlySelectedInstance()
    {
        List<int> order = [];
        List<ProbeCard> cards = [];
        VisionTaskViewModel task = null!;
        var entry = new ToolEntry("Probe", "Test", () =>
        {
            var id = cards.Count;
            var card = new ProbeCard { Execute = () => { order.Add(id); return new ToolResult(); } };
            cards.Add(card);
            return (card, new object(), null);
        });
        host.Invoke(() =>
        {
            task = new(Catalog(entry), new WpfUiDispatcher());
            task.Add(entry); task.Add(entry);
            task.MoveUpCommand.Execute(null);
            Assert.Equal("Probe 2", task.Nodes[0].Name);
        });
        try
        {
            await host.Invoke(() => task.RunTaskAsync());
            host.Invoke(() =>
            {
                Assert.Equal(new[] { 1, 0 }, order);
                task.RemoveToolCommand.Execute(null);
                Assert.Single(task.Nodes);
                Assert.Equal(0, cards[0].DisposeCount);
                Assert.Equal(1, cards[1].DisposeCount);
            });
        }
        finally { host.Invoke(task.Dispose); }
        Assert.All(cards, c => Assert.Equal(1, c.DisposeCount));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StopOrCloseWaitsForActualBackgroundCompletion_DisablesMutation_AndDiscardsLateResult(bool close)
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var resume = new ManualResetEventSlim();
        var result = new TrackedResult();
        var card = new ProbeCard { Background = () =>
        {
            started.SetResult();
            if (!resume.Wait(TimeSpan.FromSeconds(20))) throw new TimeoutException();
            return result;
        } };
        var entry = new ToolEntry("Probe", "Test", () => (card, new object(), null));
        VisionTaskViewModel task = null!;
        Task execution = null!;
        Task? closing = null;
        host.Invoke(() => { task = new(Catalog(entry), new WpfUiDispatcher()); task.Add(entry); execution = task.RunTaskAsync(); });
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            host.Invoke(() =>
            {
                Assert.True(task.IsRunning);
                Assert.False(task.AddToolCommand.CanExecute(null));
                Assert.False(task.RemoveToolCommand.CanExecute(null));
                Assert.False(task.RunTaskCommand.CanExecute(null));
                if (close)
                {
                    closing = task.DisposeAsync().AsTask();
                    Assert.False(closing.IsCompleted);
                    Assert.Equal(0, card.DisposeCount);
                }
                else task.StopTaskCommand.Execute(null);
                Assert.True(task.IsRunning);
                Assert.Equal(0, result.DisposeCount);
            });
        }
        finally { resume.Set(); }
        await execution.WaitAsync(TimeSpan.FromSeconds(20));
        if (closing != null) await closing.WaitAsync(TimeSpan.FromSeconds(20));
        host.Invoke(() =>
        {
            Assert.False(task.IsRunning);
            Assert.Equal("任务已停止", task.StatusText);
            Assert.Null(task.Nodes[0].Result);
            Assert.Equal(1, result.DisposeCount);
            task.Dispose();
        });
        Assert.Equal(1, card.DisposeCount);
    }

    [Fact]
    public async Task FailureStopsDownstream_AndClearsEarlierSuccessFromItsEditor()
    {
        List<ProbeCard> cards = [];
        var entry = new ToolEntry("Probe", "Test", () =>
        {
            var card = new ProbeCard(); cards.Add(card);
            return (card, new object(), null);
        });
        VisionTaskViewModel task = null!;
        host.Invoke(() => { task = new(Catalog(entry), new WpfUiDispatcher()); task.Add(entry); task.Add(entry); });
        try
        {
            await host.Invoke(() => task.RunTaskAsync());
            host.Invoke(() => cards[0].Execute = () => new ToolResult { Success = false, Message = "实际失败" });
            await host.Invoke(() => task.RunTaskAsync());
            host.Invoke(() =>
            {
                Assert.StartsWith("任务失败", task.StatusText);
                Assert.Equal(1, cards[1].RunCount);
                Assert.Null(task.Nodes[1].Result);
            });
        }
        finally { host.Invoke(task.Dispose); }
    }

    [Fact]
    public void LegacyTaskIsNotNavigable_AndSharedEditorKeepsBorrowedSessionAcrossUnload()
    {
        host.Invoke(() =>
        {
            var registry = (IContainerRegistry)ContainerLocator.Container;
            Assert.False(registry.IsRegistered(typeof(object), "VisionTaskView"));
            Assert.False(registry.IsRegistered(typeof(object), "VisionBrowserView"));
            Assert.True(registry.IsRegistered(typeof(object), nameof(VisionToolEditorView)));
            using var task = new VisionTaskViewModel(Vision.Controls.VisionCatalogFactory.CreateCatalog(), new WpfUiDispatcher());
            var page = new VisionToolEditorView { DataContext = task };
            page.SetBinding(VisionToolEditorView.SessionProperty, new Binding(nameof(task.SelectedEditor)));
            var window = new System.Windows.Window { Content = page, Width = 1200, Height = 800, Left = -10000, ShowInTaskbar = false };
            window.Show();
            try
            {
                task.SelectedEntry = task.ToolEntries.Single(e => e.Name == "OpenCV/Blob");
                task.AddToolCommand.Execute(null);
                var node = task.SelectedNode;
                window.Content = null;
                Assert.False(node!.Editor.Model.IsClosed);
                window.Content = page;
                Assert.Same(task, page.DataContext); Assert.Same(node, task.SelectedNode);
                task.RemoveToolCommand.Execute(null);
                Assert.True(node.Editor.Model.IsClosed);
            }
            finally { window.Close(); }
        });
    }

    private static VisionBrowserCatalog Catalog(ToolEntry entry) => new(new[] { entry }, [],
        _ => throw new NotSupportedException(), () => null);

    private sealed class ProbeCard : IExecutableTool
    {
        public string Name => "Probe";
        public long LastElapsedMs => 0;
        public Func<ToolResult> Execute { get; set; } = () => new ToolResult();
        public Func<ToolResult>? Background { get; init; }
        public int DisposeCount { get; private set; }
        public int RunCount { get; private set; }
        public Vision.Core.Results.ValidationResult Validate() => new();
        public ToolResult Run(VisionImage? image) { RunCount++; return Execute(); }
        public Func<ToolResult>? PrepareBackgroundRun(VisionImage? image) => Background;
        public void EnsureLoaded() { }
        public void Dispose() => DisposeCount++;
    }
    private sealed class TrackedResult : ToolResult
    {
        public int DisposeCount { get; private set; }
        public override void Dispose() => DisposeCount++;
    }
}
