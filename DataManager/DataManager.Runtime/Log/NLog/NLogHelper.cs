using DataManager.Log.Definition;
using NLog;
using NLog.Config;
using NLog.Targets;
using NLogLogger = global::NLog.Logger;
using NLogLogFactory = global::NLog.LogFactory;
using NLogLogLevel = global::NLog.LogLevel;

namespace DataManager.Log.NLog
{
    /// <summary>
    /// NLog 文件实现（与 SerilogHelper 同一文件格式，可复用 LogBase 查询）。
    /// 行格式：yyyy/MM/dd HH:mm:ss {Separator} {ELogLevel} {Separator} {内容}，按天滚动为 logyyyyMMdd。
    /// </summary>
    public class NLogHelper : LogBase, IDisposable
    {
        private readonly object _lock = new();
        private LogFactory? _factory;
        private Logger? _logger;
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

        public NLogHelper()
        {
        }

        private Logger GetOrCreateLogger()
        {
            lock (_lock)
            {
                if (_logger != null) return _logger;

                var dir = string.IsNullOrWhiteSpace(_param.FilePath)
                    ? AppDomain.CurrentDomain.BaseDirectory
                    : _param.FilePath;
                Directory.CreateDirectory(dir);

                _factory = new LogFactory();
                var config = new LoggingConfiguration(_factory);

                var file = new FileTarget("dm-file")
                {
                    // 与 Serilog 同名：log + yyyyMMdd（无扩展名），每天自动切新文件
                    FileName = Path.Combine(dir, "log") + "${date:format=yyyyMMdd}",
                    Layout = "${message}",
                    ConcurrentWrites = true,
                    KeepFileOpen = true,
                    ArchiveEvery = FileArchivePeriod.Day,
                    MaxArchiveFiles = _param.SaveDays <= 0 ? 180 : _param.SaveDays,
                };
                config.AddTarget(file);
                config.AddRuleForAllLevels(file);
                _factory.Configuration = config;

                _logger = _factory.GetCurrentClassLogger();
                return _logger;
            }
        }

        private void DisposeLoggerLocked()
        {
            try { _factory?.Flush(); } catch { }
            try { _factory?.Shutdown(); } catch { }
            _logger = null;
            _factory = null;
        }

        public void Connect(ELogLevel logLevel) => GetOrCreateLogger();

        public void DisConnect()
        {
            lock (_lock)
            {
                DisposeLoggerLocked();
            }
        }

        private static global::NLog.LogLevel MapLevel(ELogLevel level) => level switch
        {
            ELogLevel.Information => global::NLog.LogLevel.Info,
            ELogLevel.Warning => global::NLog.LogLevel.Warn,
            ELogLevel.Reset => global::NLog.LogLevel.Warn,
            ELogLevel.Error => global::NLog.LogLevel.Error,
            ELogLevel.SysError => global::NLog.LogLevel.Fatal,
            _ => global::NLog.LogLevel.Info,
        };

        public override void Add(string info, ELogLevel logLevel = ELogLevel.Information)
        {
            var logger = GetOrCreateLogger();
            var sep = _param.Separator ?? "||";
            var line = $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} {sep} {logLevel} {sep} {info}";
            logger.Log(MapLevel(logLevel), line);
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
