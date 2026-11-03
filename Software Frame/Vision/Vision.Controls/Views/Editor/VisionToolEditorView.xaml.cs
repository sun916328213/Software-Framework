using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Vision.Controls.Models;
using Vision.Controls.ViewModels;

#nullable enable
namespace Vision.Controls.View;

/// <summary>单工具编辑界面：五项操作固定显示，借用Session及可选工程命令，不构造目录或工具。</summary>
public partial class VisionToolEditorView : UserControl
{
    private VisionToolEditorModel? _observedModel;
    public static readonly DependencyProperty SessionProperty = DependencyProperty.Register(nameof(Session),
        typeof(VisionToolEditorSession), typeof(VisionToolEditorView), new PropertyMetadata(null, SessionChanged));
    public VisionToolEditorSession? Session { get => (VisionToolEditorSession?)GetValue(SessionProperty); set => SetValue(SessionProperty, value); }
    public static readonly DependencyProperty ToolbarAccessoryProperty = DependencyProperty.Register(nameof(ToolbarAccessory),
        typeof(object), typeof(VisionToolEditorView), new PropertyMetadata(null));
    public object? ToolbarAccessory { get => GetValue(ToolbarAccessoryProperty); set => SetValue(ToolbarAccessoryProperty, value); }
    public VisionImagePanel ImagePanel => ImageArea;
    public VisionImageToolbarView ImageToolbar => ImageToolbarArea;
    public ToolDetailsView DetailsPanel => DetailsArea;
    public VisionToolEditorView()
    {
        InitializeComponent();
        SetBinding(SessionProperty, new Binding { Mode = BindingMode.OneWay });
        Loaded += (_, _) => ObserveModel(Session?.Model);
        Unloaded += (_, _) => ObserveModel(null);
    }
    private static void SessionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (VisionToolEditorView)d;
        if (view.IsLoaded) view.ObserveModel(view.Session?.Model);
    }
    private void ObserveModel(VisionToolEditorModel? model)
    {
        if (_observedModel != null) _observedModel.PropertyChanged -= ParametersChanged;
        _observedModel = model;
        if (model != null) model.PropertyChanged += ParametersChanged;
        ImageArea.ImageViewport.RefreshLineSearch(); // 切节点/卸载后重载不沿用上一目标的手势。
    }
    private void ParametersChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(VisionToolEditorModel.CurrentParameters)) ImageArea.SelectInteractionContext();
    }
    private void DrawRoiRequested(object? sender, EventArgs e) => ImageArea.BeginRoiDrawing();
    private void ResultPickingRequested(object? sender, EventArgs e) => ImageArea.SelectInteractionContext();
    private void ParametersEditing(object? sender, EventArgs e) => ImageArea.ImageViewport.RefreshLineSearch();
    private void ParametersRefreshRequested(object? sender, LineSearchEditedEventArgs e) => DetailsArea.RefreshParameterEditor(e.Parameters, e.Finished);
}
