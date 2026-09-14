using System.Collections.Concurrent;

namespace FlowEditor.Core;

/// <summary>
/// 调试控制器（P4）：以 TaskCompletionSource 门闩实现断点/单步挂起，
/// 取代旧实现的轮询等待（Task.Delay(100) 循环），命中即精确唤醒。
/// - StepMode：每个节点执行前都暂停（单步）
/// - 断点集合：仅命中节点暂停
/// - Continue() 释放当前挂起；WaitIfPausedAsync 支持取消令牌打断
/// 线程安全：可从 UI 线程 Continue，引擎线程等待。
/// </summary>
public sealed class DebugController
{
    private volatile TaskCompletionSource? _gate;
    private readonly object _gateLock = new();
    private readonly ConcurrentDictionary<string, byte> _breakpoints = new();

    /// <summary>单步模式：true 时每个节点执行前暂停。</summary>
    public bool StepMode { get; set; }

    /// <summary>是否处于暂停等待状态。</summary>
    public bool IsPaused => _gate != null;

    /// <summary>命中暂停点时触发（参数：节点 ID）。后台线程触发。</summary>
    public event Action<string>? Paused;

    /// <summary>添加断点。</summary>
    public void AddBreakpoint(string nodeId) => _breakpoints[nodeId] = 1;

    /// <summary>移除断点。</summary>
    public void RemoveBreakpoint(string nodeId) => _breakpoints.TryRemove(nodeId, out _);

    /// <summary>清空全部断点。</summary>
    public void ClearBreakpoints() => _breakpoints.Clear();

    /// <summary>当前断点节点 ID 快照。</summary>
    public IReadOnlyCollection<string> Breakpoints => _breakpoints.Keys.ToList();

    /// <summary>继续执行（释放当前挂起的门闩）。</summary>
    public void Continue()
    {
        TaskCompletionSource? gate;
        lock (_gateLock)
        {
            gate = _gate;
            _gate = null;
        }
        gate?.TrySetResult();
    }

    /// <summary>
    /// 调试门：若该节点是断点或处于单步模式则挂起，直到 Continue() 或取消。
    /// </summary>
    public async Task WaitIfPausedAsync(string nodeId, CancellationToken ct)
    {
        var shouldPause = StepMode || _breakpoints.ContainsKey(nodeId);
        if (!shouldPause)
            return;

        TaskCompletionSource gate;
        lock (_gateLock)
        {
            // 上一个门尚未释放（异常路径残留）时直接复用
            gate = _gate ??= CreateGate();
        }

        Paused?.Invoke(nodeId);
        await gate.Task.WaitAsync(ct).ConfigureAwait(false);
    }

    private static TaskCompletionSource CreateGate()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
