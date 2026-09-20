using Extension.Core.Conversion;
using Extension.Core.Models;
using Extension.Imaging.Models;
using Extension.Automation.Device;
using Extension.Automation.Secs;
using System;
using WPFControl.Helper;
using WPFControl.ProgramControl.Models;
using WPFCustomControls.Window.Views;

namespace WPFControl.ProgramControl.Services
{
    /// <summary>
    /// 统计信息服务类
    /// 负责处理统计相关的业务逻辑
    /// </summary>
    public class StatisticService  
    {
        internal StatisicsModel Model { get; set; } = new StatisicsModel();
        /// <summary>
        /// 更新ID
        /// </summary>
        /// <param name="pID">新的ID</param>
        public void UpdateID(string pID)
        {
            TryCatchHelper.TryExecute(() =>
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
            TryCatchHelper.TryExecute(() =>
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
            TryCatchHelper.TryExecute(() =>
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
            TryCatchHelper.TryExecute(() =>
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
            TryCatchHelper.TryExecute(() =>
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
            TryCatchHelper.TryExecute(() =>
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
            TryCatchHelper.TryExecute(() =>
            {
                Model.DeviceStatus = pDeviceStatus;
            });
        }

        /// <summary>
        /// 更新设备时间
        /// </summary>
        public void UpdateTime()
        {
            TryCatchHelper.TryExecute(() =>
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
            TryCatchHelper.TryExecute(() =>
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
            TryCatchHelper.TryExecute(() =>
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
            TryCatchHelper.TryExecute(() =>
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
            TryCatchHelper.TryExecute(() =>
            {
                Model.ID = string.Empty;
                Model.OK = 0;
                Model.Total = 0;

                Model.DateTime = string.Empty;
            });
        }
    }
}
