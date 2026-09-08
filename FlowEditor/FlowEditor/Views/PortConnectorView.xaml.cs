using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using FlowEditor.Models;

namespace FlowEditor.Views;

/// <summary>
/// 可复用端口视觉模块。封装命中面积、放大反馈和候选连接状态。
/// </summary>
public partial class PortConnectorView : UserControl
{
    private const double NormalScale = 1.0;
    private const double EmphasizedScale = 1.7;

    public static readonly DependencyProperty PortProperty = DependencyProperty.Register(
        nameof(Port), typeof(PortModel), typeof(PortConnectorView), new PropertyMetadata(null));

    public static readonly DependencyProperty IsConnectionCandidateProperty = DependencyProperty.Register(
        nameof(IsConnectionCandidate), typeof(bool), typeof(PortConnectorView),
        new PropertyMetadata(false, OnVisualStateChanged));

    public PortModel? Port
    {
        get => (PortModel?)GetValue(PortProperty);
        set => SetValue(PortProperty, value);
    }

    public bool IsConnectionCandidate
    {
        get => (bool)GetValue(IsConnectionCandidateProperty);
        set => SetValue(IsConnectionCandidateProperty, value);
    }

    public PortConnectorView()
    {
        InitializeComponent();
        MouseEnter += (_, _) => UpdateScale();
        MouseLeave += (_, _) => UpdateScale();
        IsMouseCaptureWithinChanged += (_, _) => UpdateScale();
    }

    private static void OnVisualStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((PortConnectorView)d).UpdateScale();

    private void UpdateScale()
    {
        if (!IsLoaded)
            return;

        var target = IsMouseOver || IsMouseCaptureWithin || IsConnectionCandidate
            ? EmphasizedScale
            : NormalScale;
        var duration = TimeSpan.FromMilliseconds(110);
        IndicatorScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty,
            new DoubleAnimation(target, duration) { EasingFunction = new QuadraticEase() });
        IndicatorScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty,
            new DoubleAnimation(target, duration) { EasingFunction = new QuadraticEase() });
    }
}
