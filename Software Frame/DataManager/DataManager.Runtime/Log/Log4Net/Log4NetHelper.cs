// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
// 本文件定义：Log4NetHelper。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Runtime、DataManager.Core、DataManager.Core.Log、DataManager.Runtime.Log、DataManager.Core.Log.Definition、log4net.Appender。导入不代表所有类型都会在运行时加载。
using DataManager.Runtime;
using DataManager.Core;
using DataManager.Core.Log;
using DataManager.Runtime.Log;
using DataManager.Core.Log.Definition;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository;
using log4net.Repository.Hierarchy;
using L4NLogManager = global::log4net.LogManager;
using L4NILog = global::log4net.ILog;

namespace DataManager.Runtime.Log.Log4Net
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

        /// <summary>
        /// 初始化 Log4NetHelper 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public Log4NetHelper()
        {
        }

        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetOrCreateLogger。
        /// 正文中的关键协作调用包括 IsNullOrWhiteSpace、CreateDirectory、CreateRepository、ToString、NewGuid；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </summary>
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

        /// <summary>
        /// 执行资源释放步骤；对应入口为 DisposeLoggerLocked。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        private void DisposeLoggerLocked()
        {
            try { _repository?.Shutdown(); } catch { }
            _logger = null;
            _repository = null;
        }

        /// <summary>
        /// 发起连接操作；对应入口为 Connect。
        /// 正文中的关键协作调用包括 GetOrCreateLogger；具体条件和先后次序见方法体。
        /// </summary>
        public void Connect(ELogLevel logLevel) => GetOrCreateLogger();

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 DisConnect。
        /// 正文中的关键协作调用包括 DisposeLoggerLocked；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </summary>
        public void DisConnect()
        {
            lock (_lock)
            {
                DisposeLoggerLocked();
            }
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 MapLevel。
        /// </summary>
        private static Level MapLevel(ELogLevel level) => level switch
        {
            ELogLevel.Information => Level.Info,
            ELogLevel.Warning => Level.Warn,
            ELogLevel.Reset => Level.Warn,
            ELogLevel.Error => Level.Error,
            ELogLevel.SysError => Level.Fatal,
            _ => Level.Info,
        };

        /// <summary>
        /// 执行添加操作；对应入口为 Add。
        /// 正文中的关键协作调用包括 GetOrCreateLogger、Log、MapLevel；具体条件和先后次序见方法体。
        /// </summary>
        public override void Add(string info, ELogLevel logLevel = ELogLevel.Information)
        {
            var logger = GetOrCreateLogger();
            var sep = _param.Separator ?? "||";
            var line = $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} {sep} {logLevel} {sep} {info}";
            logger.Logger.Log(typeof(Log4NetHelper), MapLevel(logLevel), line, null);
        }

        /// <summary>
        /// 执行资源释放步骤；对应入口为 Dispose。
        /// 正文中的关键协作调用包括 DisposeLoggerLocked、SuppressFinalize；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </summary>
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
