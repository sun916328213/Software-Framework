// 【中文阅读说明】
// ScriptEditor 模块：脚本持久化模型、运行时编译执行和编辑器协作；脚本 ID、引用及断点属于需要保留的状态。
// 本文件定义：BreakpointController、PauseState。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：ScriptEditor.Controls、ScriptEditor.Runtime.Services、System.Collections.Concurrent、System.Threading、ScriptEditor.Controls.Models、ScriptEditor.Core.Models。导入不代表所有类型都会在运行时加载。
using ScriptEditor.Controls;
using ScriptEditor.Runtime.Services;
using System.Collections.Concurrent;
using System.Threading;
using ScriptEditor.Controls.Models;
using ScriptEditor.Core.Models;

namespace ScriptEditor.Controls.Services;

/// <summary>
/// 应用内断点控制器：脚本执行到断点行时阻塞执行线程，并通知 UI 暂停，
/// 等待用户点击"继续"后恢复。不依赖外部调试器
/// （<see cref="System.Diagnostics.Debugger.Break"/> 在无调试器时无效：.NET 5+ 静默无操作，
/// .NET Framework 会崩溃或弹出 JIT 调试器，均无法满足脚本编辑器内暂停调试的需求）。
/// </summary>
/// <remarks>
/// <para>每个断点命中在独立线程上阻塞（通过 ManualResetEventSlim.Wait），互不影响；
/// 并行"全部执行"时多个脚本可同时暂停，<see cref="Resume"/> 恢复栈顶（最近一次）暂停的脚本。</para>
/// <para>脚本项通过 AsyncLocal&lt;ScriptItem&gt; 流入脚本执行线程，使 UI 知道当前暂停的是哪个脚本/行。</para>
/// </remarks>
public static class BreakpointController
{
    /// <summary>
    /// PauseState 的类型定义。ScriptEditor 模块：脚本持久化模型、运行时编译执行和编辑器协作；脚本 ID、引用及断点属于需要保留的状态。
    /// </summary>
    private sealed class PauseState
    {
        public ManualResetEventSlim Handle = new(false);
        public int Line;
        public ScriptItem? Script;
    }

    private static readonly ConcurrentStack<PauseState> _pauses = new();

    /// <summary>当前执行线程关联的脚本项（通过 AsyncLocal 流入脚本执行线程）。</summary>
    public static readonly AsyncLocal<ScriptItem?> ScriptContext = new();

    /// <summary>是否有脚本暂停在断点。</summary>
    public static bool IsPaused => !_pauses.IsEmpty;

    /// <summary>最近一次（栈顶）暂停的行号（1-based）。</summary>
    public static int CurrentLine => _pauses.TryPeek(out var s) ? s.Line : 0;

    /// <summary>最近一次（栈顶）暂停的脚本项。</summary>
    public static ScriptItem? CurrentScript => _pauses.TryPeek(out var s) ? s.Script : null;

    /// <summary>断点状态变化（命中或恢复）时触发，UI 订阅以刷新暂停指示与高亮。</summary>
    public static event Action? PausedStateChanged;

    /// <summary>
    /// 断点命中：阻塞当前执行线程，直到 <see cref="Resume"/> 或 <see cref="ResumeAll"/> 被调用。
    /// 由插桩后的脚本代码调用（脚本执行线程）。
    /// </summary>
    public static void Hit(int line)
    {
        var state = new PauseState
        {
            Line = line,
            Script = ScriptContext.Value
        };
        _pauses.Push(state);
        PausedStateChanged?.Invoke();
        state.Handle.Wait();
        // 恢复后由 Resume/ResumeAll 负责 Pop 与 Dispose
    }

    /// <summary>恢复栈顶（最近一次）暂停的脚本执行。</summary>
    public static void Resume()
    {
        if (_pauses.TryPop(out var state))
        {
            state.Handle.Set();
            state.Handle.Dispose();
            PausedStateChanged?.Invoke();
        }
    }

    /// <summary>恢复全部暂停的脚本执行（终止/停止时调用，避免线程永久阻塞）。</summary>
    public static void ResumeAll()
    {
        while (_pauses.TryPop(out var state))
        {
            state.Handle.Set();
            state.Handle.Dispose();
        }
        PausedStateChanged?.Invoke();
    }
}
