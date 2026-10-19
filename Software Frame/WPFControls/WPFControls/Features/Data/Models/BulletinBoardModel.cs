// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：BulletinBoardModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、CommunityToolkit.Mvvm.ComponentModel、LiveChartsCore.SkiaSharpView、System、System.Collections.Generic、System.Collections.ObjectModel。导入不代表所有类型都会在运行时加载。
using WPFControls;
using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore.SkiaSharpView;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WPFControls.Controls.Models;
using WPFControls.Charts.Models;

namespace WPFControls.ProgramControl.Models
{
    /// <summary>
    /// BulletinBoardModel 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
    /// 继承或实现的契约：ObservableObject。具体约束按这些契约及各成员实现解释。
    /// </summary>
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

        private ObservableCollection<ChartLineData> _data_day = new ObservableCollection<ChartLineData>();
        /// <summary>
        /// 折线图数据 每天
        /// </summary>
        public ObservableCollection<ChartLineData> Data_Day
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

        private ObservableCollection<ChartLineData> _data_month= new ObservableCollection<ChartLineData>();
        /// <summary>
        /// 折线图数据 每月
        /// </summary>
        public ObservableCollection<ChartLineData> Data_Month
        {
            get => _data_month;
            set => SetProperty(ref _data_month, value);
        }
    }
}
