using System.Threading.Channels;
using SecsGem.Core;

namespace SecsGem.Runtime;

public enum TerminalDeliveryState { Queued, Dispatched, Failed, Cancelled }

/// <summary>Dispatched 只表示已交给显示服务；IsRead 只由本地操作员确认置位。</summary>
public sealed record TerminalMessageEntry(SecsTerminalMessage Message, TerminalDeliveryState State, bool IsRead = false, string? Error = null);

public partial class SecsGemRuntime
{
    private readonly object _terminalGate = new();
    private readonly Channel<TerminalWork> _terminalQueue = Channel.CreateBounded<TerminalWork>(new BoundedChannelOptions(64)
    { SingleReader = false, FullMode = BoundedChannelFullMode.Wait, AllowSynchronousContinuations = false });
    private readonly CancellationTokenSource _terminalStop = new();
    private readonly List<TerminalMessageEntry> _terminalHistory = new();
    private readonly HashSet<TerminalWork> _terminalPending = new();
    private Task _terminalWorker = Task.CompletedTask;
    private bool _terminalStarted;
    private bool _terminalClosed;
    private long _terminalSequence;

    private sealed record TerminalWork(SecsTerminalMessage Message, ITerminalDisplay Display)
    {
        internal TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>最近 200 条已接受通知的独立状态快照，非持久化审计日志；不暴露可编辑集合。</summary>
    public IReadOnlyList<TerminalMessageEntry> GetTerminalMessageHistory()
    { lock (_terminalGate) return _terminalHistory.ToArray(); }

    /// <summary>本地操作员显式确认阅读；不发送第二个 S10 ACK，也不等同远程命令。</summary>
    public bool AcknowledgeTerminalMessage(long id)
    {
        lock (_terminalGate)
        {
            if (_terminalClosed) return false;
            var index = _terminalHistory.FindIndex(entry => entry.Message.Id == id);
            if (index < 0 || _terminalHistory[index].State is TerminalDeliveryState.Failed or TerminalDeliveryState.Cancelled) return false;
            _terminalHistory[index] = _terminalHistory[index] with { IsRead = true };
            return true;
        }
    }

    /// <summary>显示服务无法完成已接受通知时保留失败记录，不追发或更改已返回的 ACK。</summary>
    public void ReportTerminalDisplayFailure(long id, string reason) => SetTerminalState(id, TerminalDeliveryState.Failed, reason);

    internal byte EnqueueTerminalMessage(byte tid, string command, string text)
    {
        var display = TerminalDisplay;
        lock (_terminalGate)
        {
            if (_terminalClosed || _disposed || display is null) return 1; // ACKC10=1：不接受显示。
            var message = new SecsTerminalMessage(++_terminalSequence, tid, command, text, DateTimeOffset.UtcNow);
            var work = new TerminalWork(message, display);
            if (!_terminalQueue.Writer.TryWrite(work)) return 1; // 不阻塞协议线程，不静默丢弃。
            _terminalHistory.Add(new TerminalMessageEntry(message, TerminalDeliveryState.Queued));
            if (_terminalHistory.Count > 200) _terminalHistory.RemoveAt(0);
            _terminalPending.Add(work);
            if (!_terminalStarted)
            {
                _terminalStarted = true;
                _terminalWorker = Task.Run(DeliverTerminalMessagesAsync);
            }
            return 0; // 只确认成功入队，不表示操作员阅读或设备动作完成。
        }
    }

    private async Task DeliverTerminalMessagesAsync()
    {
        try
        {
            await foreach (var work in _terminalQueue.Reader.ReadAllAsync(_terminalStop.Token).ConfigureAwait(false))
            {
                try
                {
                    lock (_terminalGate) { if (_terminalClosed || _disposed) continue; }
                    work.Display.Show(work.Message); // 不持锁调用宿主；旧显示服务即使阻塞也不阻塞 S10 ACK。
                    SetTerminalState(work.Message.Id, TerminalDeliveryState.Dispatched);
                }
                catch (Exception ex)
                {
                    SetTerminalState(work.Message.Id, TerminalDeliveryState.Failed, ex.Message);
                    try { if (!_disposed) RaiseErrorPublic($"{work.Message.Command} TID={work.Message.TerminalId} 已接收但显示失败：{ex.Message}"); }
                    catch (Exception logError) { System.Diagnostics.Trace.WriteLine(logError); }
                }
                finally
                {
                    lock (_terminalGate) _terminalPending.Remove(work);
                    work.Completion.TrySetResult();
                }
            }
        }
        catch (OperationCanceledException) when (_terminalStop.IsCancellationRequested) { }
        finally { lock (_terminalGate) _terminalStop.Dispose(); }
    }

    private void SetTerminalState(long id, TerminalDeliveryState state, string? error = null)
    {
        lock (_terminalGate)
        {
            if (_terminalClosed) return;
            var index = _terminalHistory.FindIndex(entry => entry.Message.Id == id);
            if (index < 0) return;
            // UI 可能在 Show 返回前报告失败/确认，不用后到的 Dispatched 覆盖失败状态。
            if (state == TerminalDeliveryState.Dispatched && _terminalHistory[index].State != TerminalDeliveryState.Queued) return;
            _terminalHistory[index] = _terminalHistory[index] with { State = state, Error = error };
        }
    }

    internal Task TerminalWorkerCompletion { get { lock (_terminalGate) return _terminalWorker; } }

    internal Task DrainTerminalMessagesAsync()
    { lock (_terminalGate) return Task.WhenAll(_terminalPending.Select(work => work.Completion.Task)); }

    private void StopTerminalMessages()
    {
        lock (_terminalGate)
        {
            if (_terminalClosed) return;
            _terminalClosed = true;
            _terminalQueue.Writer.TryComplete();
            _terminalStop.Cancel();
            for (var index = 0; index < _terminalHistory.Count; index++)
                if (_terminalHistory[index].State == TerminalDeliveryState.Queued)
                    _terminalHistory[index] = _terminalHistory[index] with { State = TerminalDeliveryState.Cancelled, Error = "Runtime 已释放，停止分发。" };
            while (_terminalQueue.Reader.TryRead(out var work))
            {
                _terminalPending.Remove(work);
                work.Completion.TrySetResult();
            }
            if (!_terminalStarted) _terminalStop.Dispose();
        }
    }
}
