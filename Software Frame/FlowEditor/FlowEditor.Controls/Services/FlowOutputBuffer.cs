// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：FlowOutputBuffer。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、System.Diagnostics、System.Text、FlowEditor.Controls.Interfaces。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using System.Diagnostics;
using System.Text;
using FlowEditor.Controls.Interfaces;

namespace FlowEditor.Controls.Services;

/// <summary>
/// 管理输出栏文本的缓冲、长度限制和刷新节流。
/// 调用方只需追加、刷新或清空，不需要了解锁、计时器和 UI 线程封送细节。
/// </summary>
internal sealed class FlowOutputBuffer
{
    private const int DefaultMaxChars = 200_000;
    private static readonly TimeSpan DefaultFlushInterval = TimeSpan.FromMilliseconds(100);

    private readonly object _gate = new();
    private readonly StringBuilder _builder = new();
    private readonly Action<string> _publish;
    private readonly IUiDispatcher _dispatcher;
    private readonly int _maxChars;
    private readonly TimeSpan _flushInterval;
    private long _lastFlushTimestamp;
    private int _flushScheduled;

    /// <summary>创建输出缓冲模块。</summary>
    /// <param name="publish">接收完整输出文本的发布动作。</param>
    /// <param name="dispatcher">负责将发布动作封送到 UI 线程的 adapter。</param>
    /// <param name="maxChars">缓冲字符上限；超过后丢弃较早的一半。</param>
    /// <param name="flushInterval">输出栏最小刷新间隔。</param>
    public FlowOutputBuffer(
        Action<string> publish,
        IUiDispatcher dispatcher,
        int maxChars = DefaultMaxChars,
        TimeSpan? flushInterval = null)
    {
        _publish = publish ?? throw new ArgumentNullException(nameof(publish));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        if (maxChars <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxChars), "缓冲字符上限必须大于零。");

        _maxChars = maxChars;
        _flushInterval = flushInterval ?? DefaultFlushInterval;
        if (_flushInterval < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(flushInterval), "刷新间隔不能为负数。");
    }

    /// <summary>追加一条带时间前缀的日志，并在节流窗口允许时发布。</summary>
    /// <param name="message">日志正文。</param>
    public void Append(string message)
    {
        bool shouldFlush;
        lock (_gate)
        {
            _builder.AppendLine($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
            if (_builder.Length > _maxChars)
                _builder.Remove(0, _builder.Length / 2);

            shouldFlush = Stopwatch.GetElapsedTime(_lastFlushTimestamp) >= _flushInterval;
            if (shouldFlush)
                _lastFlushTimestamp = Stopwatch.GetTimestamp();
        }

        if (shouldFlush)
            Flush();
        else
            ScheduleFlush();
    }

    /// <summary>立即将当前完整缓冲发布给输出栏。</summary>
    public void Flush()
    {
        string text;
        lock (_gate)
        {
            text = _builder.ToString();
            _lastFlushTimestamp = Stopwatch.GetTimestamp();
        }

        Publish(text);
    }

    /// <summary>清空缓冲并立即清空输出栏。</summary>
    public void Clear()
    {
        lock (_gate)
            _builder.Clear();

        Publish(string.Empty);
    }

    /// <summary>保证节流窗口内只有一个延迟刷新任务。</summary>
    private void ScheduleFlush()
    {
        if (Interlocked.Exchange(ref _flushScheduled, 1) != 0)
            return;

        _ = FlushAfterDelayAsync();
    }

    /// <summary>在节流窗口结束后发布最后一批日志。</summary>
    private async Task FlushAfterDelayAsync()
    {
        await Task.Delay(_flushInterval).ConfigureAwait(false);
        Interlocked.Exchange(ref _flushScheduled, 0);
        Flush();
    }

    /// <summary>通过 UI 调度 seam 发布文本。</summary>
    /// <param name="text">输出栏的完整文本。</param>
    private void Publish(string text)
        => _dispatcher.Invoke(() => _publish(text));
}
