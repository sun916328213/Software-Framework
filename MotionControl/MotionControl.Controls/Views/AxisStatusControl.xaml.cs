using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using MotionControl.Controls.ViewModels;
using MotionControl.Core.Abstractions;

namespace MotionControl.Controls.Views;

/// <summary>
/// 单轴状态控件：位置、Servo、Alarm、Home、Limit 等状态指示。
/// <para>仅依赖 <see cref="IMotionCard"/> 通用契约，由内部 DispatcherTimer 轮询刷新。</para>
/// </summary>
public partial class AxisStatusControl : UserControl
{
    private readonly DispatcherTimer _timer;

    /// <summary>初始化控件。</summary>
    public AxisStatusControl()
    {
        InitializeComponent();
        DataContext = this;
        ViewModel = new AxisViewModel(null, 0);
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(RefreshIntervalMs) };
        _timer.Tick += (_, _) => ViewModel?.Refresh();
        Loaded += (_, _) => _timer.Start();
        Unloaded += (_, _) => _timer.Stop();
    }

    /// <summary>绑定的视图模型（随 MotionCard/AxisIndex 重建，依赖属性变更自动刷新绑定）。</summary>
    public AxisViewModel? ViewModel
    {
        get => (AxisViewModel?)GetValue(ViewModelProperty);
        private set => SetValue(ViewModelProperty, value);
    }

    /// <summary>轴标题（默认按轴索引生成）。</summary>
    public string AxisTitle
    {
        get => (string)GetValue(AxisTitleProperty);
        set => SetValue(AxisTitleProperty, value);
    }

    /// <summary>运动控制卡。</summary>
    public IMotionCard? MotionCard
    {
        get => (IMotionCard?)GetValue(MotionCardProperty);
        set => SetValue(MotionCardProperty, value);
    }

    /// <summary>轴索引，从0开始。</summary>
    public int AxisIndex
    {
        get => (int)GetValue(AxisIndexProperty);
        set => SetValue(AxisIndexProperty, value);
    }

    /// <summary>状态轮询间隔（毫秒）。</summary>
    public int RefreshIntervalMs
    {
        get => (int)GetValue(RefreshIntervalMsProperty);
        set => SetValue(RefreshIntervalMsProperty, value);
    }

    /// <summary>视图模型依赖属性。</summary>
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(AxisViewModel), typeof(AxisStatusControl),
            new PropertyMetadata(null));

    /// <summary>轴标题依赖属性。</summary>
    public static readonly DependencyProperty AxisTitleProperty =
        DependencyProperty.Register(nameof(AxisTitle), typeof(string), typeof(AxisStatusControl),
            new PropertyMetadata("轴状态"));

    /// <summary>运动控制卡依赖属性。</summary>
    public static readonly DependencyProperty MotionCardProperty =
        DependencyProperty.Register(nameof(MotionCard), typeof(IMotionCard), typeof(AxisStatusControl),
            new PropertyMetadata(null, OnBindingChanged));

    /// <summary>轴索引依赖属性。</summary>
    public static readonly DependencyProperty AxisIndexProperty =
        DependencyProperty.Register(nameof(AxisIndex), typeof(int), typeof(AxisStatusControl),
            new PropertyMetadata(0, OnBindingChanged));

    /// <summary>轮询间隔依赖属性。</summary>
    public static readonly DependencyProperty RefreshIntervalMsProperty =
        DependencyProperty.Register(nameof(RefreshIntervalMs), typeof(int), typeof(AxisStatusControl),
            new PropertyMetadata(200, OnIntervalChanged));

    private static void OnBindingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AxisStatusControl control)
        {
            control.ViewModel = new AxisViewModel(control.MotionCard, control.AxisIndex);
            control.ViewModel.Refresh();
        }
    }

    private static void OnIntervalChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AxisStatusControl control && e.NewValue is int interval && interval > 0)
            control._timer.Interval = TimeSpan.FromMilliseconds(interval);
    }
}
