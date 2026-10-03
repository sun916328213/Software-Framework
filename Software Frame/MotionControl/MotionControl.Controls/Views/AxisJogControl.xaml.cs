// 【中文阅读说明】
// MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。
// 本文件定义：AxisJogControl。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Windows、System.Windows.Controls、System.Windows.Input、MotionControl.Controls.ViewModels、MotionControl.Core.Abstractions、MotionControl.Core.Definitions。导入不代表所有类型都会在运行时加载。
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MotionControl.Controls.ViewModels;
using MotionControl.Core.Abstractions;
using MotionControl.Core.Definitions;

namespace MotionControl.Controls.Views;

/// <summary>
/// 单轴点动控件：Jog+/Jog-（按住运行、松开停止）、速度设置。
/// <para>仅依赖 <see cref="IMotionCard"/> 通用契约，不承载运动控制实现。</para>
/// </summary>
public partial class AxisJogControl : UserControl
{
    private AxisViewModel? _jogAxis;
    /// <summary>初始化控件。</summary>
    public AxisJogControl()
    {
        InitializeComponent();
        ViewModel = new AxisViewModel(null, 0);
        Unloaded += (_, _) => StopJog();
    }

    /// <summary>绑定的视图模型（随 MotionCard/AxisIndex 重建）。</summary>
    public AxisViewModel? ViewModel
    {
        get => (AxisViewModel?)GetValue(ViewModelProperty);
        private set => SetValue(ViewModelProperty, value);
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

    /// <summary>视图模型依赖属性。</summary>
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(AxisViewModel), typeof(AxisJogControl),
            new PropertyMetadata(null));

    /// <summary>运动控制卡依赖属性。</summary>
    public static readonly DependencyProperty MotionCardProperty =
        DependencyProperty.Register(nameof(MotionCard), typeof(IMotionCard), typeof(AxisJogControl),
            new PropertyMetadata(null, OnBindingChanged));

    /// <summary>轴索引依赖属性。</summary>
    public static readonly DependencyProperty AxisIndexProperty =
        DependencyProperty.Register(nameof(AxisIndex), typeof(int), typeof(AxisJogControl),
            new PropertyMetadata(0, OnBindingChanged));

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnBindingChanged。
    /// 正文中的关键协作调用包括 StopJog；具体条件和先后次序见方法体。
    /// </summary>
    private static void OnBindingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AxisJogControl control)
        {
            control.StopJog();
            control.ViewModel = new AxisViewModel(control.MotionCard, control.AxisIndex);
        }
    }

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnJogDown。
    /// 正文中的关键协作调用包括 Equals、CanExecute、StopJog、CaptureMouse、Execute；具体条件和先后次序见方法体。
    /// </summary>
    private void OnJogDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Button button || ViewModel is null)
            return;
        var direction = Equals(button.Tag, "Negative") ? EDirection.Negative : EDirection.Positive;
        if (!ViewModel.JogStartCommand.CanExecute(direction)) return;
        StopJog();
        _jogAxis = ViewModel;
        button.CaptureMouse();
        _jogAxis.JogStartCommand.Execute(direction);
        e.Handled = true;
    }

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnJogUp。
    /// 正文中的关键协作调用包括 StopJog、ReleaseMouseCapture；具体条件和先后次序见方法体。
    /// </summary>
    private void OnJogUp(object sender, EventArgs e)
    {
        StopJog();
        if (sender is Button button && button.IsMouseCaptured)
            button.ReleaseMouseCapture();
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 StopJog。
    /// 正文中的关键协作调用包括 Execute；具体条件和先后次序见方法体。
    /// </summary>
    private void StopJog()
    {
        var axis = _jogAxis;
        _jogAxis = null;
        axis?.StopCommand.Execute(null);
    }
}
