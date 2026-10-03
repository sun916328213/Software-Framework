// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：AlarmInfo、AlarmInfoManager、AlarmInfoMessage、AlarmInfoManagerModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、CommunityToolkit.Mvvm.ComponentModel、Extension.Core.Conversion、Extension.UI.Controls、Extension.Integration.Mapster、Mapster。导入不代表所有类型都会在运行时加载。
using WPFControls;
using CommunityToolkit.Mvvm.ComponentModel;
using Extension.Core.Conversion;
using Extension.UI.Controls;
using Extension.Integration.Mapster;
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
using WPFControls.Controls.Models;
using WPFCustomControls.Controls;

namespace WPFControls.ProgramControl.Models
{
    /// <summary>
    /// AlarmInfo 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
    /// 继承或实现的契约：ObservableObject。具体约束按这些契约及各成员实现解释。
    /// </summary>
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

    /// <summary>
    /// AlarmInfoManager 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
    /// 继承或实现的契约：ObservableObject。具体约束按这些契约及各成员实现解释。
    /// </summary>
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
 
  

    /// <summary>
    /// AlarmInfoMessage 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
    /// </summary>
    public static class AlarmInfoMessage
    {
        public static readonly string[] 报警信息设置 = { "报警信息设置", "AlarmInfo Settings" };
      
    }

    /// <summary>
    /// AlarmInfoManagerModel 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
    /// 继承或实现的契约：ConfigManagerModel&lt;AlarmInfoManager&gt;。具体约束按这些契约及各成员实现解释。
    /// </summary>
    internal class AlarmInfoManagerModel : ConfigManagerModel<AlarmInfoManager>
    {
        /// <summary>
        /// 初始化 AlarmInfoManagerModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 GetMessage、Configure；具体条件和先后次序见方法体。
        /// </summary>
        public AlarmInfoManagerModel()
        {
            Name = LangSwitchEx.GetMessage(AlarmInfoMessage.报警信息设置);
            FilePath = "Config//";
            FileName = "Alarm.csv";
            Configure(TypeAdapterConfig.GlobalSettings);
        }

        /// <summary>
        /// 配置当前对象或组合入口；对应入口为 Configure。
        /// 正文中的关键协作调用包括 Map、ForType、ToString、ToDateTime、Replace；具体条件和先后次序见方法体。
        /// </summary>
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
