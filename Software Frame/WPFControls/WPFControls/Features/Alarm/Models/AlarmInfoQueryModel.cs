// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：AlarmInfoPie、AlarmInfoQueryModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、CommunityToolkit.Mvvm.ComponentModel、System、System.Collections.Generic、System.Collections.ObjectModel、System.ComponentModel。导入不代表所有类型都会在运行时加载。
using WPFControls;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WPFControls.ProgramControl.Models
{
    /// <summary>
    /// AlarmInfoPie 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
    /// </summary>
    public class AlarmInfoPie
    {
        public int ID { get; set; }
        public string Info { get; set; }
        public string Duration { get; set; }
        /// <summary>
        /// 次数
        /// </summary>
        public int Count { get; set; }
        /// <summary>
        /// 百分比 次数
        /// </summary>
        public string Percentage_of_Times { get; set; }

    }



    /// <summary>
    /// AlarmInfoQueryModel 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
    /// 继承或实现的契约：ObservableObject。具体约束按这些契约及各成员实现解释。
    /// </summary>
    internal class AlarmInfoQueryModel : ObservableObject
    {
        ObservableCollection<AlarmInfoDTO> datas = new ObservableCollection<AlarmInfoDTO>();
        /// <summary>
        /// 报警时间信息数据集合
        /// </summary>
        public ObservableCollection<AlarmInfoDTO> Datas
        {
            get => datas;
            set => SetProperty(ref datas, value);
        }

        DateTime dateTime_Start = DateTime.Now, dateTime_End = DateTime.Now;
        /// <summary>
        /// 查询开始时间
        /// </summary>
        public DateTime DateTime_Start
        {
            get => dateTime_Start;
            set => SetProperty(ref dateTime_Start, value);
        }

        /// <summary>
        /// 查询结束时间
        /// </summary>
        public DateTime DateTime_End
        {
            get => dateTime_End;
            set => SetProperty(ref dateTime_End, value);
        }
    }
}

   
