using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using MotionControl.Controls.ViewModels;
using MotionControl.Core.Abstractions;

namespace MotionControl.Controls.Views;

/// <summary>
/// 多轴总览控件：按 <see cref="IMotionCard.AxisCount"/> 为每轴建立一行 <see cref="AxisViewModel"/> 并轮询刷新。
/// <para>仅依赖 <see cref="IMotionCard"/> 通用契约，不做 Vendor/Device 选择（由宿主决定）。</para>
/// </summary>
public partial class MotionOverviewControl : UserControl
{
    private readonly DispatcherTimer _timer;

    /// <summary>初始化控件。</summary>
    public MotionOverviewControl()
    {
        InitializeComponent();
        DataContext = this;
        Axes = new ObservableCollection<AxisViewModel>();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(RefreshIntervalMs) };
        _timer.Tick += (_, _) =>
        {
            foreach (var axis in Axes)
                axis.Refresh();
        };
        Loaded += (_, _) => _timer.Start();
        Unloaded += (_, _) => _timer.Stop();
    }

    /// <summary>每轴视图模型集合。</summary>
    public ObservableCollection<AxisViewModel> Axes { get; }

    /// <summary>运动控制卡。</summary>
    public IMotionCard? MotionCard
    {
        get => (IMotionCard?)GetValue(MotionCardProperty);
        set => SetValue(MotionCardProperty, value);
    }

    /// <summary>状态轮询间隔（毫秒）。</summary>
    public int RefreshIntervalMs
    {
        get => (int)GetValue(RefreshIntervalMsProperty);
        set => SetValue(RefreshIntervalMsProperty, value);
    }

    /// <summary>运动控制卡依赖属性。</summary>
    public static readonly DependencyProperty MotionCardProperty =
        DependencyProperty.Register(nameof(MotionCard), typeof(IMotionCard), typeof(MotionOverviewControl),
            new PropertyMetadata(null, OnCardChanged));

    /// <summary>轮询间隔依赖属性。</summary>
    public static readonly DependencyProperty RefreshIntervalMsProperty =
        DependencyProperty.Register(nameof(RefreshIntervalMs), typeof(int), typeof(MotionOverviewControl),
            new PropertyMetadata(500, OnIntervalChanged));

    private static void OnCardChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not MotionOverviewControl control)
            return;
        control.Axes.Clear();
        if (e.NewValue is IMotionCard card)
        {
            for (var axis = 0; axis < card.AxisCount; axis++)
            {
                var viewModel = new AxisViewModel(card, axis);
                viewModel.Refresh();
                control.Axes.Add(viewModel);
            }
        }
    }

    private static void OnIntervalChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MotionOverviewControl control && e.NewValue is int interval && interval > 0)
            control._timer.Interval = TimeSpan.FromMilliseconds(interval);
    }
}
