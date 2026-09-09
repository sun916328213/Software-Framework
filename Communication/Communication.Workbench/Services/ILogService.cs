using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace Communication.Workbench.Services;

/// <summary>日志级别</summary>
public enum LogLevel
{
    Info,
    Success,
    Warning,
    Error,
    Rx,
    Tx,
}

/// <summary>一条日志记录</summary>
public record LogEntry(DateTime Time, LogLevel Level, string Source, string Message)
{
    public string TimeText => Time.ToString("HH:mm:ss.fff");

    public string LevelText => Level switch
    {
        LogLevel.Info => "INFO",
        LogLevel.Success => " OK ",
        LogLevel.Warning => "WARN",
        LogLevel.Error => "FAIL",
        LogLevel.Rx => " RX ",
        LogLevel.Tx => " TX ",
        _ => "INFO",
    };
}

/// <summary>应用日志服务：线程安全收集，容量有界（超出截断最旧记录）。</summary>
public interface ILogService
{
    ObservableCollection<LogEntry> Entries { get; }

    /// <summary>收到数据帧时触发（源设备，十六进制文本）。</summary>
    event Action<string, string>? RxReceived;

    /// <summary>发出数据帧时触发（源设备，十六进制文本）。</summary>
    event Action<string, string>? TxReceived;

    void Info(string source, string message);
    void Success(string source, string message);
    void Warning(string source, string message);
    void Error(string source, string message);
    void Rx(string source, string message);
    void Tx(string source, string message);
    void Clear();
}

/// <summary>默认实现。后台线程调用时投递到 UI 线程，避免跨线程访问集合。</summary>
public sealed class LogService : ILogService
{
    private const int MaxEntries = 2000;
    private readonly Dispatcher? _dispatcher;

    public LogService()
    {
        // 构造发生在 UI 线程（由 DI 容器在 Shell 创建前解析）。
        _dispatcher = Dispatcher.FromThread(Thread.CurrentThread) ?? Application.Current?.Dispatcher;
    }

    public ObservableCollection<LogEntry> Entries { get; } = [];

    public event Action<string, string>? RxReceived;
    public event Action<string, string>? TxReceived;

    public void Info(string source, string message) => Append(LogLevel.Info, source, message);
    public void Success(string source, string message) => Append(LogLevel.Success, source, message);
    public void Warning(string source, string message) => Append(LogLevel.Warning, source, message);
    public void Error(string source, string message) => Append(LogLevel.Error, source, message);
    public void Rx(string source, string message)
    {
        RxReceived?.Invoke(source, message);
        Append(LogLevel.Rx, source, message);
    }

    public void Tx(string source, string message)
    {
        TxReceived?.Invoke(source, message);
        Append(LogLevel.Tx, source, message);
    }

    public void Clear() => Dispatch(() => Entries.Clear());

    private void Append(LogLevel level, string source, string message)
    {
        var entry = new LogEntry(DateTime.Now, level, source, message);
        Dispatch(() =>
        {
            lock (Entries)
            {
                Entries.Add(entry);
                while (Entries.Count > MaxEntries)
                {
                    Entries.RemoveAt(0);
                }
            }
        });
    }

    private void Dispatch(Action action)
    {
        if (_dispatcher is null || _dispatcher.CheckAccess())
        {
            action();
            return;
        }

        _dispatcher.BeginInvoke(action);
    }
}
