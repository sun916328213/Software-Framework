using System.Globalization;
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
    /// <summary>初始化控件。</summary>
    public AxisJogControl()
    {
        InitializeComponent();
        DataContext = this;
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

    private static void OnBindingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AxisJogControl control)
            control.ViewModel = new AxisViewModel(control.MotionCard, control.AxisIndex);
    }

    private void OnApplySpeed(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
            return;
        if (!double.TryParse(SpeedBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var velocity))
        {
            ViewModel.Refresh();
            return;
        }
        ViewModel.SetSpeed(velocity);
    }

    private void OnJogDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Button button || ViewModel is null)
            return;
        var direction = Equals(button.Tag, "Negative") ? EDirection.Negative : EDirection.Positive;
        button.CaptureMouse();
        ViewModel.JogStart(direction);
        e.Handled = true;
    }

    private void OnJogUp(object sender, EventArgs e)
    {
        if (sender is Button button && button.IsMouseCaptured)
            button.ReleaseMouseCapture();
        ViewModel?.Stop();
    }
}
