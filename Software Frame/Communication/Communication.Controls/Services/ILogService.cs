// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：LogLevel、LogEntry、ILogService、LogService。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Controls、System.Collections.ObjectModel、System.IO、System.Windows、System.Windows.Threading。导入不代表所有类型都会在运行时加载。
using Communication.Controls;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace Communication.Controls.Services;

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

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Info。
    /// </summary>
    void Info(string source, string message);
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Success。
    /// </summary>
    void Success(string source, string message);
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Warning。
    /// </summary>
    void Warning(string source, string message);
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Error。
    /// </summary>
    void Error(string source, string message);
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Rx。
    /// </summary>
    void Rx(string source, string message);
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Tx。
    /// </summary>
    void Tx(string source, string message);
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Clear。
    /// </summary>
    void Clear();
}

/// <summary>默认实现。后台线程调用时投递到 UI 线程，避免跨线程访问集合。</summary>
public sealed class LogService : ILogService
{
    private const int MaxEntries = 2000;
    private Dispatcher? _dispatcher;

    /// <summary>
    /// 初始化 LogService 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 FromThread；具体条件和先后次序见方法体。
    /// </summary>
    public LogService()
    {
        // 构造线程未必是 UI 线程，取不到时由 Dispatch 惰性补一次。
        _dispatcher = Dispatcher.FromThread(Thread.CurrentThread) ?? Application.Current?.Dispatcher;
    }

    public ObservableCollection<LogEntry> Entries { get; } = [];

    public event Action<string, string>? RxReceived;
    public event Action<string, string>? TxReceived;

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Info。
    /// 正文中的关键协作调用包括 Append；具体条件和先后次序见方法体。
    /// </summary>
    public void Info(string source, string message) => Append(LogLevel.Info, source, message);
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Success。
    /// 正文中的关键协作调用包括 Append；具体条件和先后次序见方法体。
    /// </summary>
    public void Success(string source, string message) => Append(LogLevel.Success, source, message);
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Warning。
    /// 正文中的关键协作调用包括 Append；具体条件和先后次序见方法体。
    /// </summary>
    public void Warning(string source, string message) => Append(LogLevel.Warning, source, message);
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Error。
    /// 正文中的关键协作调用包括 Append；具体条件和先后次序见方法体。
    /// </summary>
    public void Error(string source, string message) => Append(LogLevel.Error, source, message);
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Rx。
    /// 正文中的关键协作调用包括 Append；具体条件和先后次序见方法体。
    /// </summary>
    public void Rx(string source, string message)
    {
        RxReceived?.Invoke(source, message);
        Append(LogLevel.Rx, source, message);
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Tx。
    /// 正文中的关键协作调用包括 Append；具体条件和先后次序见方法体。
    /// </summary>
    public void Tx(string source, string message)
    {
        TxReceived?.Invoke(source, message);
        Append(LogLevel.Tx, source, message);
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Clear。
    /// 正文中的关键协作调用包括 Dispatch、Clear；具体条件和先后次序见方法体。
    /// </summary>
    public void Clear() => Dispatch(() => Entries.Clear());

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Append。
    /// 正文中的关键协作调用包括 Dispatch、Add、RemoveAt；具体条件和先后次序见方法体。
    /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
    /// </summary>
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

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Dispatch。
    /// 正文中的关键协作调用包括 action、CheckAccess、BeginInvoke；具体条件和先后次序见方法体。
    /// </summary>
    private void Dispatch(Action action)
    {
        var dispatcher = _dispatcher ?? Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            action();
            return;
        }

        // 首次拿到 UI dispatcher 时固定下来，后续不再每调一次查一次 Application。
        _dispatcher ??= dispatcher;
        if (dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.BeginInvoke(action);
    }
}
