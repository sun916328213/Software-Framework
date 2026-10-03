// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
// 本文件定义：SerilogHelper。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Runtime、DataManager.Core、DataManager.Core.Log、DataManager.Runtime.Log、Serilog、System。导入不代表所有类型都会在运行时加载。
using DataManager.Runtime;
using DataManager.Core;
using DataManager.Core.Log;
using DataManager.Runtime.Log;
using Serilog;
using System;
using DataManager.Core.Log.Definition;


namespace DataManager.Runtime.Log.SeriLog
{
    /// <summary>
    /// SerilogHelper 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
    /// 继承或实现的契约：LogBase、IDisposable。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public class SerilogHelper : LogBase , IDisposable
    {
        private readonly object _lock = new();
        private Serilog.Core.Logger? _logger;
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
        /// 初始化 SerilogHelper 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public SerilogHelper()
        {
        }

        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetOrCreateLogger。
        /// 正文中的关键协作调用包括 IsNullOrWhiteSpace、CreateDirectory、CreateLogger、File、Debug；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </summary>
        private Serilog.Core.Logger GetOrCreateLogger()
        {
            lock (_lock)
            {
                if (_logger != null) return _logger;

                var dir = string.IsNullOrWhiteSpace(_param.FilePath)
                    ? AppDomain.CurrentDomain.BaseDirectory
                    : _param.FilePath;
                System.IO.Directory.CreateDirectory(dir);

                // 模板在创建时生成；Param 变更时旧 logger 已释放，下次懒加载用新 Separator
                var outputTemplate = "{Timestamp:yyyy/MM/dd HH:mm:ss} " + _param.Separator + " {CustomLevel} " + _param.Separator + " {Message} {NewLine}";

                // 单实例常驻 logger：Enrich.FromLogContext + 每次写入 PushProperty(CustomLevel)，
                // 保证多线程不同级别互不串扰；shared:true 允许多实例同目录共享文件。
                _logger = new LoggerConfiguration()
                    .Enrich.FromLogContext()
                    .MinimumLevel.Debug()
                    .WriteTo.File(
                        path: System.IO.Path.Combine(dir, "log"),
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: _param.SaveDays <= 0 ? null : _param.SaveDays,
                        fileSizeLimitBytes: null,
                        shared: true,
                        outputTemplate: outputTemplate)
                    .CreateLogger();
                return _logger;
            }
        }

        /// <summary>
        /// 执行资源释放步骤；对应入口为 DisposeLoggerLocked。
        /// </summary>
        private void DisposeLoggerLocked()
        {
            _logger?.Dispose();
            _logger = null;
        }

        /// <summary>
        /// 发起连接操作；对应入口为 Connect。
        /// 正文中的关键协作调用包括 GetOrCreateLogger；具体条件和先后次序见方法体。
        /// </summary>
        public void Connect(ELogLevel logLevel)
        {
            // 兼容旧 API：仅确保 logger 已创建，级别按每次 Add 的 PushProperty 写入
            GetOrCreateLogger();
        }

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
        /// 执行添加操作；对应入口为 Add。
        /// 正文中的关键协作调用包括 GetOrCreateLogger、PushProperty、ToString、Information；具体条件和先后次序见方法体。
        /// </summary>
        public override void Add(string info, ELogLevel logLevel = ELogLevel.Information)
        {
            var logger = GetOrCreateLogger();
            // Serilog Logger 本身线程安全；用 LogContext 按事件携带级别，替代旧的按次重建 logger
            using (Serilog.Context.LogContext.PushProperty("CustomLevel", logLevel.ToString()))
            {
                logger.Information(info);
            }
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
 
