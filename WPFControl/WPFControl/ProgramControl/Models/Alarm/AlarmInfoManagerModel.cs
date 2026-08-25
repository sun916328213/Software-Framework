using CommunityToolkit.Mvvm.ComponentModel;
using Extension.Core.ConvertEx;
using Extension.Core.SystemEx;
using Extension.Helper.Mapping;
using Extension.Struct;
using Mapster;
using Mapster.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WPFControl.CustomControl.Models;

namespace WPFControl.ProgramControl.Models
{
    public class AlarmInfo : ObservableObject
    {
       private int _id;
        /// <summary>
        /// 报警ID
        /// </summary>
        public int ID
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }
        string _info;
        /// <summary>
        /// 报警信息
        /// </summary>
        public string Info
        {
            get => _info;
            set => SetProperty(ref _info, value);
        }

        DateTime  _starttime;
        /// <summary>
        /// 开始时间
        /// </summary>
        public DateTime StartTime
        {
            get => _starttime;
            set => SetProperty(ref _starttime, value);
        }

        DateTime _endtime;
        /// <summary>
        /// 结束时间
        /// </summary>
        public DateTime EndTime
        {
            get => _endtime;
            set => SetProperty(ref _endtime, value);
        }

        double _duration;
        /// <summary>
        /// 持续时间S
        /// </summary>
        public double Duration
        {
            get => _duration;
            set => SetProperty(ref _duration, value);
        }

        Stopwatch _stopwatch=new Stopwatch();
        /// <summary>
        /// 计时器
        /// </summary>
        internal Stopwatch Stopwatch
        {
            get => _stopwatch;
            set => SetProperty(ref _stopwatch, value);
        }
    }

    public class AlarmInfoDTO
    {
        public int ID { get; set; }
        public string Info { get; set; }
        public string StartTime { get; set; }
        public string EndTime { get; set; }
        public string Duration { get; set; }
    }

    public class AlarmInfoManager : ObservableObject
    {
        private int _id;
        /// <summary>
        /// 报警ID
        /// </summary>
        public int ID
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        private string _info_CH;
        /// <summary>
        /// 报警信息中文
        /// </summary>
        public string Info_CH
        {
            get => _info_CH;
            set => SetProperty(ref _info_CH, value);
        }


        private string _info_EN;
        /// <summary>
        /// 报警描述 English
        /// </summary>
        public string Info_EN
        {
            get => _info_EN;
            set => SetProperty(ref _info_EN, value);
        }

        private bool _enable = true;
        /// <summary>
        /// 是否启用
        /// </summary>
        public bool Enable
        {
            get => _enable;
            set => SetProperty(ref _enable, value);
        }


        private string _address;
        [Browsable(false)]
        /// <summary>
        /// 寄存器地址
        /// </summary>
        public string Address
        {
            get => _address;
            set => SetProperty(ref _address, value);
        }
    }
 
  

    public static class AlarmInfoMessage
    {
        public static readonly string[] 报警信息设置 = { "报警信息设置", "AlarmInfo Settings" };
      
    }

    internal class AlarmInfoManagerModel : ManagerModel<AlarmInfoManager>
    {
        public AlarmInfoManagerModel()
        {
            Name = LangSwitchEx.GetMessage(AlarmInfoMessage.报警信息设置);
            FilePath = "Config//";
            FileName = "Alarm.csv";
            Configure(TypeAdapterConfig.GlobalSettings);
        }

        public static void Configure(TypeAdapterConfig config)
        {
            config.ForType<AlarmInfo, AlarmInfoDTO>() 
                .Map(dest => dest.Info, src => src.Info)
                .Map(dest => dest.Duration, src => src.Duration.ToString("0.00") + " s")
                .Map(dest => dest.StartTime, src => src.StartTime.ToDateTime())
                .Map(dest => dest.EndTime, src => src.EndTime.ToDateTime());

            config.ForType<AlarmInfoDTO, AlarmInfo>()
               .Map(dest => dest.Info, src => src.Info)
               .Map(dest => dest.Duration, src => src.Duration.Replace(" s","") )
               .Map(dest => dest.StartTime, src => src.StartTime)
               .Map(dest => dest.EndTime, src => src.EndTime );
        }
    }
}
