using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using Vision.Core.Geometry;

#nullable enable
namespace Vision.Controls.View;

/// <summary>统一找线ROI编辑，只借用原DTO；属性分区由元数据声明，不按后端或工具名猜测。</summary>
public partial class LineRoiEditorView : UserControl
{
    public static readonly DependencyProperty ParametersProperty = DependencyProperty.Register(nameof(Parameters),
        typeof(ILineSearchRegion), typeof(LineRoiEditorView), new PropertyMetadata(null, ParametersChanged));
    private static readonly DependencyPropertyKey DescriptionPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(Description), typeof(string), typeof(LineRoiEditorView), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty DescriptionProperty = DescriptionPropertyKey.DependencyProperty;
    public ILineSearchRegion? Parameters { get => (ILineSearchRegion?)GetValue(ParametersProperty); set => SetValue(ParametersProperty, value); }
    public string Description => (string)GetValue(DescriptionProperty);
    public event EventHandler<LineSearchEditedEventArgs>? GeometryEdited;
    public LineRoiEditorView() => InitializeComponent();
    private static void ParametersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (LineRoiEditorView)d;
        view.UpdateDescription();
    }
    private void UpdateDescription()
    {
        SetValue(DescriptionPropertyKey, Parameters is not ILineSearchRegion region ? string.Empty :
            region.SearchKind == LineSearchKind.Calipers
                ? "黄色外框用于统一编辑；青色细框是真实采样卡尺，间隔不代表连续采样。阈值、极性和平滑等仍在参数页。"
                : "连续搜索带内提取边缘并拟合，不生成虚假卡尺；阈值与拟合参数仍在参数页。");
    }
    private void GeometryChanged(object sender, EventArgs e)
    {
        if (Parameters is { } target && sender is VisionPropertyPanel panel && ReferenceEquals(panel.TargetObject, target))
            GeometryEdited?.Invoke(this, new(target, true));
    }
    /// <summary>只刷新现有编辑器的目标值，不重建面板、不写回DTO、不取消正在拖动的会话。</summary>
    public void RefreshValues()
    {
        UpdateDescription();
        foreach (var control in Descendants(GeometryPanel))
        {
            if (control is DecimalUpDown) BindingOperations.GetBindingExpression(control, DecimalUpDown.ValueProperty)?.UpdateTarget();
            else if (control is NumericUpDown) BindingOperations.GetBindingExpression(control, NumericUpDown.ValueProperty)?.UpdateTarget();
        }
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}
