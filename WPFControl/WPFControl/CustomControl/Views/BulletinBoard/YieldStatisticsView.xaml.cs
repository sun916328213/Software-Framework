using LiveChartsCore.SkiaSharpView;
using Prism.Ioc;
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
using WPFControl.CustomControl.Models;
using WPFControl.CustomControl.Models.Charts;
using WPFControl.CustomControl.Services;
using WPFControl.CustomControl.Views.Charts;

namespace WPFControl.CustomControl.Views.BulletinBoard
{
    /// <summary>
    /// YieldStatisticsView.xaml 的交互逻辑
    /// </summary>
    public partial class YieldStatisticsView : UserControl
    {

        public YieldStatisticsView()
        {
            InitializeComponent();

        }


        public static readonly DependencyProperty AxisXProperty = DependencyProperty.Register
        ("AxisX", typeof(ObservableCollection<string>), typeof(YieldStatisticsView),
         new PropertyMetadata(null, OnValueChanged));

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            YieldStatisticsView view = (YieldStatisticsView)d;
            view.LineCharts.AxisX = (ObservableCollection<string>)e.NewValue;
        }


        [Bindable(true)]
        [Category("自定义属性"), Description("折线图X轴信息"), DisplayName("折线图X轴信息")]
        public ObservableCollection<string> AxisX
        {
            get { return (ObservableCollection<string>)GetValue(AxisXProperty); }
            set { SetValue(AxisXProperty, value); }
        }


        public static readonly DependencyProperty ItemsourceProperty = DependencyProperty.Register
        ("ItemSource", typeof(ObservableCollection<LineSeries>), typeof(YieldStatisticsView),
         new PropertyMetadata(null, OnValueChanged_Data));

        private static void OnValueChanged_Data(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            YieldStatisticsView view = (YieldStatisticsView)d;
            view.LineCharts.ItemSource = view.ItemSource;

        }


        [Bindable(true)]
        [Category("自定义属性"), Description("折线图数据"), DisplayName("折线图数据")]
        public ObservableCollection<LineSeries> ItemSource
        {
            get { return (ObservableCollection<LineSeries>)GetValue(ItemsourceProperty); }
            set { SetValue(ItemsourceProperty, value); }
        }



        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register
        ("Title", typeof(string), typeof(YieldStatisticsView),
        new PropertyMetadata(null, OnTitleValueChanged));

        private static void OnTitleValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            YieldStatisticsView view = (YieldStatisticsView)d;
            view.tb_Title.Text = view.Title;
        }


        [Bindable(true)]
        [Category("自定义属性"), Description("标题"), DisplayName("标题")]
        public string Title
        {
            get { return (string)GetValue(TitleProperty); }
            set { SetValue(TitleProperty, value); }
        }

        public static readonly DependencyProperty AxisFontSizeProperty = DependencyProperty.Register(
          "AxisFontSize", typeof(double), typeof(YieldStatisticsView),
          new PropertyMetadata(14.0, OnAxisFontSizeChanged));

        private static void OnAxisFontSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            YieldStatisticsView view = (YieldStatisticsView)d;

            view.LineCharts.AxisFontSize = (double)e.NewValue;
        }

        [Bindable(true)]
        [Category("自定义属性"), Description("X轴标签全局字体大小"), DisplayName("X轴字体大小")]
        public double AxisFontSize
        {
            get => (double)GetValue(AxisFontSizeProperty);
            set => SetValue(AxisFontSizeProperty, value);
        }

    }
}
