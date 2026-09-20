using CommunityToolkit.Mvvm.ComponentModel;
using System;
using Extension.Core.Models;
using Extension.Imaging.Models;
using Extension.Automation.Device;
using Extension.Automation.Secs;
using System.Runtime;

namespace WPFControl.ProgramControl.Models
{
    /// <summary>
    /// 连接状态
    /// </summary>
    public enum ELinkStatus
    {
        Online,
        Offline
    }
 
    public class StatisicsModel:ObservableObject
    {
        private string _id;
        private int _ok;
        private int _totail;

        /// <summary>
        /// ID
        /// </summary>
        public string ID
        {
            get { return _id; }
            set => SetProperty(ref _id, value);
        }

        /// <summary>
        /// OK数量
        /// </summary>
        public int OK
        {
            get { return _ok; }
            set => SetProperty(ref _ok, value);
        }

        /// <summary>
        /// 总数
        /// </summary>
        public int Total
        {
            get { return _totail; }
            set => SetProperty(ref _totail, value);
        }

        /// <summary>
        /// 合格率
        /// </summary>
        public string Rate
        {
            get
            {
                if (Total == 0)
                    return "100%";
                else
                    return (Convert.ToDouble(OK) / Convert.ToDouble(Total) * 100).ToString("0.0") + "%";
            }
        }

        double alarmTime,runtime;
        public double AlarmTime
        { 
           get=> alarmTime;
            set=>SetProperty(ref alarmTime, value);
        }

        public double RunTime
        {
            get => runtime;
            set => SetProperty(ref runtime, value);
        }



        private EDeviceStatus _deviceStatus;
        private ELinkStatus _plcLinkStatus;
        private ELinkStatus _secsStatus;
        private ESecsMode _secsMode;
        private string _dateTime = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        /// <summary>
        /// 设备状态
        /// </summary>
        public EDeviceStatus DeviceStatus
        {
            get { return _deviceStatus; }
            set => SetProperty(ref _deviceStatus, value);
        }

        /// <summary>
        /// PLC连接状态
        /// </summary>
        public ELinkStatus PLCLinkStatus
        {
            get { return _plcLinkStatus; }
            set => SetProperty(ref _plcLinkStatus, value);
        }

        /// <summary>
        /// SECS连接状态
        /// </summary>
        public ELinkStatus SecsStatus
        {
            get { return _secsStatus; }
            set => SetProperty(ref _secsStatus, value);
        }

        /// <summary>
        /// SECS模式
        /// </summary>
        public ESecsMode SecsMode
        {
            get { return _secsMode; }
            set => SetProperty(ref _secsMode, value);
        }

        /// <summary>
        /// 时间
        /// </summary>
        public string DateTime
        {
            get { return _dateTime; }
            set => SetProperty(ref _dateTime, value);
        }
    }
}