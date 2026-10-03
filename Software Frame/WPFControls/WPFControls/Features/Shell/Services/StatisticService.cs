// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：StatisticService。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、Extension.Core.Conversion、Extension.Core.Models、Extension.Imaging.Models、Extension.Automation.Device、Extension.Automation.Secs。导入不代表所有类型都会在运行时加载。
using WPFControls;
using Extension.Core.Conversion;
using Extension.Core.Models;
using Extension.Imaging.Models;
using Extension.Automation.Device;
using Extension.Automation.Secs;
using System;
using WPFControls.Controls.Diagnostics;
using WPFControls.ProgramControl.Models;
using WPFCustomControls.Window.Views;

namespace WPFControls.ProgramControl.Services
{
    /// <summary>
    /// 统计信息服务类
    /// 负责处理统计相关的业务逻辑
    /// </summary>
    public class StatisticService  
    {
        private readonly TryCatchHelper _errors;
        /// <summary>
        /// 初始化 StatisticService 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public StatisticService(TryCatchHelper? errors = null) => _errors = errors ?? new();

        internal StatisicsModel Model { get; set; } = new StatisicsModel();
        /// <summary>
        /// 更新ID
        /// </summary>
        /// <param name="pID">新的ID</param>
        public void UpdateID(string pID)
        {
            _errors.TryExecute(() =>
            {
                Model.ID = pID;
            });
        }

        /// <summary>
        /// 更新计数
        /// </summary>
        /// <param name="pResult">计数结果</param>
        public void UpdateCount(bool pResult)
        {
            _errors.TryExecute(() =>
            {
                if (pResult)
                    Model.OK += 1;

                Model.Total += 1;
            });
        }

        /// <summary>
        /// 更新计数
        /// </summary>
        /// <param name="ok">OK数量</param>
        /// <param name="total">总数</param>
        public void UpdateCount(int ok, int total)
        {
            _errors.TryExecute(() =>
            {
                Model.OK = ok;
                Model.Total = total;
            });
        }


        /// <summary>
        /// 更新PLC状态
        /// </summary>
        /// <param name="pPLCStatus">PLC连接状态</param>
        public void UpdatePLC(ELinkStatus pPLCStatus)
        {
            _errors.TryExecute(() =>
            {
                Model.PLCLinkStatus = pPLCStatus;
            });
        }

        /// <summary>
        /// 更新SECS状态
        /// </summary>
        /// <param name="pSecsStatus">SECS模式</param>
        public void UpdateSecsStatus(ELinkStatus pSecsStatus)
        {
            _errors.TryExecute(() =>
            {
                Model.SecsStatus = pSecsStatus;
            });
        }

        /// <summary>
        /// 更新SECS模式
        /// </summary>
        /// <param name="pSecsStatus">SECS状态</param>
        public void UpdateSecsMode(ESecsMode pSecsStatus)
        {
            _errors.TryExecute(() =>
            {
                Model.SecsMode = pSecsStatus;
            });
        }

        /// <summary>
        /// 更新设备状态
        /// </summary>
        /// <param name="pDeviceStatus">设备状态</param>
        public void UpdateDeviceStatus(EDeviceStatus pDeviceStatus)
        {
            _errors.TryExecute(() =>
            {
                Model.DeviceStatus = pDeviceStatus;
            });
        }

        /// <summary>
        /// 更新设备时间
        /// </summary>
        public void UpdateTime()
        {
            _errors.TryExecute(() =>
            {
                Model.DateTime = DateTime.Now.ToDateTime();
            });
        }

        /// <summary>
        /// 更新设备运行时间
        /// </summary>
        /// <param name="alarmTime">报警时间</param>
        /// <param name="runTime">运行时间</param>
        public void UpdateRunTime(double alarmTime, double runTime)
        {
            _errors.TryExecute(() =>
            {
                Model.RunTime = runTime;
                Model.AlarmTime = alarmTime;
            });
        }

        /// <summary>
        /// 清除计数
        /// </summary>
        public void ClearCount()
        {
            _errors.TryExecute(() =>
            {
                Model.OK = 0;
                Model.Total = 0;
            });
        }

        /// <summary>
        /// 清除计数
        /// </summary>
        public void Clear()
        {
            _errors.TryExecute(() =>
            {
                MessagePrompt messagePrompt = new MessagePrompt("提示", "确定要清零吗？");
                if (messagePrompt.ShowDialog() == true)
                {
                    ClearCount();
                }
            });
        }

        /// <summary>
        /// 重置所有统计信息
        /// </summary>
        public void ResetAll()
        {
            _errors.TryExecute(() =>
            {
                Model.ID = string.Empty;
                Model.OK = 0;
                Model.Total = 0;

                Model.DateTime = string.Empty;
            });
        }
    }
}
