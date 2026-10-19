// 【中文阅读说明】
// WPFControls.Charts 模块：图表组件，两套绘图后端共用输入模型，业务查询、数据采集与设备操作归宿主。
// 本文件定义：ChartLineData。折线图的共用输入，只表达展示所需内容，不包含任何绘图库类型。
using System.Collections.ObjectModel;
using System.Windows.Media;

namespace WPFControls.Charts.Models
{
    /// <summary>
    /// 折线图输入数据：曲线名称、按索引排列的数值集合和颜色。
    /// 该类型由 LiveCharts 与 OxyPlot 两套后端共用，不承载数据库查询、设备连接或刷新定时器。
    /// </summary>
    public class ChartLineData
    {
        /// <summary>
        /// 值集合，按索引与坐标标签一一对应。
        /// </summary>
        public ObservableCollection<double> Value { get; set; } = new ObservableCollection<double>();

        /// <summary>
        /// 曲线名称，用于图例与提示。
        /// </summary>
        public string SeriesName { get; set; }

        /// <summary>
        /// 曲线颜色。
        /// </summary>
        public SolidColorBrush Color { get; set; }
    }
}
