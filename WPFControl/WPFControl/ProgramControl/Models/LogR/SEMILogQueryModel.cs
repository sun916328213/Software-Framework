using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;

namespace WPFControl.ProgramControl.Models
{
    public class SEMILogQueryModel : ObservableObject
    {
 
        ObservableCollection<string> semilogDatas = new ObservableCollection<string>();
        /// <summary>
        /// Log显示数据集合
        /// </summary>
        public ObservableCollection<string> SEMILogDatas
        {
            get => semilogDatas;
            set => SetProperty(ref semilogDatas, value);
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
