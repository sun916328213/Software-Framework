using System.Windows;
using System.Windows.Threading;
using SecsGem.Core;
using SecsGem.Runtime;

namespace SecsGem.Controls.Terminal;

/// <summary>异步、有界、按序的非模态终端显示；协议接收线程不等待 Dispatcher 或操作员。</summary>
public sealed class WpfTerminalDisplay : ITerminalDisplay, IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly SecsGemRuntime? _runtime;
    private readonly object _gate = new();
    private readonly Dictionary<long, SecsTerminalMessage> _accepted = new();
    private readonly Queue<(long Key, SecsTerminalMessage Message)> _pending = new();
    private MessagePrompt? _window;
    private long _sequence;
    private bool _disposed;

    public WpfTerminalDisplay(SecsGemRuntime? runtime = null)
    {
        _runtime = runtime;
        _dispatcher = Application.Current?.Dispatcher ?? throw new InvalidOperationException("终端显示需要 WPF Application。");
    }

    public void Show(string title, string message) => Show(new SecsTerminalMessage(0, 0, title, message, DateTimeOffset.UtcNow));

    public void Show(SecsTerminalMessage message)
    {
        long key;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_dispatcher.HasShutdownStarted || _accepted.Count >= 64) throw new InvalidOperationException("终端显示已停止或待处理消息已满。");
            key = ++_sequence;
            _accepted.Add(key, message);
        }
        try
        {
            var operation = _dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
            {
                lock (_gate) { if (_disposed) return; }
                _pending.Enqueue((key, message));
                ShowNext();
            }));
            if (operation.Status == DispatcherOperationStatus.Aborted) throw new InvalidOperationException("终端 Dispatcher 已停止。");
        }
        catch { lock (_gate) _accepted.Remove(key); throw; }
    }

    private void ShowNext()
    {
        if (_window != null) return;
        while (_pending.TryDequeue(out var next))
        {
            lock (_gate) { if (_disposed) return; }
            MessagePrompt? window = null;
            EventHandler? confirmed = null;
            EventHandler? closed = null;
            try
            {
                window = new MessagePrompt($"{next.Message.Command} · TID={next.Message.TerminalId}", next.Message.Text, Visibility.Collapsed);
                void Confirmed(object? sender, EventArgs args) => _runtime?.AcknowledgeTerminalMessage(next.Message.Id);
                void Closed(object? sender, EventArgs args)
                {
                    window.Confirmed -= confirmed;
                    window.Closed -= closed;
                    _window = null;
                    lock (_gate) _accepted.Remove(next.Key);
                    ShowNext(); // 关闭一条再显示下一条，不叠加模态窗口。
                }
                confirmed = Confirmed;
                closed = Closed;
                window.Confirmed += confirmed;
                window.Closed += closed;
                _window = window;
                window.Show();
                return;
            }
            catch (Exception ex)
            {
                if (window != null)
                {
                    window.Confirmed -= confirmed;
                    window.Closed -= closed;
                    try { window.Close(); }
                    catch (Exception closeError) { System.Diagnostics.Trace.WriteLine(closeError); }
                }
                _window = null;
                lock (_gate) _accepted.Remove(next.Key);
                _runtime?.ReportTerminalDisplayFailure(next.Message.Id, ex.Message);
            }
        }
    }

    public void Dispose()
    {
        SecsTerminalMessage[] dropped;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            dropped = _accepted.Values.ToArray();
            _accepted.Clear();
        }
        if (_runtime != null && ReferenceEquals(_runtime.TerminalDisplay, this)) _runtime.TerminalDisplay = null;
        foreach (var message in dropped) _runtime?.ReportTerminalDisplayFailure(message.Id, "终端显示服务已关闭。");
        void Close()
        {
            _pending.Clear();
            _window?.Close(); // 不是操作员确认，不置 IsRead。
        }
        if (_dispatcher.CheckAccess()) Close();
        else if (!_dispatcher.HasShutdownStarted) _dispatcher.BeginInvoke(new Action(Close));
    }
}
