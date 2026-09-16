using System.Diagnostics;

namespace FlowEditor.Core;

/// <summary>
/// 流程/节点执行作用域（AsyncLocal）：记录当前流程名、节点名与运行序号，
/// 供节点日志按「流程 + 任务 + 运行次数」路由；同时承载宿主通过 IoC
/// 注入的 <see cref="IFlowLogSink"/> 异步作用域。本类不依赖任何具体日志库。
/// </summary>
public static class FlowLogContext
{
    private static readonly AsyncLocal<string?> Process = new();
    private static readonly AsyncLocal<string?> Flow = new();
    private static readonly AsyncLocal<string?> Node = new();
    private static readonly AsyncLocal<long> Run = new();
    private static readonly AsyncLocal<RunCounter?> CurrentRunCounter = new();
    private static readonly AsyncLocal<bool> SinkSuppressed = new();
    private static readonly AsyncLocal<IFlowLogSink?> Sink = new();

    // 运行序号按「流程名\任务名」分桶计数：同一任务的多次运行在日志中为连续 #1 #2 #3（跨流程互不影响）
    private static readonly Dictionary<FlowLogRoute, RunCounter> _runCounters = new();
    private static readonly object _runLock = new();

    /// <summary>当前流程名（AutoFlow / InitializeFlow ...，容器级；为任务日志的上级目录）。</summary>
    public static string? ProcessName => Process.Value;

    /// <summary>当前任务名（标签页名，AddNewTab 已保证唯一，如 流程图1）。</summary>
    public static string? FlowName => Flow.Value;

    /// <summary>当前节点名（节点执行期间非 null）。</summary>
    public static string? NodeName => Node.Value;

    /// <summary>当前任务运行序号（按「流程名\任务名」独立分桶、从 1 连续递增，用于区分同一任务的多次运行）。</summary>
    public static long RunId => Run.Value;

    /// <summary>为当前异步执行链设置宿主日志 sink，并在释放时恢复原值。</summary>
    public static IDisposable EnterSink(IFlowLogSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        var old = Sink.Value;
        Sink.Value = sink;
        return new Scope(() => Sink.Value = old);
    }

    /// <summary>进入流程（容器）作用域；Dispose 时还原上一流程名（支持嵌套）。</summary>
    public static IDisposable EnterProcess(string? processName)
    {
        var old = Process.Value;
        Process.Value = processName;
        return new Scope(() => Process.Value = old);
    }

    /// <summary>进入流程作用域；Dispose 时还原上一流程名与运行序号（支持嵌套/子流程）。</summary>
    public static IDisposable EnterFlow(string? flowName)
    {
        var oldFlow = Flow.Value;
        var oldRun = Run.Value;
        var oldRunCounter = CurrentRunCounter.Value;
        Flow.Value = flowName;

        // 运行序号按任务（流程名\任务名）独立自增，同一任务多次运行日志中连续编号
        var (runId, counter) = BeginRun(flowName);
        Run.Value = runId;
        CurrentRunCounter.Value = counter;
        return new Scope(() =>
        {
            Flow.Value = oldFlow;
            Run.Value = oldRun;
            CurrentRunCounter.Value = oldRunCounter;
        });
    }

    /// <summary>取得该任务的计数桶及下一个运行序号。</summary>
    private static (long RunId, RunCounter? Counter) BeginRun(string? flowName)
    {
        if (!FlowLogRoute.TryCreate(Process.Value, flowName, out var route))
            return (0, null);

        lock (_runLock)
        {
            if (!_runCounters.TryGetValue(route, out var counter))
            {
                counter = new RunCounter();
                _runCounters.Add(route, counter);
            }

            counter.Value++;
            return (counter.Value, counter);
        }
    }

    /// <summary>
    /// 释放已关闭或已改名任务的进程级运行序号缓存。
    /// 已经进入执行作用域的任务仍持有自己的计数桶并可安全结束；同一路由随后重新打开时从 #1 开始。
    /// </summary>
    /// <param name="processName">任务所属容器名称；空字符串按无容器处理。</param>
    /// <param name="flowName">任务（标签页）名称。</param>
    /// <returns>找到并释放缓存时为 <see langword="true"/>。</returns>
    public static bool ReleaseRoute(string? processName, string? flowName)
    {
        if (!FlowLogRoute.TryCreate(processName, flowName, out var route))
            return false;

        lock (_runLock)
            return _runCounters.Remove(route);
    }

    /// <summary>
    /// 当前任务运行序号自增 1（环路模式：每沿回边重新调度一次即进入新一轮，
    /// 使循环各轮日志的 #运行序号 可区分）。仅在流程作用域内有效（RunId &gt; 0 时）。
    /// </summary>
    public static void BumpRun()
    {
        if (Run.Value <= 0) return;
        var counter = CurrentRunCounter.Value;
        if (counter == null) return;

        lock (_runLock)
        {
            counter.Value++;
            Run.Value = counter.Value;
        }
    }

    /// <summary>进入节点作用域；Dispose 时还原上一节点名。</summary>
    public static IDisposable EnterNode(string? nodeName)
    {
        var old = Node.Value;
        Node.Value = nodeName;
        return new Scope(() => Node.Value = old);
    }

    /// <summary>临时抑制日志 sink 落盘（供显式分级日志避免经 Console 捕获造成双重写入）。</summary>
    public static IDisposable SuppressSink()
    {
        var old = SinkSuppressed.Value;
        SinkSuppressed.Value = true;
        return new Scope(() => SinkSuppressed.Value = old);
    }

    /// <summary>是否抑制日志 sink 落盘（非信息级日志经此避免 Console 捕获重复落盘）。</summary>
    public static bool IsSinkSuppressed => SinkSuppressed.Value;

    /// <summary>
    /// 日志路由键：流程名\任务名（如 AutoFlow\流程图1）。仅任务名为空时返回 null；
    /// 流程名为空时退化为仅任务名。
    /// </summary>
    public static string? LogKey
    {
        get
        {
            return FlowLogRoute.TryCreate(Process.Value, Flow.Value, out var route)
                ? route.DisplayKey
                : null;
        }
    }

    /// <summary>
    /// 为日志行附加上下文前缀；不在流程作用域内返回 null。
    /// 消息已带 #运行序号 前缀时原样返回（NodeLog / PanelLog 已构造前缀的行不二次加前缀）。
    /// 注：抑制落盘状态不影响本方法——面板展示仍应统一加前缀，仅文件写入由调用方按
    /// <see cref="IsSinkSuppressed"/> 决定。
    /// </summary>
    public static string? Format(string message)
    {
        if (string.IsNullOrEmpty(Flow.Value)) return null;
        if (message.StartsWith("#")) return message;
        var node = Node.Value;
        return string.IsNullOrEmpty(node) ? $"#{Run.Value} {message}" : $"#{Run.Value} [{node}] {message}";
    }

    /// <summary>
    /// 将（未带前缀的）日志行补全上下文后交给当前 <see cref="IFlowLogSink"/>。
    /// 供引擎失败处理等非 NodeLog 路径调用，保证节点裸抛异常也能在流程日志文件中留痕；
    /// 不在流程作用域内或已抑制落盘时静默忽略。
    /// </summary>
    public static void Write(string message, FlowLogLevel level = FlowLogLevel.Information)
    {
        if (SinkSuppressed.Value) return;
        var line = Format(message);
        if (line == null) return;

        try
        {
            Sink.Value?.Write(new FlowLogRecord(
                Process.Value,
                Flow.Value,
                Node.Value,
                Run.Value,
                line,
                level));
        }
        catch (Exception ex)
        {
            // 日志是诊断边界：磁盘、权限或第三方 Sink 故障不应改写工具执行结果。
            Debug.WriteLine($"[FlowLog] 日志落盘失败: {ex.Message}");
        }
    }

    /// <summary>在释放时恢复进入作用域前的异步上下文。</summary>
    private sealed class Scope(Action restore) : IDisposable
    {
        /// <inheritdoc />
        public void Dispose() => restore();
    }

    /// <summary>单一路由的可脱离全局字典继续存活的运行序号桶。</summary>
    private sealed class RunCounter
    {
        /// <summary>该路由最后分配的运行序号。</summary>
        public long Value { get; set; }
    }
}
