using Prism.Ioc;
using System;
using WPFCustomControls.Controls;
using WPFControl.Helper;
using DataManager.Log.Definition;
using DataManager.Log;
using System.Threading.Tasks;

namespace WPFControl.ProgramControl.Services
{
    public class LogInfoService : DataGridService<LogInfo>
    {
        LogManager logManager;
        ILog log =>  logManager["RunLog"] ;
        LogAlarmInfoService logAlarmInfoService;

        public LogInfoService(IContainerProvider container)
        {
            logManager= container.Resolve<LogManager>();
            logAlarmInfoService= container.Resolve<LogAlarmInfoService>();
        }

        /// <summary>
        /// 添加运行信息到控件显示和日志文件
        /// </summary>
        /// <param name="info">日志信息</param>
        /// <param name="level">日志级别</param>
        /// <param name="retainNum">控件中保留的最大数据条数</param>
        public void AddRunInfo(string info, ELogLevel level = ELogLevel.Information, int retainNum = 1000)
        {
            TryCatchHelper.TryExecute(() =>
            {
                if (string.IsNullOrEmpty(info)) return;

                // 添加到控件显示（UI线程）
                var logInfo = new LogInfo(DateTime.Now.ToString("HH:mm:ss"), info, level);

                Add(logInfo, retainNum);

                logAlarmInfoService?.Add(logInfo, retainNum);
                // 记录到日志文件 
                log?.Add(info, level);
            });
        }
    }
}