using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using System.Collections.ObjectModel;
using System.Windows.Media;

namespace WPFControl.CustomControl.Models.Charts
{
    /// <summary>
    /// 饼状图数据
    /// </summary>
    public class PieData
    {
        /// <summary>
        /// 值
        /// </summary>
        public double  Value { get; set; }  

        /// <summary>
        /// Series Name
        /// </summary>
        public string SeriesName { get; set; }
        /// <summary>
        /// 颜色
        /// </summary>
        public SolidColorBrush Color { get; set; }
    }

    public class ChartsPieModel:ObservableObject
    {

        ObservableCollection<ISeries> series = new ObservableCollection<ISeries>();
        /// <summary>
        /// Series集合
        /// </summary>
        public ObservableCollection<ISeries> Series
        {
            get => series;
            set => SetProperty(ref series, value);
        }
    }
}
