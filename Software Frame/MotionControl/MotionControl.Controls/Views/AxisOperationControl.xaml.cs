// 【中文阅读说明】
// MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。
// 本文件定义：AxisOperationControl。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Windows、System.Windows.Controls、MotionControl.Controls.ViewModels、MotionControl.Core.Abstractions、MotionControl.Core.Definitions。导入不代表所有类型都会在运行时加载。
using System.Windows;
using System.Windows.Controls;
using MotionControl.Controls.ViewModels;
using MotionControl.Core.Abstractions;
using MotionControl.Core.Definitions;

namespace MotionControl.Controls.Views;

/// <summary>
/// 单轴基础操作控件：Servo On/Off、回零、清除报警、停止。
/// <para>仅依赖 <see cref="IMotionCard"/> 通用契约，不承载运动控制实现。</para>
/// </summary>
public partial class AxisOperationControl : UserControl
{
    /// <summary>初始化控件。</summary>
    public AxisOperationControl()
    {
        InitializeComponent();
        ViewModel = new AxisViewModel(null, 0);
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
        DependencyProperty.Register(nameof(ViewModel), typeof(AxisViewModel), typeof(AxisOperationControl),
            new PropertyMetadata(null));

    /// <summary>运动控制卡依赖属性。</summary>
    public static readonly DependencyProperty MotionCardProperty =
        DependencyProperty.Register(nameof(MotionCard), typeof(IMotionCard), typeof(AxisOperationControl),
            new PropertyMetadata(null, OnBindingChanged));

    /// <summary>轴索引依赖属性。</summary>
    public static readonly DependencyProperty AxisIndexProperty =
        DependencyProperty.Register(nameof(AxisIndex), typeof(int), typeof(AxisOperationControl),
            new PropertyMetadata(0, OnBindingChanged));

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnBindingChanged。
    /// </summary>
    private static void OnBindingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AxisOperationControl control)
            control.ViewModel = new AxisViewModel(control.MotionCard, control.AxisIndex);
    }

}
