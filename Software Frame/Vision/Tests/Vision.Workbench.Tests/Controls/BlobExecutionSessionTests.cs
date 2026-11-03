using System.Windows;
using Vision.Controls.Models;
using Vision.Controls.Services;
using Vision.Controls.View;
using Vision.Controls.ViewModels;
using Vision.Core.Imaging;
using Vision.Core.Results;
using Vision.Core.Roi;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
public sealed class BlobExecutionSessionTests(WorkbenchAppFixture host)
{
    [Fact]
    public async Task BusyCommand_PreventsReentry_DisablesEditing_ButKeepsViewportUsable_AndPublishesOnUi()
    {
        using var tool = new PreparedTool();
        VisionBrowserWorkspace? workspace = null;
        VisionBrowserViewModel? browser = null;
        Window? window = null;
        Task? run = null;
        var notifications = new List<int>();
        try
        {
            var uiThread = host.Invoke(() => Environment.CurrentManagedThreadId);
            run = host.Invoke<Task>(() =>
            {
                workspace = Workspace(tool); browser = new(workspace);
                browser.LoadImage();
                var page = new VisionToolEditorView { Session = browser };
                window = new Window { Content = page, Width = 1250, Height = 800, Left = -10000, Top = -10000, ShowInTaskbar = false };
                window.Show();
                browser.CurrentCard!.Model.PropertyChanged += (_, _) => notifications.Add(Environment.CurrentManagedThreadId);
                return browser.CurrentCard.RunCommand.ExecuteAsync(null);
            });
            await tool.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            host.Invoke(() =>
            {
                Assert.True(browser!.CurrentCard!.Model.IsBusy);
                Assert.False(browser.CurrentCard.RunCommand.CanExecute(null));
                Assert.False(browser.LoadImageCommand.CanExecute(null));
                Assert.False(browser.IsEditingEnabled);
                Assert.True(browser.IsSessionActive);
                var page = Assert.IsType<VisionToolEditorView>(window!.Content);
                var viewport = Assert.IsType<VisionImageViewport>(page.ImagePanel.FindName("Viewport"));
                Assert.True(viewport.IsEnabled); Assert.False(viewport.IsRoiEditingEnabled);
                viewport.ActualPixels(); viewport.Fit(); // 仍是可交互画布，不通过禁用页面实现互斥。
                browser.CurrentCard.Run();
                Assert.Equal(1, tool.Preparations);
            });
            tool.Release.Set();
            await run.WaitAsync(TimeSpan.FromSeconds(10));
            host.Invoke(() =>
            {
                Assert.False(browser!.CurrentCard!.Model.IsBusy); Assert.True(browser.IsEditingEnabled);
                Assert.Same(tool.Produced, browser.CurrentCard.LastResult);
                Assert.Equal("执行成功", browser.CurrentCard.StatusText);
                Assert.NotEqual(uiThread, tool.WorkerThread);
                Assert.All(notifications, thread => Assert.Equal(uiThread, thread));
            });
        }
        finally
        {
            tool.Release.Set();
            if (run != null) await run.WaitAsync(TimeSpan.FromSeconds(15));
            host.Invoke(() => { window?.Close(); browser?.Dispose(); workspace?.Dispose(); });
            tool.DisposeGate();
        }
    }
    [Theory]
    [InlineData("image")]
    [InlineData("parameters")]
    [InlineData("dispose")]
    [InlineData("switch")]
    public async Task ChangedOrClosedSession_DiscardsAndDisposesLateResult(string change)
    {
        using var tool = new PreparedTool();
        var workspace = host.Invoke(() => Workspace(tool));
        var card = host.Invoke(() => workspace.Model.CurrentCard!);
        Task? run = null;
        try
        {
            run = host.Invoke<Task>(card.RunAsync);
            await tool.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            host.Invoke(() =>
            {
                switch (change)
                {
                    case "image": card.SetImage(VisionImage.FromBuffer(1, 1, PixelFormat.Mono8, [0])); break;
                    case "parameters": card.ClearResult(); break;
                    case "dispose": workspace.Dispose(); break;
                    case "switch": workspace.Model.SelectedEntry = workspace.Model.ToolEntries[1]; break;
                }
            });
            tool.Release.Set();
            await run.WaitAsync(TimeSpan.FromSeconds(10));
            host.Invoke(() =>
            {
                Assert.Null(card.LastResult); Assert.False(card.Model.IsBusy);
                Assert.NotNull(tool.Produced); Assert.Equal(1, tool.Produced!.Disposals);
                if (change is "dispose" or "switch") Assert.True(tool.OriginalDisposed);
                if (change == "switch") Assert.Null(workspace.Model.CurrentCard!.LastResult);
            });
        }
        finally
        {
            tool.Release.Set();
            if (run != null) await run.WaitAsync(TimeSpan.FromSeconds(15));
            host.Invoke(workspace.Dispose);
            tool.DisposeGate();
        }
    }
    [Fact]
    public async Task WorkerFailure_IsVisible_AndReleasesBusyStateForRetry()
    {
        using var tool = new PreparedTool { Fail = true };
        var card = host.Invoke(() => new ToolCardViewModel(tool));
        Task? run = null;
        try
        {
            run = host.Invoke<Task>(card.RunAsync);
            await tool.Started.Task.WaitAsync(TimeSpan.FromSeconds(10)); tool.Release.Set();
            await run.WaitAsync(TimeSpan.FromSeconds(10));
            host.Invoke(() => { Assert.False(card.Model.IsBusy); Assert.Contains("controlled failure", card.StatusText); tool.Fail = false; });
            run = host.Invoke<Task>(card.RunAsync);
            await run.WaitAsync(TimeSpan.FromSeconds(10));
            host.Invoke(() => { Assert.Equal("执行成功", card.StatusText); Assert.NotNull(card.LastResult); });
        }
        finally
        {
            tool.Release.Set(); if (run != null) await run.WaitAsync(TimeSpan.FromSeconds(15));
            host.Invoke(card.Dispose);
            tool.DisposeGate();
        }
    }
    private static VisionBrowserWorkspace Workspace(PreparedTool tool) => new(new(
        [new("Prepared", "Unknown", () => (tool, new object(), null)),
         new("Other", "Unknown", () => (new ResultSelectionTests.ResultTool(() => new ToolResult()), new object(), null))], [],
        _ => throw new InvalidOperationException(), TestBrowserViewModel.CreateInput));

    private sealed class TrackingResult : ToolResult
    {
        public int Disposals;
        public override void Dispose() => Disposals++;
    }
    private sealed class PreparedTool : IExecutableTool, ISearchRegionTool
    {
        public string Name => "Controlled prepared execution";
        public long LastElapsedMs => 0;
        public ISearchRegionTool SearchRegionTool => this;
        public SearchRegion? SearchRegion { get; set; }
        public bool Fail, OriginalDisposed;
        public int Preparations, WorkerThread;
        public TrackingResult? Produced;
        public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ManualResetEventSlim Release { get; } = new(false);
        public ValidationResult Validate() => new();
        public ToolResult Run(VisionImage? image) => throw new InvalidOperationException("原工具同步 Run 不应被后台调用。");
        public Func<ToolResult> PrepareBackgroundRun(VisionImage? image)
        {
            Preparations++;
            var fail = Fail;
            return () =>
            {
                WorkerThread = Environment.CurrentManagedThreadId; Started.TrySetResult(true);
                if (!Release.Wait(TimeSpan.FromSeconds(15))) throw new TimeoutException("测试门闩未释放。");
                if (fail) throw new InvalidOperationException("controlled failure");
                return Produced = new TrackingResult();
            };
        }
        public void EnsureLoaded() { }
        public void Dispose() => OriginalDisposed = true;
        // 测试门闩由用例在任务结束后释放，不代表原工具拥有独立执行的资源。
        public void DisposeGate() => Release.Dispose();
    }
}
