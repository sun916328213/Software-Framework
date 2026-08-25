using Serilog;
using Serilog.Core;
using Serilog.Events;
using System;
using Extension.Struct;
using DataManager.Log.Definition;


namespace DataManager.Log.SeriLog
{
    // 自定义Enricher，用于将Serilog级别映射为我们的自定义级别名称
    internal class CustomLevelEnricher : ILogEventEnricher
    {
        private readonly ELogLevel _logLevel;

        public CustomLevelEnricher(ELogLevel logLevel)
        {
            _logLevel = logLevel;
        }

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            var customLevel = _logLevel.ToString();
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("CustomLevel", customLevel));
        }
    }

    public class SerilogHelper : LogBase , IDisposable
    {

        private object obj = new object();

        private string _logOutputTemplate;
 
        public override LogParam Param { get; set; } = new LogParam();

        public SerilogHelper()
        {
            _logOutputTemplate = "{Timestamp:yyyy/MM/dd HH:mm:ss} " + Param.Separator + " {CustomLevel} " + Param.Separator + " {Message} {NewLine}";
        }

        public void Connect(ELogLevel logLevel)
        {
            // 创建全局静态实例
            var loggerConfig = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .MinimumLevel.Debug()
                .WriteTo.File(
                    path: Param.FilePath + "\\.txt",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: Param.SaveDays,
                     fileSizeLimitBytes: null,
                    outputTemplate: _logOutputTemplate);

            // 添加自定义Enricher
            loggerConfig = loggerConfig.Enrich.With(new CustomLevelEnricher(logLevel));
            Serilog.Log.Logger = loggerConfig.CreateLogger();
        }

        public void DisConnect()
        {
            Serilog.Log.CloseAndFlush();
        }

        public void Config()
        {
            throw new NotImplementedException();
        }

        public override void Add(string info, ELogLevel logLevel = ELogLevel.Information)
        {
            lock (obj)
            {
                Connect(logLevel);
                Serilog.Log.Information(info);
                DisConnect();
            }
        }
 

        public void Dispose()
        {
            Serilog.Log.CloseAndFlush();
            GC.SuppressFinalize(this);
        }
 
    }
}
 
