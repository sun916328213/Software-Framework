using System.Collections.Concurrent;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Scripting;
using RoslynPad.Roslyn;
using ScriptEditor.Helpers;

namespace ScriptEditor.Services;

/// <summary>
/// 脚本全局对象。Roslyn 脚本里可直接访问 <c>Inputs</c> 字段（dynamic），支持任意类型：
/// <list type="bullet">
///   <item><see cref="System.Collections.Generic.IDictionary{TKey,TValue}"/>：保留旧写法 <c>Inputs["key"]</c></item>
///   <item>任意 class：<c>Inputs.PropertyName</c>、<c>Inputs.Method()</c>，通过 dynamic 调度</item>
/// </list>
/// 静态 globalsType 保证缓存 key 稳定（始终是本类型），不会因运行时 inputData 类型变化而重新编译。
/// </summary>
public sealed class ScriptGlobals
{
    /// <summary>脚本输入参数。支持任意类型：IDictionary 走索引器，class 走 dynamic 成员访问。
    /// 实例形式 Script 的 suffix 注入代码（EnsureScriptRunInvoked）也通过本字段给实例 Inputs 赋值。</summary>
    public dynamic? Inputs { get; set; }
}

/// <summary>
/// 无头脚本执行引擎（前端/后端分离后的后端核心）。
/// </summary>
/// <remarks>
/// <para>职责：脚本编译缓存、单次执行、循环执行、终止、预编译，
/// 以及对应的运行时状态（IsRunning / IsLooping / OutputText / StatusText 等）。</para>
/// <para>UI-free：不引用任何 WPF 控件，不创建 DispatcherTimer，不绑定线程。
/// 状态变更通过 <see cref="PropertyChanged"/> / <see cref="StateChanged"/> 广播，
/// 由调用方（编辑器服务或 ScriptItem）负责封送到 UI 线程。</para>
/// <para>与旧 ScriptEditorService 中的执行逻辑行为一致（Inputs 注入、断点插桩、
/// EnsureScriptRunInvoked、编译缓存等均已迁移至此）。</para>
/// </remarks>
public sealed class ScriptEngine : ObservableObject
{
    #region 事件

    /// <summary>IsRunning / IsLooping / StatusText 等状态变化时触发。</summary>
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

    /// <summary>获取执行时额外注入的元数据引用（宿主程序集 + 用户引用等）。</summary>
    private readonly Func<IReadOnlyList<MetadataReference>> _getAdditionalReferences;

    /// <summary>获取断点行号集合（1-based）。</summary>
    private readonly Func<IEnumerable<int>> _getBreakpoints;

    public ScriptEngine(
        Func<string> getCode,
        Func<IReadOnlyList<MetadataReference>>? getAdditionalReferences = null,
        Func<IEnumerable<int>>? getBreakpoints = null)
    {
        _getCode = getCode ?? (() => string.Empty);
        _getAdditionalReferences = getAdditionalReferences ?? (() => Array.Empty<MetadataReference>());
        _getBreakpoints = getBreakpoints ?? (() => Array.Empty<int>());
    }

    /// <summary>宿主实例（供调试/诊断外部使用）。</summary>
    public CustomRoslynHost Host => _host;

    // ════════════════════════════════════════════════════════════════
    //  运行时状态（INPC）
    // ════════════════════════════════════════════════════════════════

    private string _outputText = string.Empty;
    /// <summary>输出面板文本（自动截断超长内容，保留最多 200 行）。</summary>
    public string OutputText
    {
        get => _outputText;
        private set => SetProperty(ref _outputText, OutputTruncator.Truncate(value));
    }

    private string _statusText = "就绪";
    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    private bool _isRunning;
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
    public string ExecutionTime
    {
        get => _executionTime;
        private set => SetProperty(ref _executionTime, value);
    }

    /// <summary>最近一次脚本执行的原始返回值（未格式化）。</summary>
    public object? LastScriptResult { get; private set; }

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

    // ════════════════════════════════════════════════════════════════
    //  输出缓冲
    // ════════════════════════════════════════════════════════════════

    /// <summary>将输出队列中的全部内容冲刷进 OutputText（供调用方定时器或运行结束时调用）。</summary>
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
                OutputText = OutputText + sb.ToString();
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
    /// 调用与自己的编译结果强绑定。并行"全部执行"时各引擎编译完成先后不定，
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
            OutputText = string.Empty;
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
                var captureGetRefs = _getAdditionalReferences;

                var (s, hr, diags) = await ScriptExecutionService.CompileAndCheckAsync(
                    code, captureHost, captureGetRefs, hasBreakpoints, globalsType);
                var errors = diags.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

                if (errors.Count > 0)
                {
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

            LastScriptResult = returnValue;

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
                        OutputText = finalOutput;
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
        OutputText = string.Empty;
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

    /// <summary>仅编译当前脚本，不执行。无输入输出回调。</summary>
    public async Task CompileAsync()
    {
        var host = _host;
        if (host == null) return;

        if (IsLooping) StopLoop();

        StatusText = "编译中...";

        try
        {
            var rawCode = _getCode();

            // ScriptGlobals.Inputs 已是 dynamic 字段，无需源码注入。

            var invokeCode = ScriptExecutionService.EnsureScriptRunInvoked(rawCode);
            ScriptExecutionService.CleanupOldScript();

            var captureCode = invokeCode;
            var captureHost = host;
            var captureGetRefs = _getAdditionalReferences;

            var (script, hasResult, diagnostics) = await ScriptExecutionService.CompileAndCheckAsync(
                captureCode, captureHost, captureGetRefs, false, typeof(ScriptGlobals));

            if (!diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
            {
                _lastSubmittedCode = captureCode;
                _lastCompiledScript = script;
                _lastHasResult = hasResult;
                lock (_sync)
                {
                    OutputText = "✓ 编译成功";
                }
                StatusText = "编译完成";
                return;
            }

            _lastSubmittedCode = null;
            _lastCompiledScript = null;
            _lastHasResult = false;

            lock (_sync)
            {
                OutputText = string.Join(Environment.NewLine,
                    diagnostics.Select(d => $"{(d.Severity == DiagnosticSeverity.Error ? "✗" : "⚠")} 第{d.Location.GetLineSpan().StartLinePosition.Line + 1}行: {d.GetMessage()}"));
            }
            StatusText = "编译失败";
        }
        catch (Exception ex)
        {
            _lastSubmittedCode = null;
            _lastCompiledScript = null;
            _lastHasResult = false;
            lock (_sync)
            {
                OutputText = $"编译异常: {ex.Message}";
            }
            StatusText = "编译异常";
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
            var captureGetRefs = _getAdditionalReferences;

            var (script, hasResult, diagnostics) = await ScriptExecutionService.CompileAndCheckAsync(
                captureCode, captureHost, captureGetRefs, false, typeof(ScriptGlobals));
            if (!diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
            {
                _lastSubmittedCode = captureCode;
                _lastCompiledScript = script;
                _lastHasResult = hasResult;
            }
        }
        catch (Exception) { }
    }
}