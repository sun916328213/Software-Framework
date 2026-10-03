// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
// 本文件定义：NLogHelper。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Runtime、DataManager.Core、DataManager.Core.Log、DataManager.Runtime.Log、DataManager.Core.Log.Definition、NLog。导入不代表所有类型都会在运行时加载。
using DataManager.Runtime;
using DataManager.Core;
using DataManager.Core.Log;
using DataManager.Runtime.Log;
using DataManager.Core.Log.Definition;
using NLog;
using NLog.Config;
using NLog.Targets;
using NLogLogger = global::NLog.Logger;
using NLogLogFactory = global::NLog.LogFactory;
using NLogLogLevel = global::NLog.LogLevel;

namespace DataManager.Runtime.Log.NLog
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

        /// <summary>
        /// 初始化 NLogHelper 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public NLogHelper()
        {
        }

        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetOrCreateLogger。
        /// 正文中的关键协作调用包括 IsNullOrWhiteSpace、CreateDirectory、Combine、AddTarget、AddRuleForAllLevels；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </summary>
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

        /// <summary>
        /// 执行资源释放步骤；对应入口为 DisposeLoggerLocked。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        private void DisposeLoggerLocked()
        {
            try { _factory?.Flush(); } catch { }
            try { _factory?.Shutdown(); } catch { }
            _logger = null;
            _factory = null;
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
        private static global::NLog.LogLevel MapLevel(ELogLevel level) => level switch
        {
            ELogLevel.Information => global::NLog.LogLevel.Info,
            ELogLevel.Warning => global::NLog.LogLevel.Warn,
            ELogLevel.Reset => global::NLog.LogLevel.Warn,
            ELogLevel.Error => global::NLog.LogLevel.Error,
            ELogLevel.SysError => global::NLog.LogLevel.Fatal,
            _ => global::NLog.LogLevel.Info,
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
            logger.Log(MapLevel(logLevel), line);
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
