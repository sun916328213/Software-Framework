using DataManager.Log.Definition;
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
            
            switch (logParam.LogType) {
                case ELogType.SeriLog: 
                    var serilog= new SerilogHelper() { Param = logParam };
                    DataManagers.Add(serilog);
                    return serilog;
                case ELogType.NLog: return null;
                case ELogType.Log4Net: return null;
                default: throw new NotSupportedException($"{logParam.LogType} not support.");
            }
        }

        protected override string GetName(ILog device)
        {
          return device.Param.Name;
        }
    }
}