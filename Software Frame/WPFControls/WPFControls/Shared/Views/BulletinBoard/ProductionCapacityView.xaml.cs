// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：ProductionCapacityView、Message。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、WPFControls.Controls.Views、Extension.UI.Controls、LiveChartsCore、System、System.Collections.Generic。导入不代表所有类型都会在运行时加载。
using WPFControls;
using WPFControls.Controls.Views;
using Extension.UI.Controls;
using LiveChartsCore;
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
using WPFControls.Charts.Models;
using WPFControls.Controls.Views.Charts;
using WPFControls.Controls.Resources;


namespace WPFControls.Controls.Views.BulletinBoard
{
 
    /// <summary>
    /// ProductionCapacityView.xaml 的交互逻辑
    /// </summary>
    public partial class ProductionCapacityView : UserControl
    {
        /// <summary>
        /// Message 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
        /// </summary>
        private static class Message
        {
            public static readonly string[] 报警时间 = { "报警时间", "AlarmTime" };
            public static readonly string[] 运行时间 = { "运行时间", "RunTime" };
        }

        private bool _isInitialized = false;

        /// <summary>
        /// 初始化 ProductionCapacityView 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 InitializeComponent；具体条件和先后次序见方法体。
        /// </summary>
        public ProductionCapacityView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ProductionCapacityView_Loaded。
        /// </summary>
        private void ProductionCapacityView_Loaded(object sender, RoutedEventArgs e)
        {
            _isInitialized = true;
        }

        public static readonly DependencyProperty OKCountProperty = DependencyProperty.Register
         ("OKCount", typeof(int), typeof(ProductionCapacityView), new PropertyMetadata(0, OnOKCountChanged));


        [Bindable(true)]
        [Category("自定义属性"), Description("OK数量"), DisplayName("OK数量")]
        public int OKCount
        {
            get { return (int)GetValue(OKCountProperty); }
            set { SetValue(OKCountProperty, value); }
        }


        public static readonly DependencyProperty NGCountProperty = DependencyProperty.Register
        ("NGCount", typeof(int), typeof(ProductionCapacityView), new PropertyMetadata(0, null));

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnOKCountChanged。
        /// 正文中的关键协作调用包括 SetValue；具体条件和先后次序见方法体。
        /// </summary>
        private static void OnOKCountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ProductionCapacityView view = (ProductionCapacityView)d;
            int okCount = view.OKCount;
            int totalCount = view.TotalCount;

            // 更新 NGCount
            view.SetValue(NGCountProperty, totalCount - okCount);

            // 更新 CircleProcessBar 的值
         
                double rate = totalCount > 0 ? (okCount * 100.0 / totalCount) : 0;
                view.TB_OKRate.Value = rate;
 
        }

 

        [Bindable(true)]
        [Category("自定义属性"), Description("NG数量"), DisplayName("NG数量")]
        public int NGCount
        {
            get { return (int)GetValue(NGCountProperty); }
            set { SetValue(NGCountProperty, value); }
        }



        public static readonly DependencyProperty TotalCountProperty = DependencyProperty.Register
         ("TotalCount", typeof(int), typeof(ProductionCapacityView), new PropertyMetadata(0, OnTotalCountChanged));


        [Bindable(true)]
        [Category("自定义属性"), Description("总数量"), DisplayName("总数量")]
        public int TotalCount
        {
            get { return (int)GetValue(TotalCountProperty); }
            set { SetValue(TotalCountProperty, value); }
        }

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnTotalCountChanged。
        /// 正文中的关键协作调用包括 SetValue；具体条件和先后次序见方法体。
        /// </summary>
        private static void OnTotalCountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ProductionCapacityView view = (ProductionCapacityView)d;
            int okCount = view.OKCount;
            int totalCount = view.TotalCount;

            // 更新 NGCount
            view.SetValue(NGCountProperty, totalCount - okCount);

            // 更新 CircleProcessBar 的值
            
                double rate = totalCount > 0 ? (okCount * 100.0 / totalCount) : 0;
                view.TB_OKRate.Value = rate;
           
        }



        public static readonly DependencyProperty AlarmTimeProperty = DependencyProperty.Register
       ("AlarmTime", typeof(double), typeof(ProductionCapacityView), new PropertyMetadata(100.0, OnAlarmTimeChanged));


        [Bindable(true)]
        [Category("自定义属性"), Description("报警时间"), DisplayName("报警时间")]
        public double AlarmTime
        {
            get { return (double)GetValue(AlarmTimeProperty); }
            set { SetValue(AlarmTimeProperty, value); }
        }

        public static readonly DependencyProperty RunTimeProperty = DependencyProperty.Register
   ("RunTime", typeof(double), typeof(ProductionCapacityView), new PropertyMetadata(200.0, OnAlarmTimeChanged));


        [Bindable(true)]
        [Category("自定义属性"), Description("运行时间"), DisplayName("运行时间")]
        public double RunTime
        {
            get { return (double)GetValue(RunTimeProperty); }
            set { SetValue(RunTimeProperty, value); }
        }

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnAlarmTimeChanged。
        /// 正文中的关键协作调用包括 GetValue、GetMessage；具体条件和先后次序见方法体。
        /// </summary>
        private static void OnAlarmTimeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ProductionCapacityView view = (ProductionCapacityView)d;

            double alarm = (double)view.GetValue(AlarmTimeProperty);
            double total = (double)view.GetValue(RunTimeProperty);

            view.PieChart.ItemSource = new ObservableCollection<ChartPieData>()
            {
                new  ChartPieData{  SeriesName=LangSwitchEx.GetMessage(Message.报警时间) ,Value=alarm,Color=ColorHelper.Color_Red },
                new  ChartPieData{  SeriesName=LangSwitchEx.GetMessage(Message.运行时间) ,Value=total,Color=ColorHelper.Color_Green}
            };
        }
    }
}
