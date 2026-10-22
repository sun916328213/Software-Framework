// 【中文阅读说明】
// WPFControls.Charts 模块：图表组件，本文件是 LiveCharts 饼图控件，负责依赖属性与刷新入口。
// 本文件定义：ChartsPieView。采用快照刷新，不额外建立订阅，也不覆盖宿主 DataContext。
using LiveChartsCore.SkiaSharpView.WPF;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using WPFControls.Charts.Models;
using WPFControls.Controls.Services.Charts;

namespace WPFControls.Controls.Views.Charts
{
    /// <summary>
    /// ChartsPieView.xaml 的交互逻辑。
    /// </summary>
    public partial class ChartsPieView : PieChart
    {
        private readonly ChartsPieService chartsPieService = new ChartsPieService();

        /// <summary>
        /// 初始化 ChartsPieView 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public ChartsPieView()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty ItemSourceProperty = DependencyProperty.Register
        ("ItemSource", typeof(ObservableCollection<ChartPieData>), typeof(ChartsPieView),
         new PropertyMetadata(null, OnValueChanged_Data));

        /// <summary>
        /// 数据源被替换时刷新；null 表示清空显示。
        /// </summary>
        private static void OnValueChanged_Data(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ChartsPieView view = (ChartsPieView)d;
            view.RefreshChart();
        }

        /// <summary>
        /// 显式刷新入口：替换数据源后会自动调用；原地修改输入数据时由宿主在 UI 线程调用。
        /// </summary>
        public void RefreshChart()
        {
            VerifyAccess();
            charts_PieChart.Series = chartsPieService.ShowData(
                ItemSource?.ToArray() ?? Array.Empty<ChartPieData>(), ChartsPieType.Doughnut);
            WPFControls.Charts.Themes.ChartTheme.Apply(this);
        }

        [Bindable(true)]
        [Category("自定义属性"), Description("饼状图数据"), DisplayName("饼状图数据")]
        public ObservableCollection<ChartPieData> ItemSource
        {
            get { return (ObservableCollection<ChartPieData>)GetValue(ItemSourceProperty); }
            set { SetValue(ItemSourceProperty, value); }
        }
    }
}
