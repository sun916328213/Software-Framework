using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Kernel.Sketches;
using System.Collections.ObjectModel;
using System.Windows.Media;

namespace WPFControl.CustomControl.Models.Charts
{
    public class LineSeries
    {
        /// <summary>
        /// 值集合
        /// </summary>
        public ObservableCollection<double> Value { get; set; } = new ObservableCollection<double>();

        /// <summary>
        /// Series Name
        /// </summary>
        public string SeriesName { get; set; }

        /// <summary>
        /// 颜色
        /// </summary>
        public SolidColorBrush Color { get; set; }
    }

    public class ChartsLineModel
    {
        /// <summary>
        /// 图表点大小
        /// </summary>
        public double PointSize { get; set; } = 10;

        // X轴字体大小
        public double AxisFontSize { get; set; }=14;

        /// <summary>
        /// Series集合
        /// </summary>
        internal ObservableCollection<ISeries> Series { get; set; } = new ObservableCollection<ISeries>();

        /// <summary>
        /// X轴集合
        /// </summary>
        internal ObservableCollection<ICartesianAxis> Axis_X { get; set; } = new ObservableCollection<ICartesianAxis>();

        /// <summary>
        /// 数据点集合
        /// </summary>
        internal ObservableCollection<LineSeries> DataCollection { get; set; } = new ObservableCollection<LineSeries>();
    }
}
