using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.WPF;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
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
using WPFControl.CustomControl.Models;
using WPFControl.CustomControl.Models.Charts;
using WPFControl.CustomControl.Services;
using WPFControl.CustomControl.Services.Charts;
using WPFControl.CustomControl.Views.BulletinBoard;

namespace WPFControl.CustomControl.Views.Charts
{
    /// <summary>
    /// ChartsLineView.xaml 的交互逻辑
    /// </summary>
    public partial class ChartsLineView : CartesianChart
    {
        public ChartsLineView()
        {
            InitializeComponent();
        }

        ChartsLineService chartsLineService = new ChartsLineService();

  
        public static readonly DependencyProperty AxisXProperty = DependencyProperty.Register
        ("AxisX", typeof(ObservableCollection<string>), typeof(ChartsLineView),
         new PropertyMetadata(null, OnValueChanged));

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ChartsLineView view = (ChartsLineView)d;
            view.chartsLineService.SetAxisX(view.AxisX.ToList());
            view.chats_CartesianChart.XAxes = view.chartsLineService.GetAxisX();
        }


        [Bindable(true)]
        [Category("自定义属性"), Description("折线图X轴信息"), DisplayName("折线图X轴信息")]
        public ObservableCollection<string> AxisX
        {
            get { return (ObservableCollection<string>)GetValue(AxisXProperty); }
            set { SetValue(AxisXProperty, value); }
        }


        public static readonly DependencyProperty ItemSourceProperty = DependencyProperty.Register
        ("ItemSource", typeof(ObservableCollection<LineSeries>), typeof(ChartsLineView),
         new PropertyMetadata(null, OnValueChanged_Data));

        private static void OnValueChanged_Data(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ChartsLineView view = (ChartsLineView)d;
          
            // 1. 先移除旧集合的监听（防止内存泄漏）
            if (e.OldValue is ObservableCollection<LineSeries> oldColl)
            {
                oldColl.CollectionChanged -= view.OnCollectionChanged;
            }

            // 2. 监听新集合
            if (e.NewValue is ObservableCollection<LineSeries> newColl)
            {
                newColl.CollectionChanged += view.OnCollectionChanged;
            }

            // 3. 首次刷新
            view.RefreshChart();
        }

        /// <summary>
        /// 集合 Add / Remove / Clear 时触发
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            RefreshChart();
        }

        private void RefreshChart()
        {
            if (ItemSource == null) return;
            chats_CartesianChart.Series = chartsLineService.ShowData(ItemSource.ToArray());
        }


        [Bindable(true)]
        [Category("自定义属性"), Description("折线图数据"), DisplayName("折线图数据")]
        public ObservableCollection<LineSeries> ItemSource
        {
            get { return (ObservableCollection<LineSeries>)GetValue(ItemSourceProperty); }
            set { SetValue(ItemSourceProperty, value); }
        }

        public static readonly DependencyProperty AxisFontSizeProperty = DependencyProperty.Register(
          nameof(AxisFontSize), typeof(double), typeof(ChartsLineView),
          new PropertyMetadata(14.0, OnAxisFontSizeChanged));

        private static void OnAxisFontSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var view = (ChartsLineView)d;
            view.chartsLineService.SetAxisFontSize(view.AxisFontSize);
            // 重新赋值 XAxes 以触发图表刷新（LiveCharts 会响应内部 TextSize 属性变更）
            view.chats_CartesianChart.XAxes = view.chartsLineService.GetAxisX();
        }

        /// <summary>
        /// X轴标签字体大小
        /// </summary>
        [Bindable(true)]
        [Category("自定义属性"), Description("X轴标签字体大小")]
        public double AxisFontSize
        {
            get => (double)GetValue(AxisFontSizeProperty);
            set => SetValue(AxisFontSizeProperty, value);
        }
    }
}
