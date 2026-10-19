// 【中文阅读说明】
// WPFControls.Charts 模块：图表组件，本文件是 LiveCharts 折线图控件，负责依赖属性、刷新入口、UI 线程与订阅生命周期。
// 本文件定义：ChartsLineView。数据转换交给 ChartsLineService，控件不覆盖宿主 DataContext。
using LiveChartsCore.SkiaSharpView.WPF;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using WPFControls.Charts.Models;
using WPFControls.Controls.Services.Charts;

namespace WPFControls.Controls.Views.Charts
{
    /// <summary>
    /// ChartsLineView.xaml 的交互逻辑。
    /// </summary>
    public partial class ChartsLineView : CartesianChart
    {
        private readonly ChartsLineService chartsLineService = new ChartsLineService();

        // 当前已订阅的集合；null 表示没有订阅。用于避免重复订阅和遗留订阅。
        private ObservableCollection<ChartLineData>? subscribedSource;

        // 卸载期间不保持订阅，重新加载时再恢复。
        private bool isLoaded;

        /// <summary>
        /// 初始化 ChartsLineView 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public ChartsLineView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        public static readonly DependencyProperty AxisXProperty = DependencyProperty.Register
        ("AxisX", typeof(ObservableCollection<string>), typeof(ChartsLineView),
         new PropertyMetadata(null, OnValueChanged));

        /// <summary>
        /// 坐标标签源被替换时刷新；null 视为空标签。
        /// </summary>
        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ChartsLineView view = (ChartsLineView)d;
            view.chartsLineService.SetAxisX(view.AxisX?.ToList() ?? new List<string>());
            view.chats_CartesianChart.XAxes = view.chartsLineService.GetAxisX();
            WPFControls.Charts.Themes.ChartTheme.Apply(view);
        }

        [Bindable(true)]
        [Category("自定义属性"), Description("折线图X轴信息"), DisplayName("折线图X轴信息")]
        public ObservableCollection<string> AxisX
        {
            get { return (ObservableCollection<string>)GetValue(AxisXProperty); }
            set { SetValue(AxisXProperty, value); }
        }

        public static readonly DependencyProperty ItemSourceProperty = DependencyProperty.Register
        ("ItemSource", typeof(ObservableCollection<ChartLineData>), typeof(ChartsLineView),
         new PropertyMetadata(null, OnValueChanged_Data));

        /// <summary>
        /// 数据源被替换时解除旧订阅、订阅新源并刷新；null 表示清空显示。
        /// </summary>
        private static void OnValueChanged_Data(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ChartsLineView view = (ChartsLineView)d;
            view.AttachCollection(view.ItemSource);
            view.RefreshChart();
        }

        /// <summary>
        /// 集合 Add / Remove / Clear 时触发。
        /// </summary>
        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RefreshChart();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            isLoaded = true;
            AttachCollection(ItemSource);
            RefreshChart();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            isLoaded = false;
            AttachCollection(null);
        }

        /// <summary>
        /// 订阅目标集合：同一实例不重复订阅，替换时先解除旧订阅，未加载时不保持订阅。
        /// </summary>
        private void AttachCollection(ObservableCollection<ChartLineData>? source)
        {
            if (ReferenceEquals(subscribedSource, source)) return;

            if (subscribedSource != null)
                subscribedSource.CollectionChanged -= OnCollectionChanged;

            subscribedSource = isLoaded ? source : null;

            if (subscribedSource != null)
                subscribedSource.CollectionChanged += OnCollectionChanged;
        }

        /// <summary>
        /// 显式刷新入口：替换数据源后会自动调用；原地修改输入数据时由宿主在 UI 线程调用。
        /// 数据源为 null 时清空显示，不保留旧图形。
        /// </summary>
        public void RefreshChart()
        {
            VerifyAccess();
            chats_CartesianChart.Series = chartsLineService.ShowData(ItemSource?.ToArray() ?? Array.Empty<ChartLineData>());
            WPFControls.Charts.Themes.ChartTheme.Apply(this);
        }

        [Bindable(true)]
        [Category("自定义属性"), Description("折线图数据"), DisplayName("折线图数据")]
        public ObservableCollection<ChartLineData> ItemSource
        {
            get { return (ObservableCollection<ChartLineData>)GetValue(ItemSourceProperty); }
            set { SetValue(ItemSourceProperty, value); }
        }

        public static readonly DependencyProperty AxisFontSizeProperty = DependencyProperty.Register(
          nameof(AxisFontSize), typeof(double), typeof(ChartsLineView),
          new PropertyMetadata(14.0, OnAxisFontSizeChanged));

        /// <summary>
        /// 样式依赖属性变化时立即应用并刷新。
        /// </summary>
        private static void OnAxisFontSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var view = (ChartsLineView)d;
            view.chartsLineService.SetAxisFontSize(view.AxisFontSize);
            // 重新赋值 XAxes 以触发图表刷新（LiveCharts 会响应内部 TextSize 属性变更）
            view.chats_CartesianChart.XAxes = view.chartsLineService.GetAxisX();
            WPFControls.Charts.Themes.ChartTheme.Apply(view);
        }

        /// <summary>
        /// X轴标签字体大小。
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
