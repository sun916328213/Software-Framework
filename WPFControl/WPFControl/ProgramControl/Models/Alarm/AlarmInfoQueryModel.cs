using CommunityToolkit.Mvvm.ComponentModel;
using Extension.Struct;
using Mapster;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WPFControl.ProgramControl.Models
{
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

   
