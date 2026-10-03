// 【中文阅读说明】
// MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。
// 本文件定义：AxisStatusControl。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Windows、System.Windows.Controls、MotionControl.Controls.ViewModels、MotionControl.Core.Abstractions。导入不代表所有类型都会在运行时加载。
using System.Windows;
using System.Windows.Controls;
using MotionControl.Controls.ViewModels;
using MotionControl.Core.Abstractions;

namespace MotionControl.Controls.Views;

/// <summary>
/// 单轴状态控件：位置、Servo、Alarm、Home、Limit 等状态指示。
/// <para>卡/轴依赖属性为兼容入口，Workspace 管理状态及刷新。</para>
/// </summary>
public partial class AxisStatusControl : UserControl
{
    private IDisposable? _refreshSubscription;

    /// <summary>初始化控件。</summary>
    public AxisStatusControl()
    {
        InitializeComponent();
        ViewModel = new AxisViewModel(null, 0);
        Loaded += (_, _) => SubscribeRefresh();
        Unloaded += (_, _) => ReleaseRefresh();
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

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnBindingChanged。
    /// 正文中的关键协作调用包括 Refresh、SubscribeRefresh；具体条件和先后次序见方法体。
    /// </summary>
    private static void OnBindingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AxisStatusControl control)
        {
            control.ViewModel = new AxisViewModel(control.MotionCard, control.AxisIndex);
            control.ViewModel.Refresh();
            control.SubscribeRefresh();
        }
    }

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnIntervalChanged。
    /// 正文中的关键协作调用包括 SubscribeRefresh；具体条件和先后次序见方法体。
    /// </summary>
    private static void OnIntervalChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AxisStatusControl control && e.NewValue is int interval && interval > 0)
            control.SubscribeRefresh();
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 SubscribeRefresh。
    /// 正文中的关键协作调用包括 ReleaseRefresh、ObserveRefresh；具体条件和先后次序见方法体。
    /// </summary>
    private void SubscribeRefresh()
    {
        ReleaseRefresh();
        if (IsLoaded && MotionCard is not null && RefreshIntervalMs > 0)
            _refreshSubscription = ViewModel?.Workspace.ObserveRefresh(RefreshIntervalMs);
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ReleaseRefresh。
    /// </summary>
    private void ReleaseRefresh()
    {
        _refreshSubscription?.Dispose();
        _refreshSubscription = null;
    }
}
