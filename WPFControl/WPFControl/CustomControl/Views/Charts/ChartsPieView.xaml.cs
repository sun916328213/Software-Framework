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
using WPFControl.CustomControl.Models.Charts;
using WPFControl.CustomControl.Services.Charts;

namespace WPFControl.CustomControl.Views.Charts
{
    /// <summary>
    /// ChartsPieView.xaml 的交互逻辑
    /// </summary>
    public partial class ChartsPieView : PieChart
    {
        public ChartsPieView()
        {
            InitializeComponent();
        }

        ChartsPieService   chartsPieService = new ChartsPieService();

 
        public static readonly DependencyProperty ItemSourceProperty = DependencyProperty.Register
        ("ItemSource", typeof(ObservableCollection<PieData>), typeof(ChartsPieView),
         new PropertyMetadata(null, OnValueChanged_Data));

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
