using Serilog;
using System;
using Extension.Struct;
using DataManager.Log.Definition;


namespace DataManager.Log.SeriLog
{
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

        public SerilogHelper()
        {
        }

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

        private void DisposeLoggerLocked()
        {
            _logger?.Dispose();
            _logger = null;
        }

        public void Connect(ELogLevel logLevel)
        {
            // 兼容旧 API：仅确保 logger 已创建，级别按每次 Add 的 PushProperty 写入
            GetOrCreateLogger();
        }

        public void DisConnect()
        {
            lock (_lock)
            {
                DisposeLoggerLocked();
            }
        }

        public override void Add(string info, ELogLevel logLevel = ELogLevel.Information)
        {
            var logger = GetOrCreateLogger();
            // Serilog Logger 本身线程安全；用 LogContext 按事件携带级别，替代旧的按次重建 logger
            using (Serilog.Context.LogContext.PushProperty("CustomLevel", logLevel.ToString()))
            {
                logger.Information(info);
            }
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
 
