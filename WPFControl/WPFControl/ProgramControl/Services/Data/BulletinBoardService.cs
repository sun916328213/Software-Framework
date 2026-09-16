using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WPFControl.ProgramControl.Models;
using Prism.Ioc;
using Extension.Core.ConvertEx;
using System.Collections.ObjectModel;
using WPFControl.CustomControl.Models;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore.SkiaSharpView;
using WPFControl.CustomControl.Models.Charts;

namespace WPFControl.ProgramControl.Services
{
    public class BulletinBoardService
    {
       public BulletinBoardModel Model { get; set; } = new BulletinBoardModel();

        internal StatisticService StatisticService;

 
        IContainerProvider containerProvider;   
        public BulletinBoardService(IContainerProvider container)
        {
            containerProvider= container;
            StatisticService= container.Resolve<StatisticService>();
        }

        /// <summary>
        /// 显示一天的产能
        /// </summary>
        /// <param name="days"></param>
        /// <param name="datas"></param>
        public void ShowData_Day(    List<LineSeries> datas)
        {
            List<string> axis = new List<string>();

            DateTime curtime = DateTime.Now;
            for (int i = curtime.Hour; i > 0; i--)
            {
                axis.Add(curtime.AddHours(-i).Hour.ToString()+":00");

            }

            Model.AxisX_Day = new ObservableCollection<string>(axis);
            Model.Data_Day = new ObservableCollection<LineSeries>(datas);
        }

        /// <summary>
        /// 显示一月的产能
        /// </summary>
        /// <param name="datas"></param>
        public void ShowData_Month(  List<LineSeries> datas)
        {
            List<string> axis = new List<string>();

            DateTime curtime = DateTime.Now;
            for (int i = curtime.Month; i > 0; i--)
            {
                axis.Add(curtime.AddDays(-i).ToYearMonth());

            }

            Model.AxisX_Month = new ObservableCollection<string>(axis);
            Model.Data_Month = new ObservableCollection<LineSeries>(datas);
        }
 
    }
}

