using DataManager.Log.Definition;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository;
using log4net.Repository.Hierarchy;
using L4NLogManager = global::log4net.LogManager;
using L4NILog = global::log4net.ILog;

namespace DataManager.Log.Log4Net
{
    /// <summary>
    /// log4net 文件实现（与 SerilogHelper 同一文件格式，可复用 LogBase 查询）。
    /// 行格式：yyyy/MM/dd HH:mm:ss {Separator} {ELogLevel} {Separator} {内容}，按天滚动为 logyyyyMMdd。
    /// 每个实例独占 ILoggerRepository，Dispose 时 Shutdown 释放句柄；MinimalLock 保证查询可共享读。
    /// </summary>
    public class Log4NetHelper : LogBase, IDisposable
    {
        private readonly object _lock = new();
        private ILoggerRepository? _repository;
        private L4NILog? _logger;
        private bool _disposed;
        private LogParam _param = new();

        public override LogParam Param
        {
            get => _param;
            set
            {
                lock (_lock)
                {
                    _param = value;
                    DisposeLoggerLocked();
                }
            }
        }

        public Log4NetHelper()
        {
        }

        private L4NILog GetOrCreateLogger()
        {
            lock (_lock)
            {
                if (_logger != null) return _logger;

                var dir = string.IsNullOrWhiteSpace(_param.FilePath)
                    ? AppDomain.CurrentDomain.BaseDirectory
                    : _param.FilePath;
                Directory.CreateDirectory(dir);

                _repository = L4NLogManager.CreateRepository(Guid.NewGuid().ToString("N"));
                var hierarchy = (Hierarchy)_repository;
                hierarchy.Root.Level = Level.All;

                var appender = new RollingFileAppender
                {
                    Name = "dm-file",
                    // 与 Serilog 同名：File=log + DatePattern=yyyyMMdd → log20260907
                    File = Path.Combine(dir, "log"),
                    StaticLogFileName = false,
                    DatePattern = "yyyyMMdd",
                    RollingStyle = RollingFileAppender.RollingMode.Date,
                    AppendToFile = true,
                    MaxSizeRollBackups = _param.SaveDays <= 0 ? 180 : _param.SaveDays,
                    LockingModel = new FileAppender.MinimalLock(),
                    Layout = new PatternLayout("%message%newline"),
                };
                appender.ActivateOptions();
                hierarchy.Root.AddAppender(appender);
                hierarchy.Configured = true;

                _logger = L4NLogManager.GetLogger(_repository.Name, "DataManager");
                return _logger;
            }
        }

        private void DisposeLoggerLocked()
        {
            try { _repository?.Shutdown(); } catch { }
            _logger = null;
            _repository = null;
        }

        public void Connect(ELogLevel logLevel) => GetOrCreateLogger();

        public void DisConnect()
        {
            lock (_lock)
            {
                DisposeLoggerLocked();
            }
        }

        private static Level MapLevel(ELogLevel level) => level switch
        {
            ELogLevel.Information => Level.Info,
            ELogLevel.Warning => Level.Warn,
            ELogLevel.Reset => Level.Warn,
            ELogLevel.Error => Level.Error,
            ELogLevel.SysError => Level.Fatal,
            _ => Level.Info,
        };

        public override void Add(string info, ELogLevel logLevel = ELogLevel.Information)
        {
            var logger = GetOrCreateLogger();
            var sep = _param.Separator ?? "||";
            var line = $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} {sep} {logLevel} {sep} {info}";
            logger.Logger.Log(typeof(Log4NetHelper), MapLevel(logLevel), line, null);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            lock (_lock)
            {
                DisposeLoggerLocked();
            }
            GC.SuppressFinalize(this);
        }
    }
}
