using System.ComponentModel;
using System.IO;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using Vision.Controls.Models;
using Vision.Controls.ViewModels;

#nullable enable
namespace Vision.Controls.View;

/// <summary>图像显示区与右键操作；顶部操作栏独立，不创建或拥有工具/输入/结果。</summary>
public partial class VisionImagePanel : UserControl
{
    public static readonly DependencyProperty ResultSelectionActiveProperty = DependencyProperty.Register(
        nameof(ResultSelectionActive), typeof(bool), typeof(VisionImagePanel),
        new PropertyMetadata(false, (d, _) => ((VisionImagePanel)d).ApplySelectionContext()));
    public bool ResultSelectionActive { get => (bool)GetValue(ResultSelectionActiveProperty); set => SetValue(ResultSelectionActiveProperty, value); }
    public VisionImageViewport ImageViewport => Viewport;
    public event EventHandler<LineSearchEditedEventArgs>? ParametersRefreshRequested;
    private VisionToolEditorSession? Session => DataContext as VisionToolEditorSession;
    private VisionToolEditorModel? _observedModel;
    private bool ShouldPickResults => ResultSelectionActive && Session?.SelectedImageView != ImageViewKind.Roi;
    public VisionImagePanel()
    {
        InitializeComponent();
        Loaded += (_, _) => ObserveModel(Session?.Model);
        Unloaded += (_, _) => ObserveModel(null);
        DataContextChanged += (_, _) => { if (IsLoaded) ObserveModel(Session?.Model); };
    }
    private void ObserveModel(VisionToolEditorModel? model)
    {
        if (_observedModel != null) _observedModel.PropertyChanged -= DisplayContextChanged;
        _observedModel = model;
        if (model != null) model.PropertyChanged += DisplayContextChanged;
        ApplySelectionContext();
    }
    private void DisplayContextChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(VisionToolEditorModel.SelectedImageView)) ApplySelectionContext();
    }
    private void ApplySelectionContext()
    {
        if (Viewport == null) return;
        if (ShouldPickResults) Viewport.Mode = "结果选择";
        else if (Viewport.Mode == "结果选择") Viewport.Mode = "选择";
    }
    public void SelectInteractionContext(bool focus = false)
    {
        Viewport.Mode = ShouldPickResults ? "结果选择" : "选择";
        if (focus) Viewport.Focus();
    }
    private void SelectCanvas(object sender, RoutedEventArgs e)
    {
        if (Session?.IsSessionActive != false) SelectInteractionContext(focus: true);
    }
    public void BeginRoiDrawing()
    {
        if (Session is not { CanDrawAreaRoi: true } browser) return;
        browser.SelectedImageView = ImageViewKind.Roi;
        browser.SelectOriginalImage();
        browser.ShowRois = true; browser.ShowResults = false;
        Viewport.Mode = browser.RoiEditor.SelectedRoiType;
        Viewport.Focus();
    }
    private void ZoomIn(object sender, RoutedEventArgs e) => Viewport.ZoomIn();
    private void ZoomOut(object sender, RoutedEventArgs e) => Viewport.ZoomOut();
    private void PanImage(object sender, RoutedEventArgs e) { Viewport.Mode = "平移"; Viewport.Focus(); }
    private void ImageMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (Viewport.IsDrawing) { e.Handled = true; return; }
        foreach (var item in Viewport.ContextMenu.Items.OfType<MenuItem>())
            item.IsEnabled = Viewport.CurrentBitmap != null;
    }
    /// <summary>保存当前底图原始像素尺寸，不保存缩放、标注或界面截图。</summary>
    public void SaveImage(string path)
    {
        var bitmap = Viewport.CurrentBitmap ?? throw new InvalidOperationException("当前没有可保存的图像。");
        BitmapEncoder encoder = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => new PngBitmapEncoder(),
            ".tif" or ".tiff" => new TiffBitmapEncoder(),
            _ => throw new NotSupportedException("请选择PNG或TIFF图像格式。")
        };
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
    private void SaveImageAs(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "另存当前显示图像", Filter = "PNG (*.png)|*.png|TIFF (*.tif)|*.tif",
            DefaultExt = ".png", AddExtension = true, OverwritePrompt = true
        };
        if (dialog.ShowDialog() != true) return;
        try { SaveImage(dialog.FileName); }
        catch (Exception error) { MessageBox.Show(error.Message, "保存图像失败", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
    private void CompleteRoiDrawing(object sender, RoutedEventArgs e) { Viewport.CompleteDrawing(); Viewport.Focus(); }
    private void CancelRoiDrawing(object sender, RoutedEventArgs e) { Viewport.CancelDrawing(); Viewport.Focus(); }
    private void FitImage(object sender, RoutedEventArgs e) => Viewport.Fit();
    private void ActualPixels(object sender, RoutedEventArgs e) => Viewport.ActualPixels();
    private void LineSearchEdited(object sender, LineSearchEditedEventArgs e)
    {
        if (Session == null || !ReferenceEquals(e.Parameters, Session.CurrentParameters)) return;
        Session.ParametersChanged();
        ParametersRefreshRequested?.Invoke(this, e);
    }
}
