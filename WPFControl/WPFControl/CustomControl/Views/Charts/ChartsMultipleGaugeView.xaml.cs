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
    /// ChartsMultipleGaugeView.xaml 的交互逻辑
    /// </summary>
    public partial class ChartsMultipleGaugeView : PieChart
    {
        public ChartsMultipleGaugeView()
        {
            InitializeComponent();
        }

        ChartsPieService chartsPieService = new ChartsPieService();


        public static readonly DependencyProperty ItemSourceProperty = DependencyProperty.Register
        ("ItemSource", typeof(ObservableCollection<PieData>), typeof(ChartsMultipleGaugeView),
         new PropertyMetadata(null, OnValueChanged_Data));

        private static void OnValueChanged_Data(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ChartsMultipleGaugeView view = (ChartsMultipleGaugeView)d;

            view.charts_PieChart.Series = view.chartsPieService.ShowData(view.ItemSource.ToArray(), ChartsPieType.MultipleGauge);
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
