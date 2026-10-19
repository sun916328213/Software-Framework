// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：YieldStatisticsView。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、WPFControls.Controls.Views、LiveChartsCore.SkiaSharpView、Prism.Ioc、System、System.Collections.Generic。导入不代表所有类型都会在运行时加载。
using WPFControls;
using WPFControls.Controls.Views;
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
using WPFControls.Controls.Models;
using WPFControls.Charts.Models;
using WPFControls.Controls.Services;
using WPFControls.Controls.Views.Charts;

namespace WPFControls.Controls.Views.BulletinBoard
{
    /// <summary>
    /// YieldStatisticsView.xaml 的交互逻辑
    /// </summary>
    public partial class YieldStatisticsView : UserControl
    {

        /// <summary>
        /// 初始化 YieldStatisticsView 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 InitializeComponent；具体条件和先后次序见方法体。
        /// </summary>
        public YieldStatisticsView()
        {
            InitializeComponent();

        }


        public static readonly DependencyProperty AxisXProperty = DependencyProperty.Register
        ("AxisX", typeof(ObservableCollection<string>), typeof(YieldStatisticsView),
         new PropertyMetadata(null, OnValueChanged));

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnValueChanged。
        /// </summary>
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
        ("ItemSource", typeof(ObservableCollection<ChartLineData>), typeof(YieldStatisticsView),
         new PropertyMetadata(null, OnValueChanged_Data));

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnValueChanged_Data。
        /// </summary>
        private static void OnValueChanged_Data(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            YieldStatisticsView view = (YieldStatisticsView)d;
            view.LineCharts.ItemSource = view.ItemSource;

        }


        [Bindable(true)]
        [Category("自定义属性"), Description("折线图数据"), DisplayName("折线图数据")]
        public ObservableCollection<ChartLineData> ItemSource
        {
            get { return (ObservableCollection<ChartLineData>)GetValue(ItemsourceProperty); }
            set { SetValue(ItemsourceProperty, value); }
        }



        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register
        ("Title", typeof(string), typeof(YieldStatisticsView),
        new PropertyMetadata(null, OnTitleValueChanged));

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnTitleValueChanged。
        /// </summary>
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

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnAxisFontSizeChanged。
        /// </summary>
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
