using System.Collections.Concurrent;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Scripting;
using RoslynPad.Roslyn;
using ScriptEditor.Contracts;
using ScriptEditor.Helpers;
using ScriptEditor.Models;

namespace ScriptEditor.Services;

/// <summary>
/// 一次脚本运行/编辑会话：脚本运行状态的**唯一事实源**。
/// </summary>
/// <remarks>
/// <para>职责：脚本编译缓存、单次执行、循环执行、终止、预编译，以及对应的运行状态
/// （<see cref="IsRunning"/> / <see cref="IsLooping"/> / <see cref="Output"/> /
/// <see cref="StatusText"/> / <see cref="ExecutionTime"/> / <see cref="CompilationState"/>）、
/// 取消令牌（执行 + 循环）与已解析引用缓存。</para>
/// <para>UI-free：不引用任何 WPF 控件，不创建 DispatcherTimer，不绑定线程。
/// 状态变更通过 <see cref="PropertyChanged"/> / <see cref="StateChanged"/> 广播，
/// 由调用方（编辑器服务 / 视图）负责封送到 UI 线程。</para>
/// <para>会话与脚本数据的关联由 <see cref="RoslynScriptRuntime"/> 维护：
/// 会话通过构造函数注入的委托**实时读取**脚本数据（代码 / 引用 / 断点），
/// 因此脚本数据对象不需要保存任何运行状态，也不持有本会话实例。</para>
/// <para>与旧 ScriptEngine 的执行行为一致（Inputs 注入、断点插桩、
/// EnsureScriptRunInvoked、编译缓存等均已保留）。</para>
/// </remarks>
public sealed class ScriptSession : ObservableObject, IScriptSession
{
    #region 事件

    /// <summary>IsRunning / IsLooping / StatusText / CompilationState 等状态变化时触发。</summary>
    public event Action? StateChanged;

    private void NotifyStateChanged()
    {
        StateChanged?.Invoke();
    }

    #endregion

    // ════════════════════════════════════════════════════════════════
    //  注入依赖（纯后端，无 UI）
    // ════════════════════════════════════════════════════════════════

    private readonly CustomRoslynHost _host = ScriptHostProvider.Shared;

    /// <summary>获取当前脚本源代码。</summary>
    private readonly Func<string> _getCode;

    /// <summary>获取执行时额外注入的用户引用描述（由会话解析为元数据引用并缓存）。</summary>
    private readonly Func<IReadOnlyList<ScriptReference>> _getReferences;

    /// <summary>获取断点行号集合（1-based）。</summary>
    private readonly Func<IEnumerable<int>> _getBreakpoints;

    /// <summary>获取会话显示名（脚本标题）；未提供时使用 <see cref="Title"/> 的静态值。</summary>
    private readonly Func<string>? _getTitle;

    public ScriptSession(
        Func<string> getCode,
        Func<IReadOnlyList<ScriptReference>>? getReferences = null,
        Func<IEnumerable<int>>? getBreakpoints = null,
        Func<string>? getTitle = null)
    {
        _getCode = getCode ?? (() => string.Empty);
        _getReferences = getReferences ?? (() => Array.Empty<ScriptReference>());
        _getBreakpoints = getBreakpoints ?? (() => Array.Empty<int>());
        _getTitle = getTitle;
    }

    /// <summary>宿主实例（供调试/诊断外部使用）。</summary>
    public CustomRoslynHost Host => _host;

    // ════════════════════════════════════════════════════════════════
    //  会话标识
    // ════════════════════════════════════════════════════════════════

    /// <summary>会话对应的脚本永久标识（独立会话为运行时分配的新标识）。</summary>
    public Guid ScriptId { get; init; } = Guid.NewGuid();

    private string _title = "脚本";
    /// <summary>会话显示名（脚本标题）。</summary>
    public string Title
    {
        get => _getTitle?.Invoke() ?? _title;
        set => SetProperty(ref _title, value);
    }

    // ════════════════════════════════════════════════════════════════
    //  运行时状态（INPC）
    // ════════════════════════════════════════════════════════════════

    private string _output = string.Empty;
    /// <summary>输出面板文本（自动截断超长内容，保留最多 200 行）。</summary>
    public string Output
    {
        get => _output;
        private set => SetProperty(ref _output, OutputTruncator.Truncate(value));
    }

    private string _statusText = "就绪";
    /// <summary>状态栏文本。</summary>
    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    private bool _isRunning;
    /// <summary>是否正在执行。</summary>
    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (SetProperty(ref _isRunning, value))
                NotifyStateChanged();
        }
    }

    private bool _isLooping;
    /// <summary>是否正在循环执行。</summary>
    public bool IsLooping
    {
        get => _isLooping;
        private set
        {
            if (SetProperty(ref _isLooping, value))
                NotifyStateChanged();
        }
    }

    private int _loopInterval = 200;
    /// <summary>循环执行间隔（毫秒）。</summary>
    public int LoopInterval
    {
        get => _loopInterval;
        set => SetProperty(ref _loopInterval, value);
    }

    private string _executionTime = string.Empty;
    /// <summary>最近一次执行耗时。</summary>
    public string ExecutionTime
    {
        get => _executionTime;
        private set => SetProperty(ref _executionTime, value);
    }

    private ScriptCompilationState _compilationState = ScriptCompilationState.NotCompiled;
    /// <summary>编译阶段状态。</summary>
    public ScriptCompilationState CompilationState
    {
        get => _compilationState;
        private set
        {
            if (SetProperty(ref _compilationState, value))
                NotifyStateChanged();
        }
    }

    /// <summary>最近一次脚本执行的原始返回值（未格式化）。</summary>
    public object? LastResult { get; private set; }

    // ════════════════════════════════════════════════════════════════
    //  引用解析缓存（会话级，避免每次执行重复加载程序集）
    // ════════════════════════════════════════════════════════════════

    private readonly RoslynScriptReferenceResolver _resolver = new();
    private readonly object _referenceGate = new();
    private IReadOnlyList<MetadataReference> _references = Array.Empty<MetadataReference>();
    private string[] _referencePaths = Array.Empty<string>();
    private bool _referencesLoaded;

    /// <summary>解析并缓存用户引用（路径集合变化时重新加载）。</summary>
    private IReadOnlyList<MetadataReference> GetReferences()
    {
        var descriptions = _getReferences();
        var paths = descriptions.Select(reference => reference.Path).ToArray();
        lock (_referenceGate)
        {
            if (!_referencesLoaded || !paths.SequenceEqual(_referencePaths, StringComparer.OrdinalIgnoreCase))
            {
                _references = _resolver.LoadMetadataReferences(descriptions);
                _referencePaths = paths;
                _referencesLoaded = true;
            }
            return _references;
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  执行字段
    // ════════════════════════════════════════════════════════════════

    private readonly object _sync = new();

    private CancellationTokenSource? _executionCts;
    private CancellationTokenSource? _loopCts;
    private int _loopGeneration;
    private readonly object _loopLock = new();
    private int _iterationCount;
    private string? _lastSubmittedCode;
    private Script<object>? _lastCompiledScript;
    private bool _lastHasResult;

    private readonly ConcurrentQueue<string> _outputQueue = new();

    private object? _inputData;

    /// <summary>脚本执行时的输入数据（作为 Inputs 字典注入脚本）。</summary>
    public void SetInputData(object? data)
    {
        _inputData = data;
    }

    /// <summary>编辑器级消息写入输出面板（替换当前输出）。</summary>
    public void SetOutput(string output)
    {
        lock (_sync)
        {
            Output = output;
        }
    }

    /// <summary>编辑器级状态消息写入状态栏。</summary>
    public void SetStatus(string statusText) => StatusText = statusText;

    // ════════════════════════════════════════════════════════════════
    //  输出缓冲
    // ════════════════════════════════════════════════════════════════

    /// <summary>将输出队列中的全部内容冲刷进 <see cref="Output"/>（供调用方定时器或运行结束时调用）。</summary>
    public void FlushOutput()
    {
        _lastFlushTicks = Environment.TickCount64;
        const int MaxFlushChars = 4096;
        var sb = new StringBuilder();
        while (_outputQueue.TryDequeue(out var line))
        {
            sb.Append(line);
            if (sb.Length >= MaxFlushChars) break;
        }
        if (sb.Length > 0)
        {
            lock (_sync)
            {
                Output = Output + sb.ToString();
            }
        }
    }

    /// <summary>上次节流刷出的时间戳（Environment.TickCount64），用于限制 UI 刷新频率。</summary>
    private long _lastFlushTicks;

    /// <summary>两次刷出之间的最小间隔（毫秒）。高频 Console.WriteLine 时
    /// 避免每行都触发整串拼接 + 截断扫描 + 绑定传播 + TextBox 全文重排，
    /// 多脚本并行执行多次后该开销累积导致 UI 卡顿。</summary>
    private const int MinFlushIntervalMs = 40;

    private void ClearOutputQueue()
    {
        while (_outputQueue.TryDequeue(out _)) { }
    }

    private void EnqueueRaw(string line)
    {
        _outputQueue.Enqueue(AddTimestamps(line));
        // 节流刷出：间隔不足时仅入队，由下次输出或运行结束的 FlushOutput 兜底
        var now = Environment.TickCount64;
        if (now - _lastFlushTicks >= MinFlushIntervalMs)
            FlushOutput();
    }

    /// <summary>
    /// 为输出块的每一行附加本地时间戳（[HH:mm:ss.fff]），便于排查执行时序问题
    /// （如循环实际间隔、脚本间输出先后）。空行不打点，保持段落间隔清爽。
    /// </summary>
    private static string AddTimestamps(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        var lines = text.Split('\n');
        var sb = new StringBuilder(text.Length + lines.Length * 15);
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                sb.Append(line);
                continue;
            }
            sb.Append('[')
              .Append(DateTime.Now.ToString("HH:mm:ss.fff"))
              .Append("] ")
              .Append(line);
        }
        return sb.ToString();
    }


    // ════════════════════════════════════════════════════════════════
    //  执行
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 异步执行当前脚本。返回 true 表示执行成功，false 表示出错。
    /// </summary>
    /// <remarks>
    /// 并发安全：编译完成后将 <c>Script</c> 实例捕获到局部变量再执行，
    /// 调用与自己的编译结果强绑定。并行"全部执行"时各会话编译完成先后不定，
    /// 即使共享缓存字段被其他调用交错覆盖，也不会执行到别的脚本的代码。
    /// </remarks>
    public async Task<bool> RunAsync()
    {
        var host = _host;
        if (host == null) return false;

        var isLoopMode = IsLooping;

        if (!isLoopMode)
        {
            StatusText = "执行中...";
            ExecutionTime = string.Empty;
            IsRunning = true;
            Output = string.Empty;
        }

        // globals 的静态类型：固定为 ScriptGlobals，使脚本里可访问 Inputs（dynamic）字段，
        // 任意 _inputData 类型（含 class）通过 dynamic 调度都能正确解析，缓存 key 稳定。
        var globalsType = typeof(ScriptGlobals);

        _executionCts?.Cancel();
        _executionCts?.Dispose();
        _executionCts = new CancellationTokenSource();
        var ct = _executionCts.Token;
        var startTime = DateTime.Now;

        string? finalOutput = null;
        string finalStatus = "就绪";
        var isErrorCase = true; // 默认视为错误，成功时才设为 false

        try
        {
            var rawCode = _getCode();
            if (string.IsNullOrWhiteSpace(rawCode))
            {
                finalOutput = "请输入脚本代码";
                finalStatus = "就绪";
                return false;
            }

            // ScriptGlobals.Inputs 是 dynamic 字段，脚本里直接写 Inputs 即可访问 _inputData。
            // IDictionary 走索引器，任意 class 走 dynamic 成员访问，均由 DLR 调度。
            // 无需源码注入，避免污染用户代码与断点行号。

            // 有断点时进行源码插桩 + 启用调试信息
            var breakpoints = _getBreakpoints().ToList();
            var hasBreakpoints = breakpoints.Count > 0;
            if (hasBreakpoints)
                rawCode = ScriptExecutionService.InstrumentBreakpoints(rawCode, breakpoints);

            // 确保 Script.Run() 被调用
            var code = ScriptExecutionService.EnsureScriptRunInvoked(rawCode);

            // 先快照缓存再比较：并发调用期间字段可能被其他调用覆盖，
            // 全程基于快照判断与执行，避免读到被覆盖后的值
            var cachedScript = _lastCompiledScript;
            var codeChanged = !string.Equals(code, _lastSubmittedCode, StringComparison.Ordinal);

            Script<object> scriptToRun;
            bool hasResultToRun;

            if (codeChanged || cachedScript == null)
            {
                // 重新编译前清理旧脚本类型的静态字段与事件订阅，
                // 防止多次执行后旧脚本持有的引用累积导致内存增长与 UI 卡顿
                ScriptExecutionService.CleanupOldScript();

                var captureHost = host;
                var captureGetRefs = GetReferences;

                CompilationState = ScriptCompilationState.Compiling;

                var (s, hr, diags) = await ScriptExecutionService.CompileAndCheckAsync(
                    code, captureHost, captureGetRefs, hasBreakpoints, globalsType);
                var errors = diags.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

                if (errors.Count > 0)
                {
                    CompilationState = ScriptCompilationState.Failed;
                    finalOutput = string.Join(Environment.NewLine, errors.Select(ScriptExecutionService.FormatObject));
                    finalStatus = "编译失败";
                    return false;
                }

                // 仅在编译成功后更新缓存：失败不污染既有缓存
                //（旧实现先写 _lastSubmittedCode 再 await 编译，
                //  编译失败后相同代码会误命中缓存而执行旧的编译产物）
                _lastSubmittedCode = code;
                _lastCompiledScript = s;
                _lastHasResult = hr;
                CompilationState = ScriptCompilationState.Compiled;

                scriptToRun = s;
                hasResultToRun = hr;
            }
            else
            {
                scriptToRun = cachedScript;
                hasResultToRun = _lastHasResult;
            }

            if (isLoopMode)
            {
                var iter = Interlocked.Increment(ref _iterationCount);
                EnqueueRaw($"\r\n\r\n=== 第 {iter} 次迭代 ===\r\n");
            }

            // 注入策略：直接把 _inputData 作为动态全局传入，运行时由 DLR binder 按实际类型分派——
            // IDictionary 走索引器（Inputs["key"] 旧 API），class 走成员访问（Inputs.Prop 新 API）。
            // null 时给空字典，保证脚本内 Inputs 可安全访问。
            var globalsInput = (object?)_inputData ?? new System.Collections.Generic.Dictionary<string, object?>();

            var (result, hasError, returnValue) = await ScriptExecutionService.RunScriptAsync(
                scriptToRun, hasResultToRun, ct,
                onOutput: EnqueueRaw,
                globals: new ScriptGlobals
                {
                    Inputs = globalsInput
                });

            LastResult = returnValue;

            var elapsed = DateTime.Now - startTime;
            finalStatus = hasError ? "执行失败" : "执行成功";
            ExecutionTime = $"{elapsed.TotalMilliseconds:F2} ms";

            if (isLoopMode)
            {
                EnqueueExtraOutput(result!);
            }
            else
            {
                // 成功路径：不 drain 队列，交给调用方定时器持续 flush
                EnqueueExtraOutput(result!);
                if (!hasError)
                    isErrorCase = false;
            }
        }
        catch (OperationCanceledException)
        {
            ClearOutputQueue();
            CompilationState = ScriptCompilationState.Cancelled;
            finalOutput = "执行已取消";
            finalStatus = "已取消";
        }
        catch (Exception ex)
        {
            ClearOutputQueue();
            finalOutput = $"执行异常: {ex.Message}";
            finalStatus = "执行异常";
        }
        finally
        {
            if (!isLoopMode)
            {
                IsRunning = false;
                if (isErrorCase && finalOutput != null)
                {
                    // 错误/取消/编译失败：替换输出
                    ClearOutputQueue();
                    lock (_sync)
                    {
                        Output = finalOutput;
                    }
                }
                else
                {
                    // 成功：flush 剩余队列，让输出完整
                    FlushOutput();
                }
                StatusText = finalStatus;
            }
            else
            {
                FlushOutput();
            }
        }

        return !isErrorCase;
    }

    /// <summary>
    /// 将 RunScriptAsync 返回结果中的非 Console 部分入队到输出队列（循环模式专用）。
    /// </summary>
    private void EnqueueExtraOutput(string result)
    {
        if (string.IsNullOrEmpty(result)) return;
        const string ConsoleHeader = "=== Console 输出 ===";
        var consoleIdx = result.IndexOf(ConsoleHeader, StringComparison.Ordinal);
        if (consoleIdx < 0)
        {
            EnqueueRaw(result);
            return;
        }
        var nextSection = result.IndexOf("===", consoleIdx + ConsoleHeader.Length, StringComparison.Ordinal);
        if (nextSection < 0) return;
        var extraPart = result.Substring(nextSection);
        if (!string.IsNullOrWhiteSpace(extraPart))
            EnqueueRaw(Environment.NewLine + extraPart);
    }

    /// <summary>终止当前执行。</summary>
    public void Terminate()
    {
        _executionCts?.Cancel();
        IsRunning = false;
        StatusText = "正在终止...";
    }

    /// <summary>异步循环执行当前脚本。</summary>
    public async Task LoopRunAsync()
    {
        int myGeneration;
        CancellationTokenSource localCts;

        lock (_loopLock)
        {
            // 取消旧循环（旧循环的 finally 会自行清理）
            _loopCts?.Cancel();

            localCts = new CancellationTokenSource();
            _loopCts = localCts;
            _loopGeneration++;
            myGeneration = _loopGeneration;
        }

        var ct = localCts.Token;

        IsLooping = true;
        StatusText = "循环执行中...";
        Output = string.Empty;
        _iterationCount = 0;

        IsRunning = true;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(Math.Max(20, LoopInterval), ct);
                if (ct.IsCancellationRequested) break;

                var runTask = RunAsync();
                if (await Task.WhenAny(runTask, Task.Delay(-1, ct)) != runTask)
                    break;
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            bool isCurrentGeneration;
            lock (_loopLock)
            {
                isCurrentGeneration = (_loopGeneration == myGeneration);
                // 只有当前代际才清理 _loopCts 和重置状态，避免覆盖新版循环的状态
                if (isCurrentGeneration)
                {
                    _loopCts?.Dispose();
                    _loopCts = null;
                }
            }

            if (isCurrentGeneration)
            {
                IsRunning = false;
                IsLooping = false;
            }
            StatusText = "循环已停止...";
        }
    }

    /// <summary>停止循环执行。</summary>
    public void StopLoop()
    {
        StatusText = "正在停止循环...";
        lock (_sync)
        {
            _loopCts?.Cancel();
            _executionCts?.Cancel();
        }
        IsLooping = false;
        IsRunning = false;
    }

    /// <summary>耗时文本格式化（编译/执行统一 "xx.xx ms"）。</summary>
    private static string FormatElapsed(TimeSpan elapsed) => $"{elapsed.TotalMilliseconds:F2} ms";

    /// <summary>
    /// 写入输出文本并**强制**广播变更（无论新旧文本是否相同）。
    /// </summary>
    /// <remarks>
    /// 会话在编辑器中可被反复复用（同一标签页多次编译）。若上一次编译留下的结果文本与本次完全相同，
    /// 普通 setter 会因"值未变化"而不广播 PropertyChanged，
    /// 当前标签页的输出面板就停留在切换前的旧内容——表现为"点了编译，输出栏没被覆盖"。
    /// </remarks>
    private void SetOutputForced(string text)
    {
        lock (_sync)
        {
            _output = OutputTruncator.Truncate(text);
        }
        OnPropertyChanged(nameof(Output));
    }

    /// <summary>写入耗时文本并**强制**广播变更（原因同 <see cref="SetOutputForced"/>）。</summary>
    private void SetExecutionTimeForced(string text)
    {
        _executionTime = text;
        OnPropertyChanged(nameof(ExecutionTime));
    }

    /// <summary>仅编译当前脚本，不执行。无输入输出回调。返回是否编译成功。</summary>
    public async Task<bool> CompileAsync()
    {
        var host = _host;
        if (host == null) return false;

        if (IsLooping) StopLoop();

        StatusText = "编译中...";

        // 与 RunAsync 对齐：编译开始前先清空输出与耗时，
        // 保证本次编译结果一定会覆盖输出面板中的旧内容（含上一次编译/执行的残留）。
        SetOutputForced(string.Empty);
        SetExecutionTimeForced(string.Empty);
        CompilationState = ScriptCompilationState.Compiling;

        var startTime = DateTime.Now;

        try
        {
            var rawCode = _getCode();

            // ScriptGlobals.Inputs 已是 dynamic 字段，无需源码注入。

            var invokeCode = ScriptExecutionService.EnsureScriptRunInvoked(rawCode);
            ScriptExecutionService.CleanupOldScript();

            var captureCode = invokeCode;
            var captureHost = host;
            var captureGetRefs = GetReferences;

            var (script, hasResult, diagnostics) = await ScriptExecutionService.CompileAndCheckAsync(
                captureCode, captureHost, captureGetRefs, false, typeof(ScriptGlobals));

            if (!diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
            {
                _lastSubmittedCode = captureCode;
                _lastCompiledScript = script;
                _lastHasResult = hasResult;
                CompilationState = ScriptCompilationState.Compiled;
                SetOutputForced("✓ 编译成功");
                StatusText = "编译完成";
                SetExecutionTimeForced(FormatElapsed(DateTime.Now - startTime));
                return true;
            }

            _lastSubmittedCode = null;
            _lastCompiledScript = null;
            _lastHasResult = false;
            CompilationState = ScriptCompilationState.Failed;

            SetOutputForced(string.Join(Environment.NewLine,
                diagnostics.Select(d => $"{(d.Severity == DiagnosticSeverity.Error ? "✗" : "⚠")} 第{d.Location.GetLineSpan().StartLinePosition.Line + 1}行: {d.GetMessage()}")));
            StatusText = "编译失败";
            SetExecutionTimeForced(FormatElapsed(DateTime.Now - startTime));
            return false;
        }
        catch (Exception ex)
        {
            _lastSubmittedCode = null;
            _lastCompiledScript = null;
            _lastHasResult = false;
            CompilationState = ScriptCompilationState.Failed;
            SetOutputForced($"编译异常: {ex.Message}");
            StatusText = "编译异常";
            SetExecutionTimeForced(FormatElapsed(DateTime.Now - startTime));
            return false;
        }
    }

    /// <summary>预编译脚本（后台静默编译，加速首次执行）。</summary>
    /// <remarks>
    /// 并发安全：仅在编译成功后写缓存字段；与 <see cref="RunAsync"/> 并发时
    /// 即使缓存被交错覆盖，RunAsync 也使用局部变量执行自己的编译结果。
    /// </remarks>
    public async Task PreCompileAsync(string code)
    {
        var host = _host;
        if (host == null || string.IsNullOrWhiteSpace(code)) return;

        // ScriptGlobals.Inputs 已是 dynamic 字段，无需源码注入。

        var invokeCode = ScriptExecutionService.EnsureScriptRunInvoked(code);
        if (string.IsNullOrWhiteSpace(invokeCode)) return;

        try
        {
            var captureCode = invokeCode;
            var captureHost = host;
            var captureGetRefs = GetReferences;

            var (script, hasResult, diagnostics) = await ScriptExecutionService.CompileAndCheckAsync(
                captureCode, captureHost, captureGetRefs, false, typeof(ScriptGlobals));
            if (!diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
            {
                _lastSubmittedCode = captureCode;
                _lastCompiledScript = script;
                _lastHasResult = hasResult;
                CompilationState = ScriptCompilationState.Compiled;
            }
        }
        catch (Exception) { }
    }
}
