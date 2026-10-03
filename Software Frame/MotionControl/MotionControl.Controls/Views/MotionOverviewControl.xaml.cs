// 【中文阅读说明】
// MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。
// 本文件定义：MotionOverviewControl。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Collections.ObjectModel、System.Windows、System.Windows.Controls、MotionControl.Controls.ViewModels、MotionControl.Core.Abstractions。导入不代表所有类型都会在运行时加载。
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using MotionControl.Controls.ViewModels;
using MotionControl.Core.Abstractions;

namespace MotionControl.Controls.Views;

/// <summary>
/// 多轴总览控件：按 <see cref="IMotionCard.AxisCount"/> 为每轴建立一行 <see cref="AxisViewModel"/> 并轮询刷新。
/// <para>仅依赖 <see cref="IMotionCard"/> 通用契约，不做 Vendor/Device 选择（由宿主决定）。</para>
/// </summary>
public partial class MotionOverviewControl : UserControl
{
    private readonly List<IDisposable> _refreshSubscriptions = [];

    /// <summary>初始化控件。</summary>
    public MotionOverviewControl()
    {
        InitializeComponent();
        Loaded += (_, _) => SubscribeRefresh();
        Unloaded += (_, _) => ReleaseRefresh();
    }

    /// <summary>每轴视图模型集合。</summary>
    public ObservableCollection<AxisViewModel> Axes { get; } = [];

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

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnCardChanged。
    /// 正文中的关键协作调用包括 ReleaseRefresh、Clear、Refresh、Add、SubscribeRefresh；具体条件和先后次序见方法体。
    /// </summary>
    private static void OnCardChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not MotionOverviewControl control)
            return;
        control.ReleaseRefresh();
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
        control.SubscribeRefresh();
    }

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnIntervalChanged。
    /// 正文中的关键协作调用包括 SubscribeRefresh；具体条件和先后次序见方法体。
    /// </summary>
    private static void OnIntervalChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MotionOverviewControl control && e.NewValue is int interval && interval > 0)
            control.SubscribeRefresh();
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 SubscribeRefresh。
    /// 正文中的关键协作调用包括 ReleaseRefresh、Add、ObserveRefresh；具体条件和先后次序见方法体。
    /// </summary>
    private void SubscribeRefresh()
    {
        ReleaseRefresh();
        if (!IsLoaded || RefreshIntervalMs <= 0) return;
        foreach (var axis in Axes)
            _refreshSubscriptions.Add(axis.Workspace.ObserveRefresh(RefreshIntervalMs));
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ReleaseRefresh。
    /// 正文中的关键协作调用包括 Dispose、Clear；具体条件和先后次序见方法体。
    /// </summary>
    private void ReleaseRefresh()
    {
        foreach (var subscription in _refreshSubscriptions) subscription.Dispose();
        _refreshSubscriptions.Clear();
    }

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnTableSizeChanged。
    /// 正文中的关键协作调用包括 Sum、Take、Max；具体条件和先后次序见方法体。
    /// </summary>
    private void OnTableSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is not ListView { View: GridView grid } || grid.Columns.Count == 0) return;
        var fixedWidth = grid.Columns.Take(grid.Columns.Count - 1).Sum(column => column.Width);
        grid.Columns[^1].Width = Math.Max(220,
            e.NewSize.Width - fixedWidth - SystemParameters.VerticalScrollBarWidth - 8);
    }
}
