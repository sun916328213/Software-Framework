using DataManager.Log.Definition;
using DataManager.Log.Log4Net;
using DataManager.Log.NLog;
using DataManager.Log.SeriLog;
using System;
using System.Collections.Generic;

namespace DataManager.Log
{
    public class LogManager : ManagerBase<ILog, LogParam> 
    {
        public override void Config (List<LogParam> param)
        {
            foreach (var item in param)
            {
                Create(item);
            }
        }

        public override ILog Create (LogParam param)
        {
            // 将参数转换为 LogParam 类型
            var logParam = param as LogParam;
            if (null == logParam) throw new ArgumentNullException(nameof(param));
            logParam.Validate();
            
            switch (logParam.LogType) {
                case ELogType.SeriLog:
                    if (Contains(logParam.Name))
                        throw new InvalidOperationException($"已存在同名日志实例: {logParam.Name}");
                    var serilog = new SerilogHelper() { Param = logParam };
                    Add(serilog);
                    return serilog;
                case ELogType.NLog:
                    if (Contains(logParam.Name))
                        throw new InvalidOperationException($"已存在同名日志实例: {logParam.Name}");
                    var nlog = new NLogHelper() { Param = logParam };
                    Add(nlog);
                    return nlog;
                case ELogType.Log4Net:
                    if (Contains(logParam.Name))
                        throw new InvalidOperationException($"已存在同名日志实例: {logParam.Name}");
                    var log4net = new Log4NetHelper() { Param = logParam };
                    Add(log4net);
                    return log4net;
                default: throw new NotSupportedException($"{logParam.LogType} not support.");
            }
        }

        protected override string GetName(ILog device)
        {
          return device.Param.Name;
        }
    }
}