// 【中文阅读说明】
// WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：WorkbenchLifetimeTests、CancellableViewModel、DisposableView、CleanupViewModel、AsyncCleanupViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Windows.Threading、CommunityToolkit.Mvvm.Input、Prism.Regions、WPFCustomControls.Tests.Window、WPFCustomControls.Workbench。导入不代表所有类型都会在运行时加载。
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using Prism.Regions;
using WPFCustomControls.Tests.Window;
using WPFCustomControls.Workbench;

namespace WPFCustomControls.Tests.Workbench;

/// <summary>
/// WorkbenchLifetimeTests 的类型定义。WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class WorkbenchLifetimeTests
{
    /// <summary>
    /// 测试相关入口 WindowClosing_AfterTimeout_AllowsAnExplicitDecisionAndCleansUpOnce；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Run、FromMilliseconds、Show、Close、PumpUntil；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Theory]
    [InlineData("continue")]
    [InlineData("close")]
    [InlineData("complete")]
    public void WindowClosing_AfterTimeout_AllowsAnExplicitDecisionAndCleansUpOnce(string decision)
    {
        StaFact.Run(() =>
        {
            var window = new System.Windows.Window();
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var calls = 0;
            var remainingReleased = false;
            var lifetime = new WorkbenchLifetime(window,
                async () => { calls++; await release.Task; },
                () => { remainingReleased = true; return ValueTask.CompletedTask; })
                { CloseTimeout = TimeSpan.FromMilliseconds(40) };
            try
            {
                window.Show();
                window.Close();
                PumpUntil(() => lifetime.CloseStatus.IsTimedOut);
                Assert.True(window.IsVisible);
                Assert.False(window.IsEnabled);
                Assert.Single(window.OwnedWindows.Cast<System.Windows.Window>());
                Assert.True(lifetime.CloseStatus.ContinueWaitingCommand.CanExecute(null));
                Assert.True(lifetime.CloseStatus.CloseWithoutWaitingCommand.CanExecute(null));
                if (decision == "continue")
                {
                    // Complete on the state change so a second timeout cannot hide the transient waiting state.
                    lifetime.CloseStatus.PropertyChanged += (_, args) =>
                    {
                        if (args.PropertyName == nameof(WorkbenchCloseViewModel.IsTimedOut)
                            && !lifetime.CloseStatus.IsTimedOut) release.TrySetResult();
                    };
                    lifetime.CloseStatus.ContinueWaitingCommand.Execute(null);
                    Assert.True(window.IsVisible);
                }
                else if (decision == "close") lifetime.CloseStatus.CloseWithoutWaitingCommand.Execute(null);
                else release.SetResult();

                PumpUntil(() => !window.IsVisible);
                Assert.Empty(window.OwnedWindows.Cast<System.Windows.Window>());
                if (decision == "close")
                {
                    Assert.False(remainingReleased);
                    Assert.False(lifetime.BeginCleanup().IsCompleted);
                    release.SetResult();
                }
                PumpUntil(() => lifetime.BeginCleanup().IsCompleted);
                Assert.True(remainingReleased);
                Assert.Equal(1, calls);
            }
            finally { release.TrySetResult(); }
        });
    }

    /// <summary>
    /// 测试相关入口 PumpUntil；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 AddSeconds、FromMilliseconds、condition、Start、PushFrame；具体条件和先后次序见方法体。
    /// </summary>
    private static void PumpUntil(Func<bool> condition)
    {
        var frame = new DispatcherFrame();
        var deadline = DateTime.UtcNow.AddSeconds(5);
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(5) };
        timer.Tick += (_, _) => { if (condition() || DateTime.UtcNow >= deadline) frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
        timer.Stop();
        Assert.True(condition(), "Dispatcher did not reach the expected close state.");
    }

    /// <summary>
    /// 测试相关入口 CloseRegion_RequestsCancellationOfRunningCommands；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Run、Add、ExecuteAsync、CloseRegionAsync、SetResult；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void CloseRegion_RequestsCancellationOfRunningCommands()
    {
        StaFact.Run(() =>
        {
            var manager = new RegionManager();
            var region = new Region { Name = "TestRegion" };
            manager.Regions.Add(region);
            var vm = new CancellableViewModel();
            region.Add(new DisposableView { DataContext = vm });
            _ = vm.Command.ExecuteAsync(null);
            var cleanup = WorkbenchLifetime.CloseRegionAsync(manager, "TestRegion");
            var cancelled = vm.Command.IsCancellationRequested;
            vm.Release.SetResult();
            cleanup.GetAwaiter().GetResult();
            Assert.True(cancelled);
        });
    }

    /// <summary>
    /// CancellableViewModel 的类型定义。WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// </summary>
    private sealed class CancellableViewModel
    {
        public TaskCompletionSource Release { get; } = new();
        public IAsyncRelayCommand Command { get; }
        /// <summary>
        /// 初始化 CancellableViewModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public CancellableViewModel() => Command = new AsyncRelayCommand(_ => Release.Task);
    }

    /// <summary>
    /// 测试相关入口 CloseRegion_AfterCleanupFailure_AttemptsEveryStageAndRemainingPages；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Run、Add、GetResult、GetAwaiter、StartCommand；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    [Theory]
    [InlineData("sync-dispose")]
    [InlineData("async-dispose")]
    [InlineData("command-fault")]
    [InlineData("command-cancel")]
    [InlineData("view-dispose")]
    [InlineData("clear-context")]
    public void CloseRegion_AfterCleanupFailure_AttemptsEveryStageAndRemainingPages(string failure)
    {
        StaFact.Run(() =>
        {
            var manager = new RegionManager();
            var region = new Region { Name = "TestRegion" };
            manager.Regions.Add(region);
            var view = new DisposableView { FailDispose = failure == "view-dispose" };
            CleanupViewModel vm = failure == "async-dispose"
                ? new AsyncCleanupViewModel(failure)
                : new CleanupViewModel(failure);
            vm.OnDispose = () =>
            {
                if (failure == "clear-context") view.DataContext = null;
            };
            view.DataContext = vm;
            var remainingVm = new CleanupViewModel("");
            var remainingView = new DisposableView { DataContext = remainingVm };
            region.Add(view);
            region.Add(remainingView);
            // Observe the command failure without letting it escape the test thread.
            try { vm.StartCommand().GetAwaiter().GetResult(); }
            catch (InvalidOperationException) { }
            catch (OperationCanceledException) { }

            WorkbenchLifetime.CloseRegionAsync(manager, "TestRegion").GetAwaiter().GetResult();

            Assert.Equal(1, vm.DisposeCalls);
            Assert.Equal(1, view.DisposeCalls);
            Assert.Equal(1, vm.CommandReads);
            Assert.Equal(1, remainingVm.DisposeCalls);
            Assert.Equal(1, remainingVm.CommandReads);
            Assert.Equal(1, remainingView.DisposeCalls);
        });
    }

    /// <summary>
    /// DisposableView 的类型定义。WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：System.Windows.FrameworkElement、IDisposable。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class DisposableView : System.Windows.FrameworkElement, IDisposable
    {
        public bool FailDispose { get; init; }
        public int DisposeCalls { get; private set; }

        /// <summary>
        /// 测试相关入口 Dispose；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public void Dispose()
        {
            DisposeCalls++;
            if (FailDispose) throw new InvalidOperationException("view release failed");
        }
    }

    /// <summary>
    /// CleanupViewModel 的类型定义。WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：IDisposable。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private class CleanupViewModel(string failure) : IDisposable
    {
        private readonly AsyncRelayCommand _command = new(() => failure switch
        {
            "command-fault" => Task.FromException(new InvalidOperationException("command failed")),
            "command-cancel" => Task.FromCanceled(new CancellationToken(true)),
            _ => Task.CompletedTask
        }, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);

        public Action? OnDispose { get; set; }
        public int DisposeCalls { get; private set; }
        public int CommandReads { get; private set; }
        public IAsyncRelayCommand Command { get { CommandReads++; return _command; } }
        /// <summary>
        /// 测试相关入口 StartCommand；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 ExecuteAsync；具体条件和先后次序见方法体。
        /// </summary>
        public Task StartCommand() => _command.ExecuteAsync(null);

        /// <summary>
        /// 测试相关入口 Dispose；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public void Dispose()
        {
            DisposeCalls++;
            OnDispose?.Invoke();
            if (failure == "sync-dispose")
                throw new InvalidOperationException("view model release failed");
        }
    }

    /// <summary>
    /// AsyncCleanupViewModel 的类型定义。WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：CleanupViewModel、IAsyncDisposable。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class AsyncCleanupViewModel(string failure) : CleanupViewModel(failure), IAsyncDisposable
    {
        /// <summary>
        /// 测试相关入口 DisposeAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Dispose、FromException；具体条件和先后次序见方法体。
        /// </summary>
        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.FromException(new InvalidOperationException("async view model release failed"));
        }
    }

    /// <summary>
    /// 测试相关入口 WindowClosing_WithAlreadyCompletedCleanup_ClosesAfterTheClosingEvent；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Run、Show、True、BeginCleanup、Close；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void WindowClosing_WithAlreadyCompletedCleanup_ClosesAfterTheClosingEvent()
    {
        StaFact.Run(() =>
        {
            var window = new System.Windows.Window();
            var calls = 0;
            var lifetime = new WorkbenchLifetime(window, () => { calls++; return ValueTask.CompletedTask; });
            window.Show();
            Assert.True(lifetime.BeginCleanup().IsCompletedSuccessfully);
            var frame = new DispatcherFrame();
            window.Closed += (_, _) => frame.Continue = false;
            window.Close();
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            timer.Tick += (_, _) => frame.Continue = false;
            timer.Start();
            Dispatcher.PushFrame(frame);
            timer.Stop();
            Assert.False(window.IsVisible);
            Assert.Equal(1, calls);
        });
    }

    /// <summary>
    /// 测试相关入口 WindowClosing_AwaitsCleanupOnce_AndContinuesAfterResourceFailure；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Run、Show、Close、True、Equal；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public void WindowClosing_AwaitsCleanupOnce_AndContinuesAfterResourceFailure()
    {
        StaFact.Run(() =>
        {
            var window = new System.Windows.Window();
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var calls = 0;
            var remainingReleased = false;
            _ = new WorkbenchLifetime(window,
                async () => { calls++; await release.Task; },
                () => throw new InvalidOperationException("release failed"),
                () => { remainingReleased = true; return ValueTask.CompletedTask; });
            window.Show();
            window.Close();
            window.Close();
            Assert.True(window.IsVisible);
            Assert.Equal(1, calls);
            var frame = new DispatcherFrame();
            window.Closed += (_, _) => frame.Continue = false;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            timer.Tick += (_, _) => frame.Continue = false;
            timer.Start();
            release.SetResult();
            Dispatcher.PushFrame(frame);
            timer.Stop();
            Assert.False(window.IsVisible);
            Assert.True(remainingReleased);
            Assert.Equal(1, calls);
        });
    }
}
