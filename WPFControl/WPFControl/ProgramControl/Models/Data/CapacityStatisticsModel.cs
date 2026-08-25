using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using WPFControl.CustomControl.Models.Charts;

namespace WPFControl.ProgramControl.Models
{
    /// <summary>
    /// 产能统计数据模型
    /// </summary>
    public class CapacityStatisticsModel : ObservableObject
    {
        private int _todayOutput;
        private long _totalOutput;
        private int _goodCount;
        private int _defectCount;
        private double _yieldRate;
        private double _runTime;
        private double _utilizationRate;
        private string _updateTime;

        /// <summary>
        /// 今日产量
        /// </summary>
        public int TodayOutput
        {
            get => _todayOutput;
            set => SetProperty(ref _todayOutput, value);
        }

        /// <summary>
        /// 累计产量
        /// </summary>
        public long TotalOutput
        {
            get => _totalOutput;
            set => SetProperty(ref _totalOutput, value);
        }

        /// <summary>
        /// 良品数
        /// </summary>
        public int GoodCount
        {
            get => _goodCount;
            set => SetProperty(ref _goodCount, value);
        }

        /// <summary>
        /// 不良品数
        /// </summary>
        public int DefectCount
        {
            get => _defectCount;
            set => SetProperty(ref _defectCount, value);
        }

        /// <summary>
        /// 良率(%)
        /// </summary>
        public double YieldRate
        {
            get => _yieldRate;
            set => SetProperty(ref _yieldRate, value);
        }

        /// <summary>
        /// 运行时长(h)
        /// </summary>
        public double RunTime
        {
            get => _runTime;
            set => SetProperty(ref _runTime, value);
        }

        /// <summary>
        /// 稼动率(%)
        /// </summary>
        public double UtilizationRate
        {
            get => _utilizationRate;
            set => SetProperty(ref _utilizationRate, value);
        }

        /// <summary>
        /// 更新时间
        /// </summary>
        public string UpdateTime
        {
            get => _updateTime;
            set => SetProperty(ref _updateTime, value);
        }

        /// <summary>
        /// 产量趋势X轴
        /// </summary>
        public ObservableCollection<string> AxisX { get; set; } = new ObservableCollection<string>();

        /// <summary>
        /// 产量趋势折线图数据
        /// </summary>
        public ObservableCollection<LineSeries> OutputTrend { get; set; } = new ObservableCollection<LineSeries>();

        /// <summary>
        /// 良品/不良品饼图数据
        /// </summary>
        public ObservableCollection<PieData> PieData { get; set; } = new ObservableCollection<PieData>();
    }
}