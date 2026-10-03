// 【中文阅读说明】
// WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
// 本文件定义：WorkbenchLifetime。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.ComponentModel、System.Diagnostics、System.Windows、CommunityToolkit.Mvvm.Input、Prism.Regions。导入不代表所有类型都会在运行时加载。
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using Prism.Regions;

namespace WPFCustomControls.Workbench;

/// <summary>Awaits resource cleanup while the window dispatcher is still available.</summary>
/// <remarks>
/// WorkbenchLifetime 的类型定义。WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
/// </remarks>
public sealed class WorkbenchLifetime
{
    private readonly System.Windows.Window _window;
    private readonly Func<ValueTask>[] _cleanup;
    private Task? _completion;
    private bool _readyToClose;
    private bool _closingStarted;
    public WorkbenchCloseViewModel CloseStatus { get; } = new();
    public TimeSpan CloseTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 初始化 WorkbenchLifetime 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    public WorkbenchLifetime(System.Windows.Window window, params Func<ValueTask>[] cleanup)
    {
        _window = window;
        _cleanup = cleanup;
        window.Closing += OnClosing;
    }

    /// <summary>Starts cleanup once, also usable as a fallback from application exit.</summary>
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 BeginCleanup。
    /// 正文中的关键协作调用包括 CleanupAsync；具体条件和先后次序见方法体。
    /// </remarks>
    public Task BeginCleanup() => _completion ??= CleanupAsync();

    /// <summary>Drains commands already running on a page; it does not execute new commands.</summary>
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 WaitForCommandsAsync。
    /// 正文中的关键协作调用包括 ToArray、Distinct、OfType、Select、Where；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </remarks>
    public static Task WaitForCommandsAsync(object viewModel)
    {
        var commands = viewModel.GetType().GetProperties()
            .Where(property => typeof(IAsyncRelayCommand).IsAssignableFrom(property.PropertyType)
                && property.GetIndexParameters().Length == 0)
            .Select(property => property.GetValue(viewModel) as IAsyncRelayCommand)
            .OfType<IAsyncRelayCommand>().Distinct().ToArray();
        foreach (var command in commands)
        {
            try { if (command.CanBeCanceled) command.Cancel(); }
            catch (Exception error) { Trace.TraceError("Workbench command cancellation failed: {0}", error); }
        }
        var tasks = commands.Select(command => command.ExecutionTask).OfType<Task>().Distinct().ToArray();
        return Task.WhenAll(tasks);
    }

    /// <summary>Retires all created region pages, including inactive cached pages.</summary>
    /// <remarks>
    /// 执行关闭步骤；对应入口为 CloseRegionAsync。
    /// 正文中的关键协作调用包括 ContainsRegionWithName、ToArray、OfType、DisposeAsync、TraceError；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </remarks>
    public static async ValueTask CloseRegionAsync(IRegionManager regions, string regionName)
    {
        if (!regions.Regions.ContainsRegionWithName(regionName)) return;
        var views = regions.Regions[regionName].Views.OfType<FrameworkElement>().ToArray();
        foreach (var view in views)
        {
            // Disposal may detach DataContext; retain the owner whose commands must be drained.
            var viewModel = view.DataContext;
            try
            {
                if (viewModel is IAsyncDisposable asyncDisposable) await asyncDisposable.DisposeAsync();
                else (viewModel as IDisposable)?.Dispose();
            }
            catch (Exception error) { Trace.TraceError("Workbench view model cleanup failed: {0}", error); }

            try
            {
                if (viewModel is not null) await WaitForCommandsAsync(viewModel);
            }
            catch (Exception error) { Trace.TraceError("Workbench page command cleanup failed: {0}", error); }

            try { (view as IDisposable)?.Dispose(); }
            catch (Exception error) { Trace.TraceError("Workbench view cleanup failed: {0}", error); }
        }
    }

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnClosing。
    /// 正文中的关键协作调用包括 BeginCleanup、Show、FromMilliseconds、FromSeconds、WaitAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    private async void OnClosing(object? sender, CancelEventArgs args)
    {
        if (_readyToClose) return;
        args.Cancel = true;
        if (_closingStarted) return;
        _closingStarted = true;
        _window.IsEnabled = false;
        var completion = BeginCleanup();
        WorkbenchCloseWindow? progress = null;
        if (!completion.IsCompleted && _window.IsVisible)
        {
            progress = new WorkbenchCloseWindow(_window, CloseStatus);
            progress.Show();
        }
        var timeout = CloseTimeout > TimeSpan.Zero && CloseTimeout <= TimeSpan.FromMilliseconds(uint.MaxValue - 1)
            ? CloseTimeout : TimeSpan.FromSeconds(30);
        try
        {
            while (!completion.IsCompleted)
            {
                try { await completion.WaitAsync(timeout); }
                catch (TimeoutException)
                {
                    var decision = CloseStatus.AskAfterTimeoutAsync();
                    if (await Task.WhenAny(completion, decision) == completion) break;
                    if (!await decision)
                    {
                        Trace.TraceWarning("Workbench closed before resource cleanup completed by user request.");
                        break;
                    }
                    CloseStatus.ResumeWaiting();
                }
            }
        }
        finally
        {
            // A forced close does not cancel or restart the ongoing cleanup sequence.
            if (completion.IsCompleted) CloseStatus.Finish();
            progress?.Finish();
        }
        _readyToClose = true;
        _window.Closing -= OnClosing;
        // Cleanup may complete synchronously. Finish the current Closing event before closing again.
        _ = _window.Dispatcher.BeginInvoke(new Action(_window.Close));
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 CleanupAsync。
    /// 正文中的关键协作调用包括 ReportProgress、TraceError；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    private async Task CleanupAsync()
    {
        for (var index = 0; index < _cleanup.Length; index++)
        {
            CloseStatus.ReportProgress(index + 1, _cleanup.Length);
            try { await _cleanup[index](); }
            catch (Exception error) { Trace.TraceError("Workbench cleanup failed: {0}", error); }
        }
    }
}
