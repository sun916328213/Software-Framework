// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：LogAlarmInfoService。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、DataManager.Core.Log、DataManager.Runtime.Log、DataManager.Core.Log.Definition、Prism.Ioc、System.Linq。导入不代表所有类型都会在运行时加载。
using WPFControls;
using DataManager.Core.Log;
using DataManager.Runtime.Log;
using DataManager.Core.Log.Definition;
using Prism.Ioc;
using System.Linq;
using WPFCustomControls.Controls;
using WPFControls.Controls.Diagnostics;
using WPFControls.ProgramControl.Models;

namespace WPFControls.ProgramControl.Services
{
    /// <summary>
    /// LogAlarmInfoService 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
    /// 继承或实现的契约：DataGridService&lt;LogInfo&gt;。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public class LogAlarmInfoService : DataGridService<LogInfo>
    {
        IContainerProvider containerProvider;
        AlarmInfoManagerService _AlarmInfoManagerService => containerProvider.Resolve<AlarmInfoManagerService>();

        /// <summary>
        /// 初始化 LogAlarmInfoService 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
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
            if (alarmInfo == null)
                return;
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