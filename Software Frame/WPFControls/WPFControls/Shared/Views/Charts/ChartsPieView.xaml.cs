// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：ChartsPieView。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、WPFControls.Controls.Views、LiveChartsCore.Defaults、LiveChartsCore.Measure、LiveChartsCore.SkiaSharpView、LiveChartsCore.SkiaSharpView.Extensions。导入不代表所有类型都会在运行时加载。
using LiveChartsCore.Defaults;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Extensions;
using LiveChartsCore.SkiaSharpView.WPF;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using WPFControls.Controls.Models.Charts;
using WPFControls.Controls.Services.Charts;

namespace WPFControls.Controls.Views.Charts
{
    /// <summary>
    /// ChartsPieView.xaml 的交互逻辑
    /// </summary>
    public partial class ChartsPieView : PieChart
    {
        /// <summary>
        /// 初始化 ChartsPieView 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 InitializeComponent；具体条件和先后次序见方法体。
        /// </summary>
        public ChartsPieView()
        {
            InitializeComponent();
        }

        ChartsPieService   chartsPieService = new ChartsPieService();

 
        public static readonly DependencyProperty ItemSourceProperty = DependencyProperty.Register
        ("ItemSource", typeof(ObservableCollection<PieData>), typeof(ChartsPieView),
         new PropertyMetadata(null, OnValueChanged_Data));

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnValueChanged_Data。
        /// 正文中的关键协作调用包括 ShowData、ToArray；具体条件和先后次序见方法体。
        /// </summary>
        private static void OnValueChanged_Data(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ChartsPieView view = (ChartsPieView)d;

            view.charts_PieChart.Series = view.chartsPieService.ShowData(view.ItemSource.ToArray(), ChartsPieType.Doughnut);
        }

      

        [Bindable(true)]
        [Category("自定义属性"), Description("饼状图数据"), DisplayName("饼状图数据")]
        public ObservableCollection<PieData> ItemSource
        {
            get { return (ObservableCollection<PieData>)GetValue(ItemSourceProperty); }
            set { SetValue(ItemSourceProperty, value); }
        }
    }
}
