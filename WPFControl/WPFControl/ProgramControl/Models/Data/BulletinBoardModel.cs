using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore.SkiaSharpView;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WPFControl.CustomControl.Models;
using WPFControl.CustomControl.Models.Charts;

namespace WPFControl.ProgramControl.Models
{
    public class BulletinBoardModel : ObservableObject
    {
 
        private ObservableCollection<string> _axisX_day = new ObservableCollection<string>();
        /// <summary>
        /// 轴X信息 每天
        /// </summary>
        public ObservableCollection<string> AxisX_Day
        {
            get => _axisX_day;
            set => SetProperty(ref _axisX_day, value);
        }

        private ObservableCollection<LineSeries> _data_day = new ObservableCollection<LineSeries>();
        /// <summary>
        /// 折线图数据 每天
        /// </summary>
        public ObservableCollection<LineSeries> Data_Day
        {
            get => _data_day;
            set => SetProperty(ref _data_day, value);
        }



        private ObservableCollection<string> _axisX_month = new ObservableCollection<string>();
        /// <summary>
        /// 轴X信息 每月
        /// </summary>
        public ObservableCollection<string> AxisX_Month
        {
            get => _axisX_month;
            set => SetProperty(ref _axisX_month, value);
        }

        private ObservableCollection<LineSeries> _data_month= new ObservableCollection<LineSeries>();
        /// <summary>
        /// 折线图数据 每月
        /// </summary>
        public ObservableCollection<LineSeries> Data_Month
        {
            get => _data_month;
            set => SetProperty(ref _data_month, value);
        }
    }
}
