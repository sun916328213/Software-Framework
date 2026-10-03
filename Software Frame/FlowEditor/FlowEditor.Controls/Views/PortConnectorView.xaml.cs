// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：PortConnectorView。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、System.Windows、System.Windows.Controls、System.Windows.Media.Animation、FlowEditor.Controls.Models、FlowEditor.Core.Models。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using FlowEditor.Controls.Models;
using FlowEditor.Core.Models;

namespace FlowEditor.Controls.Views;

/// <summary>
/// 可复用端口视觉模块。封装命中面积、放大反馈和候选连接状态。
/// </summary>
public partial class PortConnectorView : UserControl
{
    private const double NormalScale = 1.0;
    private const double EmphasizedScale = 1.7;

    /// <summary>标识端口模型依赖属性。</summary>
    public static readonly DependencyProperty PortProperty = DependencyProperty.Register(
        nameof(Port), typeof(PortModel), typeof(PortConnectorView), new PropertyMetadata(null));

    /// <summary>标识当前端口是否为有效连线候选的依赖属性。</summary>
    public static readonly DependencyProperty IsConnectionCandidateProperty = DependencyProperty.Register(
        nameof(IsConnectionCandidate), typeof(bool), typeof(PortConnectorView),
        new PropertyMetadata(false, OnVisualStateChanged));

    /// <summary>当前视觉对象呈现的端口模型。</summary>
    public PortModel? Port
    {
        get => (PortModel?)GetValue(PortProperty);
        set => SetValue(PortProperty, value);
    }

    /// <summary>当前端口是否应以候选连线状态强调显示。</summary>
    public bool IsConnectionCandidate
    {
        get => (bool)GetValue(IsConnectionCandidateProperty);
        set => SetValue(IsConnectionCandidateProperty, value);
    }

    /// <summary>初始化端口悬停、捕获和候选状态的缩放反馈。</summary>
    public PortConnectorView()
    {
        InitializeComponent();
        MouseEnter += (_, _) => UpdateScale();
        MouseLeave += (_, _) => UpdateScale();
        IsMouseCaptureWithinChanged += (_, _) => UpdateScale();
    }

    /// <summary>依赖属性状态变化时刷新连接器视觉比例。</summary>
    private static void OnVisualStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((PortConnectorView)d).UpdateScale();

    /// <summary>根据悬停、鼠标捕获和候选连接状态播放缩放动画。</summary>
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
