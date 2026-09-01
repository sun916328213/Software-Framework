namespace FlowEditor.Core;

/// <summary>
/// 流程/节点执行作用域（AsyncLocal）：记录当前流程名、节点名与运行序号，
/// 供节点日志按「流程 + 任务 + 运行次数」路由；同时承载落盘 Sink 委托（由宿主注册，
/// 写入 DataManager 的 ILog）。本类不依赖任何具体日志库，仅承载上下文与委托。
/// </summary>
public static class FlowLogContext
{
    private static readonly AsyncLocal<string?> Process = new();
    private static readonly AsyncLocal<string?> Flow = new();
    private static readonly AsyncLocal<string?> Node = new();
    private static readonly AsyncLocal<long> Run = new();
    private static readonly AsyncLocal<bool> SuppressSink = new();
    private static long _runCounter;

    /// <summary>当前流程名（AutoFlow / InitializeFlow ...，容器级；为任务日志的上级目录）。</summary>
    public static string? ProcessName => Process.Value;

    /// <summary>当前任务名（标签页名，AddNewTab 已保证唯一，如 流程图1）。</summary>
    public static string? FlowName => Flow.Value;

    /// <summary>当前节点名（节点执行期间非 null）。</summary>
    public static string? NodeName => Node.Value;

    /// <summary>当前流程运行序号（每次进入流程作用域自增，用于区分同一流程的多次运行）。</summary>
    public static long RunId => Run.Value;

    /// <summary>落盘 Sink：由宿主注册，把（已带上下文前缀的）日志行写入 DataManager 的 ILog；null = 不落盘。</summary>
    public static Action<string>? FileSink { get; set; }

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
        Flow.Value = flowName;
        Run.Value = Interlocked.Increment(ref _runCounter);
        return new Scope(() =>
        {
            Flow.Value = oldFlow;
            Run.Value = oldRun;
        });
    }

    /// <summary>进入节点作用域；Dispose 时还原上一节点名。</summary>
    public static IDisposable EnterNode(string? nodeName)
    {
        var old = Node.Value;
        Node.Value = nodeName;
        return new Scope(() => Node.Value = old);
    }

    /// <summary>临时抑制 FileSink 落盘（供 NodeLog 避免经 Console 捕获造成双重写入）。</summary>
    public static IDisposable SuppressFileSink()
    {
        var old = SuppressSink.Value;
        SuppressSink.Value = true;
        return new Scope(() => SuppressSink.Value = old);
    }

    /// <summary>
    /// 日志路由键：流程名\任务名（如 AutoFlow\流程图1）。仅任务名为空时返回 null；
    /// 流程名为空时退化为仅任务名。
    /// </summary>
    public static string? LogKey
    {
        get
        {
            var task = Flow.Value;
            if (string.IsNullOrEmpty(task)) return null;
            var proc = Process.Value;
            return string.IsNullOrEmpty(proc) ? task : $"{proc}\\{task}";
        }
    }

    /// <summary>为日志行附加上下文前缀；不在流程作用域内或已抑制落盘时返回 null。</summary>
    public static string? Format(string message)
    {
        if (SuppressSink.Value) return null;
        if (string.IsNullOrEmpty(Flow.Value)) return null;
        var node = Node.Value;
        return string.IsNullOrEmpty(node) ? $"#{Run.Value} {message}" : $"#{Run.Value} [{node}] {message}";
    }

    private sealed class Scope(Action restore) : IDisposable
    {
        public void Dispose() => restore();
    }
}
