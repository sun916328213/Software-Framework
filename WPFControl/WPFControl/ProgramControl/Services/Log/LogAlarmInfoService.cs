using DataManager.Log;
using Extension.Struct;
using Prism.Ioc;
using System.Linq;
using WPFCustomControls.Controls;
using WPFControl.Helper;
using WPFControl.ProgramControl.Models;

namespace WPFControl.ProgramControl.Services
{
    public class LogAlarmInfoService : DataGridService<LogInfo>
    {
        IContainerProvider containerProvider;
        AlarmInfoManagerService _AlarmInfoManagerService => containerProvider.Resolve<AlarmInfoManagerService>();

        public LogAlarmInfoService(IContainerProvider container)
        {
            containerProvider = container;
        }

        /// <summary>
        /// 添加数据 不记录日志 只显示
        /// </summary>
        /// <param name="data">要添加的数据项</param>
        /// <param name="retainNum">保留的最大数据条数</param>
        public override void Add(LogInfo data, int retainNum = 200)
        {
            ELogLevel logLevel = data.Level;

            // 报警/重置触发立即执行（不涉及UI更新）
            if (logLevel == ELogLevel.Warning)
                TriggerAlarm(data);
            if (logLevel == ELogLevel.Reset)
                ResetAlarm(data);

            // UI更新部分只入队，由 DataGridLogView 的定时器批量刷新
            if (logLevel != ELogLevel.Information)
                base.Add(data, retainNum);
        }

        /// <summary>
        /// 触发报警
        /// </summary>
        /// <param name="logInfo"></param>
        private void TriggerAlarm(LogInfo logInfo)
        {
            AlarmInfoManager alarmInfo = _AlarmInfoManagerService.Model.DataCollection.FirstOrDefault(x => x.Info_CH == logInfo.Info);
            _AlarmInfoManagerService.TriggerAlarm(alarmInfo, false);
        }

        /// <summary>
        /// 重置报警
        /// </summary>
        /// <param name="logInfo"></param>
        private void ResetAlarm(LogInfo logInfo)
        {
            AlarmInfoManager alarmInfo = _AlarmInfoManagerService.Model.DataCollection.FirstOrDefault(x => x.Info_CH == logInfo.Info);

            _AlarmInfoManagerService.ResetAlarm(alarmInfo, false);
        }
    }
}