using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Vision.Controls.Models;
using Vision.Controls.ViewModels;

#nullable enable
namespace Vision.Controls.View;

/// <summary>参数、ROI、结果、模型/模板页；只管理视图订阅，不拥有会话。</summary>
public partial class ToolDetailsView : UserControl
{
    private VisionToolEditorModel? _observedModel;
    private static readonly DependencyPropertyKey IsResultsSelectedPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(IsResultsSelected), typeof(bool), typeof(ToolDetailsView), new PropertyMetadata(false));
    public static readonly DependencyProperty IsResultsSelectedProperty = IsResultsSelectedPropertyKey.DependencyProperty;
    public bool IsResultsSelected => (bool)GetValue(IsResultsSelectedProperty);
    public event EventHandler? DrawRoiRequested;
    public event EventHandler? ParametersEditing;
    public event EventHandler? ResultPickingRequested;
    private VisionToolEditorSession? Session => DataContext as VisionToolEditorSession;
    public ToolDetailsView()
    {
        InitializeComponent();
        Loaded += (_, _) => ObserveModel(Session?.Model);
        Unloaded += (_, _) => ObserveModel(null);
        DataContextChanged += (_, _) => { if (IsLoaded) ObserveModel(Session?.Model); };
    }
    private void ObserveModel(VisionToolEditorModel? model)
    {
        if (_observedModel != null) _observedModel.PropertyChanged -= OnCapabilitiesChanged;
        _observedModel = model;
        if (model != null) model.PropertyChanged += OnCapabilitiesChanged;
        EnsureAvailableTab();
    }
    private void OnCapabilitiesChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(VisionToolEditorModel.SupportsProvider) or
            nameof(VisionToolEditorModel.SupportsModelLoading) or nameof(VisionToolEditorModel.SupportsTemplateInput))
            EnsureAvailableTab();
    }
    private void EnsureAvailableTab()
    {
        if (ParametersTab == null) return;
        if ((TemplateTab?.IsSelected == true && Session?.SupportsTemplateInput != true) ||
            (ModelTab?.IsSelected == true && Session?.SupportsModelPanel != true))
            ParametersTab.IsSelected = true;
    }
    private void DrawSelectedRoi(object sender, RoutedEventArgs e) => DrawRoiRequested?.Invoke(this, EventArgs.Empty);
    public void RefreshParameterEditor(object target, bool finished = true)
    {
        if (!ReferenceEquals(target, Session?.CurrentParameters)) return;
        if (finished && ParametersPanel.IsLoaded) ParametersPanel.Refresh();
        LineRegionEditor.RefreshValues();
    }
    private void LineRoiGeometryEdited(object? sender, LineSearchEditedEventArgs e)
    {
        if (Session is not { } browser || !ReferenceEquals(e.Parameters, browser.LineSearch)) return;
        browser.SelectedImageView = ImageViewKind.Roi;
        browser.SelectOriginalImage();
        ParametersEditing?.Invoke(this, EventArgs.Empty);
        browser.ParametersChanged();
    }
    private void DetailsTabChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ReferenceEquals(e.OriginalSource, sender) || ResultsTab == null) return;
        SetValue(IsResultsSelectedPropertyKey, ResultsTab.IsSelected);
        if (RoiTab.IsSelected && Session is { HasLineSearch: true } browser)
        {
            browser.SelectedImageView = ImageViewKind.Roi;
            browser.SelectOriginalImage();
        }
    }
    private void ResultSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is DataGrid grid && grid.SelectedItem != null)
        {
            if (ResultsTab.IsSelected && ReferenceEquals(grid.DataContext, Session?.CurrentCard))
                ResultPickingRequested?.Invoke(this, EventArgs.Empty);
            grid.ScrollIntoView(grid.SelectedItem);
        }
    }
    private void ResultKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && sender is DataGrid { DataContext: ToolCardViewModel card })
        {
            card.Results.SelectId(null); e.Handled = true;
        }
    }
    private void TemplateTrainingParametersChanged(object sender, EventArgs e)
    {
        if (sender is VisionPropertyPanel { Tag: TemplateEditorViewModel editor }) editor.TrainingParametersChanged();
    }
    private void MetadataParametersChanged(object sender, EventArgs e)
    {
        if (sender is VisionPropertyPanel panel && Session is { } browser &&
            ReferenceEquals(panel.TargetObject, browser.ParameterEditor))
        {
            ParametersEditing?.Invoke(this, EventArgs.Empty);
            LineRegionEditor.RefreshValues();
            browser.ParametersChanged();
        }
    }
}
