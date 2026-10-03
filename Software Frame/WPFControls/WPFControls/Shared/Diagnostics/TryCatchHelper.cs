// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：TryCatchHelper。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Diagnostics。导入不代表所有类型都会在运行时加载。
using System.Diagnostics;

namespace WPFControls.Controls.Diagnostics;

/// <summary>容器或组件拥有的错误执行器；上报目标不保存为进程静态状态。</summary>
public sealed class TryCatchHelper
{
    private readonly Action<Exception>? _report;
    private readonly AsyncLocal<bool> _reporting = new();
    private long _nextDiagnosticUtcTicks;
    private int _suppressedDiagnosticCount;
    /// <summary>
    /// 初始化 TryCatchHelper 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    public TryCatchHelper(Action<Exception>? report = null) => _report = report;

    /// <summary>
    /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryExecute。
    /// 正文中的关键协作调用包括 Report；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    public void TryExecute(Action? action, Action<Exception>? onError = null)
    {
        try { action?.Invoke(); }
        catch (Exception error) { Report(error, onError); }
    }
    /// <summary>
    /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryExecuteAsync。
    /// 正文中的关键协作调用包括 action、Report；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    public async Task TryExecuteAsync(Func<Task>? action, Action<Exception>? onError = null)
    {
        try { if (action != null) await action(); }
        catch (Exception error) { Report(error, onError); }
    }
    /// <summary>
    /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryExecute。
    /// 正文中的关键协作调用包括 action、Report；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    public T? TryExecute<T>(Func<T>? action, T? defaultValue = default)
    {
        try { return action != null ? action() : defaultValue; }
        catch (Exception error) { Report(error); return defaultValue; }
    }
    /// <summary>
    /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryExecuteAsync。
    /// 正文中的关键协作调用包括 action、Report；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    public async Task<T?> TryExecuteAsync<T>(Func<Task<T>>? action, T? defaultValue = default)
    {
        try { return action != null ? await action() : defaultValue; }
        catch (Exception error) { Report(error); return defaultValue; }
    }
    /// <summary>
    /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryExecuteWithResult。
    /// 正文中的关键协作调用包括 Report；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    public bool TryExecuteWithResult(Action? action, Action<Exception>? onError = null)
    {
        try { action?.Invoke(); return true; }
        catch (Exception error) { Report(error, onError); return false; }
    }
    /// <summary>
    /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryExecuteWithResultAsync。
    /// 正文中的关键协作调用包括 action、Report；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    public async Task<bool> TryExecuteWithResultAsync(Func<Task>? action)
    {
        try { if (action != null) await action(); return true; }
        catch (Exception error) { Report(error); return false; }
    }
    /// <summary>日志通道自身失败时只写诊断，禁止回报同一个日志通道。</summary>
    public void TryExecuteWithoutReport(Action? action)
    {
        try { action?.Invoke(); }
        catch (Exception error) { ReportFailure(error, "日志上报失败"); }
    }
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Report。
    /// 正文中的关键协作调用包括 ReportFailure、_report；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    private void Report(Exception error, Action<Exception>? onError = null)
    {
        if (_reporting.Value) { ReportFailure(error); return; }
        _reporting.Value = true;
        try
        {
            try { onError?.Invoke(error); }
            catch (Exception callbackError) { ReportFailure(callbackError); }
            if (_report == null) ReportFailure(error);
            else
            {
                try { _report(error); }
                catch (Exception reportingError) { ReportFailure(reportingError); }
            }
        }
        finally { _reporting.Value = false; }
    }
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ReportFailure。
    /// 正文中的关键协作调用包括 Read、CompareExchange、Increment、Exchange、TraceError；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    private void ReportFailure(Exception error, string operation = "错误上报失败")
    {
        var now = DateTime.UtcNow.Ticks;
        var next = Volatile.Read(ref _nextDiagnosticUtcTicks);
        if (now < next || Interlocked.CompareExchange(ref _nextDiagnosticUtcTicks,
                now + TimeSpan.TicksPerSecond * 30, next) != next)
        {
            Interlocked.Increment(ref _suppressedDiagnosticCount);
            return;
        }
        var suppressed = Interlocked.Exchange(ref _suppressedDiagnosticCount, 0);
        try { Trace.TraceError($"[TryCatch] {operation}: {error.Message}; suppressed={suppressed}"); }
        catch { /* 诊断失败不能覆盖原始操作结果。 */ }
    }
}
