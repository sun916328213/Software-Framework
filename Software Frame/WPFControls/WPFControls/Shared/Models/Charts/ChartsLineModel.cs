// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：LineSeries、ChartsLineModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、WPFControls.Controls.Models、LiveChartsCore、LiveChartsCore.Defaults、LiveChartsCore.Kernel.Sketches、System.Collections.ObjectModel。导入不代表所有类型都会在运行时加载。
using WPFControls;
using WPFControls.Controls.Models;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Kernel.Sketches;
using System.Collections.ObjectModel;
using System.Windows.Media;

namespace WPFControls.Controls.Models.Charts
{
    /// <summary>
    /// LineSeries 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
    /// </summary>
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

    /// <summary>
    /// ChartsLineModel 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
    /// </summary>
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
