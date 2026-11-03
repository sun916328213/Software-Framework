using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FlowEditor.Controls.Presentation.Threading;
using Vision.Controls.Models;
using Vision.Controls.Services;
using Vision.Controls.View;
using Vision.Controls.ViewModels;
using Vision.Core.Imaging;
using Vision.Core.Results;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
[Trait("Category", "Regression")]
public sealed class VisionTaskP0Tests(WorkbenchAppFixture host)
{
    [Fact]
    public async Task DisposeFailure_StillReleasesRemainingInstances_AndReportsBothErrors()
    {
        var cards = new[]
        {
            new TestCard { DisposeError = new InvalidOperationException("释放错误A") },
            new TestCard(),
            new TestCard { DisposeError = new InvalidOperationException("释放错误B") }
        };
        VisionTaskViewModel task = null!;
        host.Invoke(() => task = CreateTask(cards));
        try
        {
            var error = await Record.ExceptionAsync(() => host.Invoke(() => task.DisposeAsync().AsTask()));
            Assert.All(cards, card => Assert.Equal(1, card.DisposeCount));
            Assert.NotNull(error);
            Assert.Contains("释放错误A", error.ToString());
            Assert.Contains("释放错误B", error.ToString());
            host.Invoke(() => Assert.All(task.Nodes, n => Assert.True(n.Editor.Model.IsClosed)));
        }
        finally { host.Invoke(() => Cleanup(task)); }
    }

    [Fact]
    public async Task ToolDisposeFailure_DoesNotSkipOwnedProviderRelease()
    {
        var card = new TestCard { DisposeError = new InvalidOperationException("工具释放失败") };
        var provider = new TestProvider();
        VisionTaskViewModel task = null!;
        host.Invoke(() =>
        {
            var entry = new ToolEntry("离线工具", "Test", () => (card, new object(), _ => { }));
            task = new(new VisionBrowserCatalog(new[] { entry }, new[] { new ProviderEntry(provider.Name, true) },
                _ => provider, () => null), new WpfUiDispatcher());
            task.Add(entry);
        });
        try
        {
            var error = await Record.ExceptionAsync(() => host.Invoke(() => task.DisposeAsync().AsTask()));
            Assert.NotNull(error);
            Assert.Contains("工具释放失败", error.ToString());
            Assert.Equal(1, card.DisposeCount);
            Assert.Equal(1, provider.DisposeCount);
        }
        finally { host.Invoke(() => Cleanup(task)); }
    }

    [Fact]
    public async Task DisposeFailure_DoesNotSkipWaitingForAnotherRunningInstance()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var resume = new ManualResetEventSlim();
        var result = new TrackedResult();
        var cards = new[]
        {
            new TestCard { DisposeError = new InvalidOperationException("空闲实例释放失败") },
            new TestCard { Background = () =>
            {
                started.SetResult();
                if (!resume.Wait(TimeSpan.FromSeconds(20))) throw new TimeoutException();
                return result;
            } },
            new TestCard()
        };
        VisionTaskViewModel task = null!;
        Task execution = null!;
        Task? closing = null;
        host.Invoke(() => { task = CreateTask(cards); execution = task.RunTaskAsync(); });
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            host.Invoke(() =>
            {
                closing = task.DisposeAsync().AsTask();
                Assert.False(closing.IsCompleted);
                Assert.Equal(1, cards[2].DisposeCount);
                Assert.Equal(0, cards[1].DisposeCount);
                Assert.Equal(0, result.DisposeCount);
            });
        }
        finally
        {
            resume.Set();
            await execution.WaitAsync(TimeSpan.FromSeconds(20));
            if (closing != null) await Record.ExceptionAsync(() => closing.WaitAsync(TimeSpan.FromSeconds(20)));
            host.Invoke(() => Cleanup(task));
        }
        var error = await Record.ExceptionAsync(() => closing!);
        Assert.NotNull(error);
        Assert.Contains("空闲实例释放失败", error.ToString());
        Assert.All(cards, card => Assert.Equal(1, card.DisposeCount));
        Assert.Equal(1, result.DisposeCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ManualCardRun_CloseWaitsForActualCompletion_AndDiscardsLateResult(bool useCommand)
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var resume = new ManualResetEventSlim();
        var result = new TrackedResult();
        var card = new TestCard { Background = () =>
        {
            started.SetResult();
            if (!resume.Wait(TimeSpan.FromSeconds(20))) throw new TimeoutException();
            return result;
        } };
        VisionTaskViewModel task = null!;
        Task execution = null!;
        Task? closing = null;
        host.Invoke(() =>
        {
            task = CreateTask(card);
            var editorCard = task.Nodes[0].Editor.CurrentCard!;
            if (useCommand) { editorCard.RunCommand.Execute(null); execution = editorCard.RunCommand.ExecutionTask!; }
            else execution = editorCard.RunAsync();
        });
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            host.Invoke(() =>
            {
                closing = task.DisposeAsync().AsTask();
                Assert.False(closing.IsCompleted);
                Assert.Equal(0, card.DisposeCount);
                Assert.Equal(0, result.DisposeCount);
            });
        }
        finally
        {
            resume.Set();
            await execution.WaitAsync(TimeSpan.FromSeconds(20));
            if (closing != null) await closing.WaitAsync(TimeSpan.FromSeconds(20));
            host.Invoke(() => Cleanup(task));
        }
        Assert.Equal(1, card.DisposeCount);
        Assert.Equal(1, result.DisposeCount);
        host.Invoke(() => Assert.Null(task.Nodes[0].Result));
    }

    [Fact]
    public async Task BusyTask_KeepsCanvasZoomAndPanCommands_ButLocksOtherInstanceEditing()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var resume = new ManualResetEventSlim();
        var card = new TestCard { Background = () =>
        {
            started.SetResult();
            if (!resume.Wait(TimeSpan.FromSeconds(20))) throw new TimeoutException();
            return new ToolResult();
        } };
        VisionTaskViewModel task = null!;
        VisionToolEditorView page = null!;
        Window window = null!;
        Task execution = null!;
        host.Invoke(() =>
        {
            task = CreateTask(card, new TestCard());
            task.SetImage(VisionImage.FromBuffer(100, 100, Vision.Core.Imaging.PixelFormat.Mono8,
                Enumerable.Repeat((byte)255, 10000).ToArray()));
            task.SelectedNode = task.Nodes[0];
            page = new VisionToolEditorView { DataContext = task };
            page.SetBinding(VisionToolEditorView.SessionProperty, new Binding(nameof(task.SelectedEditor)));
            window = new Window { Content = page, Width = 1200, Height = 700 };
            window.Show(); window.UpdateLayout();
            execution = task.RunTaskAsync();
        });
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            host.Invoke(() =>
            {
                // 另一实例还未执行，也必须只读；但原图浏览不能跟着整页禁用。
                task.SelectedNode = task.Nodes[1]; window.UpdateLayout();
                var editor = page;
                var viewport = editor.ImagePanel.ImageViewport;
                Assert.True(viewport.IsEnabled);
                Assert.False(viewport.IsRoiEditingEnabled);
                Assert.False(editor.Session!.IsEditingEnabled);
                Assert.False(editor.Session.CurrentCard!.RunCommand.CanExecute(null));
                Assert.False(Assert.IsType<TabItem>(editor.DetailsPanel.FindName("ParametersTab")).IsEnabled);
                Assert.False(task.LoadImageCommand.CanExecute(null));
                Assert.False(task.AddToolCommand.CanExecute(null));
                viewport.ActualPixels();
                var before = WhitePixelsAcrossCenter(viewport);
                var zoom = viewport.ContextMenu.Items.OfType<MenuItem>().Single(m => Equals(m.Header, "放大"));
                Assert.True(zoom.IsEnabled);
                zoom.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                Assert.True(WhitePixelsAcrossCenter(viewport) > before);
                viewport.ContextMenu.Items.OfType<MenuItem>().Single(m => Equals(m.Header, "平移"))
                    .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                Assert.Equal("平移", viewport.Mode);
            });
        }
        finally
        {
            resume.Set();
            await execution.WaitAsync(TimeSpan.FromSeconds(20));
            host.Invoke(() => { window.Close(); Cleanup(task); });
        }
        host.Invoke(() => Assert.False(task.IsRunning));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WindowClose_DrainsActiveTaskAndCachedPage_ThroughRealHostLifetime(bool runWholeTask)
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var resume = new ManualResetEventSlim();
        var result = new TrackedResult();
        var card = new TestCard { Background = () =>
        {
            started.SetResult();
            if (!resume.Wait(TimeSpan.FromSeconds(20))) throw new TimeoutException();
            return result;
        } };
        var cachedCard = new TestCard { DisposeError = new InvalidOperationException("缓存页释放失败") };
        VisionTaskViewModel task = null!;
        VisionTaskViewModel cachedTask = null!;
        Window window = null!;
        WPFCustomControls.Workbench.WorkbenchLifetime lifetime = null!;
        Task execution = null!;
        host.Invoke(() =>
        {
            task = CreateTask(card);
            cachedTask = CreateTask(cachedCard);
            // 此用例验证区域DataContext所有权，不保留已退役列表页布局。
            var activePage = new ContentControl { DataContext = task };
            var cachedPage = new ContentControl { DataContext = cachedTask };
            var regions = new Prism.Regions.RegionManager();
            var region = new Prism.Regions.Region { Name = "P0LocalRegion" };
            regions.Regions.Add(region);
            region.Add(cachedPage); // 先清理一个失败的非活动缓存页，不能跳过活动页等待。
            region.Add(activePage);
            region.Activate(activePage);
            Assert.DoesNotContain(cachedPage, region.ActiveViews);
            window = new Window { Content = activePage, Width = 1200, Height = 700 };
            window.Closed += (_, _) => closed.TrySetResult();
            lifetime = new(window, () => WPFCustomControls.Workbench.WorkbenchLifetime.CloseRegionAsync(regions, region.Name));
            window.Show(); window.UpdateLayout();
            if (runWholeTask) execution = task.RunTaskAsync();
            else
            {
                var command = task.Nodes[0].Editor.CurrentCard!.RunCommand;
                command.Execute(null);
                execution = command.ExecutionTask!;
            }
        });
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            host.Invoke(() =>
            {
                window.Close(); // 走真实Closing事件，不直接调用任务VM的DisposeAsync。
                Assert.True(window.IsVisible);
                Assert.False(window.IsEnabled);
                Assert.False(lifetime.BeginCleanup().IsCompleted);
                Assert.Equal(1, cachedCard.DisposeCount);
                Assert.Equal(0, card.DisposeCount);
                Assert.Equal(0, result.DisposeCount);
            });
        }
        finally
        {
            resume.Set();
            await execution.WaitAsync(TimeSpan.FromSeconds(20));
            await host.Invoke(lifetime.BeginCleanup).WaitAsync(TimeSpan.FromSeconds(20));
            await closed.Task.WaitAsync(TimeSpan.FromSeconds(20));
            host.Invoke(() => { Cleanup(task); Cleanup(cachedTask); });
        }
        Assert.Equal(1, card.DisposeCount);
        Assert.Equal(1, cachedCard.DisposeCount);
        Assert.Equal(1, result.DisposeCount);
        host.Invoke(() =>
        {
            Assert.False(window.IsVisible);
            Assert.Null(task.Nodes[0].Result);
            Assert.True(cachedTask.Nodes[0].Editor.Model.IsClosed);
        });
        var error = await Record.ExceptionAsync(() => host.Invoke(() => cachedTask.DisposeAsync().AsTask()));
        Assert.NotNull(error);
        Assert.Contains("缓存页释放失败", error.ToString());
    }

    private VisionTaskViewModel CreateTask(params TestCard[] cards)
    {
        var next = 0;
        var entry = new ToolEntry("离线受控工具", "Test", () => (cards[next++], new object(), null));
        var task = new VisionTaskViewModel(new VisionBrowserCatalog(new[] { entry }, [],
            _ => throw new NotSupportedException(), () => null), new WpfUiDispatcher());
        foreach (var _ in cards) task.Add(entry);
        return task;
    }

    private static int WhitePixelsAcrossCenter(FrameworkElement view)
    {
        view.UpdateLayout();
        var width = (int)Math.Ceiling(view.ActualWidth);
        var height = (int)Math.Ceiling(view.ActualHeight);
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(view);
        var row = new byte[width * 4];
        bitmap.CopyPixels(new Int32Rect(0, height / 2, width, 1), row, row.Length, 0);
        return Enumerable.Range(0, width).Count(x => row[x * 4] > 250 && row[x * 4 + 1] > 250 && row[x * 4 + 2] > 250);
    }

    private static void Cleanup(VisionTaskViewModel task)
    {
        // 红测也要回收其它fake实例；不让预期释放错误破坏共享STA宿主退出。
        foreach (var node in task.Nodes) { try { node.Dispose(); } catch { } }
        try { task.Dispose(); } catch { }
    }

    private sealed class TestCard : IExecutableTool
    {
        public string Name => "离线受控工具";
        public long LastElapsedMs => 0;
        public Func<ToolResult>? Background { get; init; }
        public Exception? DisposeError { get; init; }
        public int DisposeCount { get; private set; }
        public Vision.Core.Results.ValidationResult Validate() => new();
        public ToolResult Run(VisionImage? image) => new();
        public Func<ToolResult>? PrepareBackgroundRun(VisionImage? image) => Background;
        public void EnsureLoaded() { }
        public void Dispose() { DisposeCount++; if (DisposeError != null) throw DisposeError; }
    }

    private sealed class TestProvider : Vision.Core.AI.IAIProvider, IDisposable
    {
        public string Name => "离线Provider";
        public Vision.Core.AI.AICapabilities Capabilities => default;
        public bool IsAvailable => true;
        public int DisposeCount { get; private set; }
        public Vision.Core.AI.IAIModelSession OpenSession(Vision.Core.AI.AIModelRequest request) => throw new NotSupportedException();
        public void CloseSession(Vision.Core.AI.IAIModelSession session) => throw new NotSupportedException();
        public void Dispose() => DisposeCount++;
    }

    private sealed class TrackedResult : ToolResult
    {
        public int DisposeCount { get; private set; }
        public override void Dispose() => DisposeCount++;
    }
}
