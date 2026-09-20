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
using WPFControl.CustomControl.Models.Charts;
using WPFControl.CustomControl.Views.Charts;
using WPFControl.Resources;


namespace WPFControl.CustomControl.Views.BulletinBoard
{
 
    /// <summary>
    /// ProductionCapacityView.xaml 的交互逻辑
    /// </summary>
    public partial class ProductionCapacityView : UserControl
    {
        private static class Message
        {
            public static readonly string[] 报警时间 = { "报警时间", "AlarmTime" };
            public static readonly string[] 运行时间 = { "运行时间", "RunTime" };
        }

        private bool _isInitialized = false;

        public ProductionCapacityView()
        {
            InitializeComponent();
        }

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

        private static void OnAlarmTimeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ProductionCapacityView view = (ProductionCapacityView)d;

            double alarm = (double)view.GetValue(AlarmTimeProperty);
            double total = (double)view.GetValue(RunTimeProperty);

            view.PieChart.ItemSource = new ObservableCollection<PieData>()
            {
                new  PieData{  SeriesName=LangSwitchEx.GetMessage(Message.报警时间) ,Value=alarm,Color=ColorHelper.Color_Red },
                new  PieData{  SeriesName=LangSwitchEx.GetMessage(Message.运行时间) ,Value=total,Color=ColorHelper.Color_Green}
            };
        }
    }
}
