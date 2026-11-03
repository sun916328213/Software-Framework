using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Vision.Controls.Presentation;
using Vision.Controls.ViewModels;
using Vision.Core.Imaging;
using Vision.Core.Results;

#nullable enable
namespace Vision.Controls.View;

/// <summary>离线图像画布。所有交互使用图像像素坐标；页面卸载仅解绑显示订阅。</summary>
public sealed partial class VisionImageViewport : FrameworkElement
{
    public static readonly DependencyProperty ImageProperty = DependencyProperty.Register(nameof(Image), typeof(VisionImage), typeof(VisionImageViewport), new FrameworkPropertyMetadata(null, Changed));
    public static readonly DependencyProperty ResultProperty = DependencyProperty.Register(nameof(Result), typeof(ToolResult), typeof(VisionImageViewport), new FrameworkPropertyMetadata(null, Changed));
    public static readonly DependencyProperty EditorProperty = DependencyProperty.Register(nameof(Editor), typeof(RoiEditorViewModel), typeof(VisionImageViewport), new FrameworkPropertyMetadata(null, EditorChanged));
    // ShowOutput 只选择底图；ROI 和结果叠加层独立控制。
    public static readonly DependencyProperty ShowOutputProperty = DependencyProperty.Register(nameof(ShowOutput), typeof(bool), typeof(VisionImageViewport), new FrameworkPropertyMetadata(true, Changed));
    public static readonly DependencyProperty TrainingImageProperty = DependencyProperty.Register(nameof(TrainingImage), typeof(VisionImage), typeof(VisionImageViewport), new FrameworkPropertyMetadata(null, Changed));
    public static readonly DependencyProperty ShowTrainingProperty = DependencyProperty.Register(nameof(ShowTraining), typeof(bool), typeof(VisionImageViewport), new FrameworkPropertyMetadata(false, Changed));
    public VisionImage? TrainingImage { get => (VisionImage?)GetValue(TrainingImageProperty); set => SetValue(TrainingImageProperty, value); }
    public bool ShowTraining { get => (bool)GetValue(ShowTrainingProperty); set => SetValue(ShowTrainingProperty, value); }
    public static readonly DependencyProperty ShowRoiImageProperty = DependencyProperty.Register(nameof(ShowRoiImage), typeof(bool), typeof(VisionImageViewport), new FrameworkPropertyMetadata(false, Changed));
    public static readonly DependencyProperty ShowRoisProperty = DependencyProperty.Register(nameof(ShowRois), typeof(bool), typeof(VisionImageViewport),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender, RoiVisibilityChanged));
    public static readonly DependencyProperty ShowResultsProperty = DependencyProperty.Register(nameof(ShowResults), typeof(bool), typeof(VisionImageViewport),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty ResultsProperty = DependencyProperty.Register(nameof(Results), typeof(ResultSelectionViewModel), typeof(VisionImageViewport), new FrameworkPropertyMetadata(null, ResultsChanged));
    public static readonly DependencyProperty ShowResultBoundsProperty = DependencyProperty.Register(nameof(ShowResultBounds), typeof(bool), typeof(VisionImageViewport), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty ShowResultCentersProperty = DependencyProperty.Register(nameof(ShowResultCenters), typeof(bool), typeof(VisionImageViewport), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
    private static readonly DependencyPropertyKey StatusPropertyKey = DependencyProperty.RegisterReadOnly(nameof(Status), typeof(string), typeof(VisionImageViewport), new PropertyMetadata("加载图像开始"));
    public static readonly DependencyProperty StatusProperty = StatusPropertyKey.DependencyProperty;
    public VisionImage? Image { get => (VisionImage?)GetValue(ImageProperty); set => SetValue(ImageProperty, value); }
    public ToolResult? Result { get => (ToolResult?)GetValue(ResultProperty); set => SetValue(ResultProperty, value); }
    public RoiEditorViewModel? Editor { get => (RoiEditorViewModel?)GetValue(EditorProperty); set => SetValue(EditorProperty, value); }
    public bool ShowOutput { get => (bool)GetValue(ShowOutputProperty); set => SetValue(ShowOutputProperty, value); }
    public bool ShowRoiImage { get => (bool)GetValue(ShowRoiImageProperty); set => SetValue(ShowRoiImageProperty, value); }
    public bool ShowRois { get => (bool)GetValue(ShowRoisProperty); set => SetValue(ShowRoisProperty, value); }
    public bool ShowResults { get => (bool)GetValue(ShowResultsProperty); set => SetValue(ShowResultsProperty, value); }
    public ResultSelectionViewModel? Results { get => (ResultSelectionViewModel?)GetValue(ResultsProperty); set => SetValue(ResultsProperty, value); }
    public bool ShowResultBounds { get => (bool)GetValue(ShowResultBoundsProperty); set => SetValue(ShowResultBoundsProperty, value); }
    public bool ShowResultCenters { get => (bool)GetValue(ShowResultCentersProperty); set => SetValue(ShowResultCentersProperty, value); }
    public string Status => (string)GetValue(StatusProperty);
    public static readonly DependencyProperty ModeProperty = DependencyProperty.Register(nameof(Mode), typeof(string), typeof(VisionImageViewport),
        new FrameworkPropertyMetadata("选择", FrameworkPropertyMetadataOptions.AffectsRender, ModeChanged));
    private static readonly DependencyPropertyKey IsDrawingPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsDrawing), typeof(bool), typeof(VisionImageViewport), new PropertyMetadata(false));
    public static readonly DependencyProperty IsDrawingProperty = IsDrawingPropertyKey.DependencyProperty;
    private static readonly DependencyPropertyKey DrawingHintPropertyKey = DependencyProperty.RegisterReadOnly(nameof(DrawingHint), typeof(string), typeof(VisionImageViewport), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty DrawingHintProperty = DrawingHintPropertyKey.DependencyProperty;
    public string Mode { get => (string)GetValue(ModeProperty); set => SetValue(ModeProperty, value); }
    public bool IsDrawing => (bool)GetValue(IsDrawingProperty);
    public string DrawingHint => (string)GetValue(DrawingHintProperty);
    private static void ModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (VisionImageViewport)d;
        view.CancelGesture();
        var mode = (string)e.NewValue;
        var drawing = mode is "矩形" or "旋转矩形" or "圆" or "椭圆" or "多边形";
        view.SetValue(IsDrawingPropertyKey, drawing);
        view.SetValue(DrawingHintPropertyKey, !drawing ? string.Empty : mode == "多边形"
            ? "多边形：至少三点，完成 / Enter / 双击 / 右键确认；Esc 取消。"
            : $"{mode}：拖拽后松开即确认，随后可移动/调整；Esc 取消。");
        view.Cursor = mode == "平移" ? Cursors.Hand : drawing ? Cursors.Cross : Cursors.Arrow;
    }
    private BitmapSource? _bitmap;
    private VisionImage? _bitmapImage;
    private VisionImage? _inputBitmapImage;
    private BitmapSource? _inputBitmap;
    private VisionResultDrawing _resultDrawing = new(null);
    public static readonly DependencyProperty IsRoiEditingEnabledProperty = DependencyProperty.Register(nameof(IsRoiEditingEnabled), typeof(bool), typeof(VisionImageViewport),
        new FrameworkPropertyMetadata(true, RoiVisibilityChanged));
    public bool IsRoiEditingEnabled { get => (bool)GetValue(IsRoiEditingEnabledProperty); set => SetValue(IsRoiEditingEnabledProperty, value); }
    private bool _panning;
    private MouseButton _gestureButton;
    private string? _displayError;
    private string? _inputError;
    private RoiEditorViewModel? _subscribed;
    private ResultSelectionViewModel? _subscribedResults;
    private double _scale = 1;
    private Vector _offset;
    private bool _fit = true;
    private Point? _start;
    private Point _last;
    private Rect? _draft;
    private RoiEditorViewModel? _gestureEditor;
    private bool CanEditImage => !ShowTraining && Image != null && DisplayImage?.Width == Image.Width && DisplayImage.Height == Image.Height;
    private VisionImage? DisplayImage => ShowTraining ? TrainingImage : ShowRoiImage ? VisionDisplay.RoiImage(Result) ?? Image : ShowOutput ? VisionDisplay.OutputImage(Result) ?? Image : Image;

    public VisionImageViewport()
    {
        ClipToBounds = true;
        Focusable = true;
        Cursor = Cursors.Arrow;
        Loaded += (_, _) => { Subscribe(Editor); SubscribeResults(Results); RefreshBitmap(); };
        Unloaded += (_, _) => { Subscribe(null); SubscribeResults(null); CancelGesture(); };
        LostMouseCapture += (_, _) => CancelGesture();
    }

    private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (VisionImageViewport)d;
        if (e.Property == ImageProperty)
        {
            view._inputError = null;
            view.SetValue(StatusPropertyKey, e.NewValue == null ? "加载图像开始" : "图像已加载");
            view._fit = true; view._inputBitmapImage = null; view._inputBitmap = null;
        }
        if (e.Property == ResultProperty)
        {
            view._resultDrawing = new(view.Result);
        }
        // 同一输入坐标系内切换底图或清旧结果，不应取消刚开始的 ROI 手势。
        // 新输入/新结果仍取消；切到局部输出也不能保留原图坐标的手势。
        var displayOnly = e.Property == ShowOutputProperty || e.Property == ShowRoiImageProperty ||
            (e.Property == ResultProperty && e.NewValue == null);
        var preserve = displayOnly && view._bitmap != null && view.CanEditImage &&
            view.Image is { IsEmpty: false } input && view._bitmapImage is { } previous &&
            previous.Width == input.Width && previous.Height == input.Height;
        if (!preserve) view.CancelGesture();
        view.RefreshBitmap(preserveTransform: preserve);
    }
    private static void RoiVisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (!(bool)e.NewValue) ((VisionImageViewport)d).CancelGesture();
    }
    private static void EditorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (VisionImageViewport)d;
        view.CancelGesture();
        if (view.IsLoaded) view.Subscribe(view.Editor);
        view.InvalidateVisual();
    }
    private void Subscribe(RoiEditorViewModel? editor)
    {
        if (_subscribed != null) { _subscribed.PropertyChanged -= RoiChanged; _subscribed.Rois.CollectionChanged -= CollectionChanged; }
        _subscribed = editor;
        if (editor != null) { editor.PropertyChanged += RoiChanged; editor.Rois.CollectionChanged += CollectionChanged; }
    }
    private static void ResultsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (VisionImageViewport)d;
        view.CancelGesture();
        if (view.IsLoaded) view.SubscribeResults(view.Results);
        view.InvalidateVisual();
    }
    private void SubscribeResults(ResultSelectionViewModel? results)
    {
        if (_subscribedResults != null) _subscribedResults.PropertyChanged -= SelectionChanged;
        _subscribedResults = results;
        if (results != null) results.PropertyChanged += SelectionChanged;
    }
    private void SelectionChanged(object? sender, PropertyChangedEventArgs e) => InvalidateVisual();

    /// <summary>结果选择模式的图像坐标入口；鼠标和离线交互测试使用同一路径。</summary>
    public bool SelectResultAt(Core.Geometry.Point2D point, double tolerance = 3)
    {
        if (Mode != "结果选择" || !ShowResults || !CanEditImage || Result?.Success != true ||
            Results == null || !ReferenceEquals(Results.Result, Result)) return false;
        return Results.SelectAt(point, tolerance);
    }
    private void RoiChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_gestureEditor != null && !_gestureEditor.IsManipulating) CancelGesture();
        InvalidateVisual();
    }
    private void CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => InvalidateVisual();
    /// <summary>原生/外部图像转换失败：撤销旧底图和旧结果，沿原错误文字绘制；不释放借用图像/结果。null用于清除诊断。</summary>
    public void ReportImageError(string? message)
    {
        VerifyAccess();
        SetCurrentValue(ImageProperty, null); SetCurrentValue(ResultProperty, null);
        _inputError = string.IsNullOrWhiteSpace(message) ? null : message;
        RefreshBitmap();
        SetValue(StatusPropertyKey, _inputError ?? "加载图像开始");
    }
    private void RefreshBitmap(bool preserveTransform = false)
    {
        if (_bitmap != null && ReferenceEquals(_bitmapImage, DisplayImage)) { InvalidateVisual(); return; }
        var previous = _bitmapImage;
        _bitmapImage = DisplayImage;
        _bitmap = null;
        _displayError = DisplayImage == null ? _inputError : null;
        try
        {
            if (DisplayImage is { IsEmpty: false } image)
            {
                if (ReferenceEquals(image, Image))
                {
                    if (_inputBitmap == null || !ReferenceEquals(_inputBitmapImage, image))
                    {
                        _inputBitmap = VisionDisplay.ToBitmap(image); _inputBitmapImage = image;
                    }
                    _bitmap = _inputBitmap;
                }
                else _bitmap = VisionDisplay.ToBitmap(image);
            }
        }
        catch (Exception ex) { _displayError = ex.Message; }
        // 中间图失效切回同尺寸输入时，保持当前手势和变换；不能经 Fit 再次回滚刚改的 ROI。
        if (_fit && !(preserveTransform && previous != null && _bitmapImage != null &&
            previous.Width == _bitmapImage.Width && previous.Height == _bitmapImage.Height)) Fit();
        InvalidateVisual();
    }
    public void Fit()
    {
        CancelGesture();
        _fit = true;
        if (DisplayImage is not { IsEmpty: false } image || ActualWidth <= 0 || ActualHeight <= 0) return;
        _scale = Math.Min(ActualWidth / image.Width, ActualHeight / image.Height) * 0.94;
        _offset = new Vector((ActualWidth - image.Width * _scale) / 2, (ActualHeight - image.Height * _scale) / 2);
        InvalidateVisual();
    }
    internal BitmapSource? CurrentBitmap => _bitmap;
    public void ZoomIn() => ZoomAt(1.2, new Point(ActualWidth / 2, ActualHeight / 2));
    public void ZoomOut() => ZoomAt(1 / 1.2, new Point(ActualWidth / 2, ActualHeight / 2));
    public void ActualPixels()
    {
        CancelGesture();
        _fit = false;
        _scale = 1;
        if (DisplayImage is { } image) _offset = new Vector((ActualWidth - image.Width) / 2, (ActualHeight - image.Height) / 2);
        InvalidateVisual();
    }
    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo) { base.OnRenderSizeChanged(sizeInfo); if (_fit) Fit(); }
    private Point Pixel(Point screen) => new((screen.X - _offset.X) / _scale, (screen.Y - _offset.Y) / _scale);
    private Point ClampPixel(Point p) => Image == null ? p : new Point(Math.Clamp(p.X, 0, Image.Width), Math.Clamp(p.Y, 0, Image.Height));
    private void ZoomAt(double factor, Point screen)
    {
        if (_bitmap == null) return;
        CancelGesture();
        var pixel = Pixel(screen);
        _scale = Math.Clamp(_scale * factor, 0.01, 100);
        _offset = new Vector(screen.X - pixel.X * _scale, screen.Y - pixel.Y * _scale);
        _fit = false;
        InvalidateVisual();
    }
    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        if (_bitmap == null) return;
        ZoomAt(e.Delta > 0 ? 1.2 : 1 / 1.2, e.GetPosition(this));
        e.Handled = true;
    }
    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.ChangedButton == MouseButton.Right && IsDrawing) { CompleteDrawing(); e.Handled = true; return; }
        if ((e.ChangedButton != MouseButton.Left && e.ChangedButton != MouseButton.Middle) || _bitmap == null) return;
        var temporaryPan = e.ChangedButton == MouseButton.Middle || (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        if (e.ClickCount == 2 && (Mode == "平移" || temporaryPan)) { Fit(); return; }
        if (Mode == "多边形" && !temporaryPan)
        {
            AddPolygonPoint(e.GetPosition(this), e.ClickCount == 2); e.Handled = true; return;
        }
        CancelGesture();
        Focus();
        _last = e.GetPosition(this);
        var pixel = Pixel(_last);
        if (Mode == "结果选择" && !temporaryPan)
        {
            SelectResultAt(new(pixel.X, pixel.Y), 4 / _scale);
            e.Handled = true;
            return;
        }
        _panning = Mode == "平移" || temporaryPan;
        _gestureButton = e.ChangedButton;
        if (!_panning && (!CanEditImage || !ShowRois || !IsRoiEditingEnabled)) return;
        if (Mode == "选择" && !_panning)
        {
            if (LineSearch != null) { if (!BeginLineSearch(pixel)) return; }
            else
            {
                if (Editor?.TryBeginManipulation(new(pixel.X, pixel.Y), 6 / _scale) != true) return;
                _gestureEditor = Editor;
            }
        }
        else if (!_panning && LineSearch != null) return;
        else if (!_panning && Image != null && (pixel.X < 0 || pixel.Y < 0 || pixel.X >= Image.Width || pixel.Y >= Image.Height)) return;
        _start = ClampPixel(pixel);
        if (!CaptureMouse()) { CancelGesture(); return; }
        e.Handled = true;
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var screen = e.GetPosition(this);
        if (_start != null && IsMouseCaptured)
        {
            if (_lineGesture != null) UpdateLineSearch(Pixel(screen));
            else if (_gestureEditor != null)
            {
                var pixel = Pixel(screen);
                _gestureEditor.UpdateManipulation(new(pixel.X, pixel.Y));
            }
            else if (_panning) { _offset += screen - _last; _fit = false; }
            else _draft = new Rect(_start.Value, ClampPixel(Pixel(screen)));
            _last = screen;
            InvalidateVisual();
        }
        if (_polygonPoints.Count > 0) { _polygonCursor = ClampPixel(Pixel(screen)); InvalidateVisual(); }
        var p = Pixel(screen);
        var image = DisplayImage;
        var text = $"缩放 {_scale:P0}  |  X {p.X:0.0}, Y {p.Y:0.0}";
        if (image != null && image.Format == Core.Imaging.PixelFormat.Mono8 && p.X >= 0 && p.Y >= 0 && p.X < image.Width && p.Y < image.Height)
        {
            var stride = image.Stride > 0 ? image.Stride : image.ExpectedStride;
            text += $"  |  灰度 {image.Pixels[(int)p.Y * stride + (int)p.X]}";
        }
        if (ShowTraining) text += "  |  训练图局部坐标，只读；ROI/结果请切换输入图";
        else if (!CanEditImage && Image != null) text += "  |  输出图尺寸不同，ROI/结果请切换原图";
        SetValue(StatusPropertyKey, _displayError ?? text);
    }
    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.ChangedButton != _gestureButton || _start == null) return;
        var line = _lineGesture;
        if (line != null)
        {
            UpdateLineSearch(Pixel(e.GetPosition(this)));
            _lineGesture = null; line.End(); LineSearchEdited?.Invoke(this, new(line.Target, true));
        }
        var editor = _gestureEditor;
        if (editor != null)
        {
            var pixel = Pixel(e.GetPosition(this));
            editor.UpdateManipulation(new(pixel.X, pixel.Y));
            _gestureEditor = null;
            editor.EndManipulation();
        }
        if (IsDrawing && _draft is { Width: >= 1, Height: >= 1 }) CompleteDrawing();
        CancelGesture();
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Enter && IsDrawing) { CompleteDrawing(); e.Handled = true; }
        if (e.Key == Key.Escape) { CancelDrawing(); if (Mode == "结果选择") Results?.SelectId(null); e.Handled = true; }
        if (e.Key == Key.Delete && LineSearch == null && Mode == "选择" && ShowRois && CanEditImage && IsRoiEditingEnabled) { CancelGesture(); Editor?.RemoveSelected(); e.Handled = true; }
    }
    private void CancelGesture()
    {
        var editor = _gestureEditor;
        _gestureEditor = null;
        _start = null;
        _draft = null;
        _panning = false;
        editor?.EndManipulation(cancel: true);
        var line = _lineGesture; _lineGesture = null;
        if (line != null) { line.End(cancel: true); LineSearchEdited?.Invoke(this, new(line.Target, true)); }
        _polygonPoints.Clear();
        if (IsMouseCaptured) ReleaseMouseCapture();
        InvalidateVisual();
    }
    public static readonly DependencyProperty ExecutionRoiProperty = DependencyProperty.Register(nameof(ExecutionRoi),
        typeof(Core.Roi.Roi), typeof(VisionImageViewport), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    /// <summary>只读本次搜索几何，不能作为ROI手势目标；坐标为实际输入像素。</summary>
    public Core.Roi.Roi? ExecutionRoi { get => (Core.Roi.Roi?)GetValue(ExecutionRoiProperty); set => SetValue(ExecutionRoiProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(27, 30, 35)), null, new Rect(RenderSize));
        if (_bitmap == null)
        {
            dc.DrawText(new FormattedText(_displayError ?? (ShowTraining ? "尚无训练取样图\n请在输入图取样或显式加载模板" : "加载图像开始\n滚轮缩放 · 中键或 Ctrl+左键平移"), CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), 14, Brushes.LightGray, VisualTreeHelper.GetDpi(this).PixelsPerDip), new Point(20, 30));
            return;
        }
        dc.PushTransform(new MatrixTransform(_scale, 0, 0, _scale, _offset.X, _offset.Y));
        dc.DrawImage(_bitmap, new Rect(0, 0, _bitmap.PixelWidth, _bitmap.PixelHeight));
        var resultPen = new Pen(Brushes.LimeGreen, 1.5 / _scale);
        var drawResults = ShowResults && Result?.Success == true && CanEditImage;
        if (drawResults && Mode != "结果选择") _resultDrawing.Draw(dc, resultPen, _scale, Results, ShowResultBounds, ShowResultCenters);
        if (ShowRois && LineSearch != null && CanEditImage) DrawLineSearch(dc);
        if (ShowRois && LineSearch == null && Editor != null && CanEditImage)
            foreach (var roi in Editor.Rois)
            {
                if (roi.Kind is Core.Roi.RoiKind.Circle or Core.Roi.RoiKind.Ellipse)
                {
                    var ellipse = new EllipseGeometry(new Point(roi.Pose.X, roi.Pose.Y), roi.Size.Width / 2, roi.Size.Height / 2)
                    { Transform = new RotateTransform(roi.Pose.AngleDeg, roi.Pose.X, roi.Pose.Y) };
                    dc.DrawGeometry(null, new Pen(ReferenceEquals(roi, Editor.Selected) ? Brushes.Yellow : Brushes.Cyan, 1.5 / _scale), ellipse);
                    continue;
                }
                var outline = roi.Outline;
                if (outline.Count < 2) continue;
                var geometry = new StreamGeometry();
                using (var context = geometry.Open())
                {
                    context.BeginFigure(new Point(outline[0].X, outline[0].Y), false, true);
                    context.PolyLineTo(outline.Skip(1).Select(p => new Point(p.X, p.Y)).ToArray(), true, false);
                }
                dc.DrawGeometry(null, new Pen(ReferenceEquals(roi, Editor.Selected) ? Brushes.Yellow : Brushes.Cyan, 1.5 / _scale), geometry);
            }
        if (ExecutionRoi is { } executed && (ShowRois || ShowResults) && CanEditImage)
        {
            var pen = new Pen(Brushes.Magenta, 1.5 / _scale) { DashStyle = DashStyles.Dash };
            if (executed.Kind is Core.Roi.RoiKind.Circle or Core.Roi.RoiKind.Ellipse)
            {
                var geometry = new EllipseGeometry(new Point(executed.Pose.X, executed.Pose.Y), executed.Size.Width / 2, executed.Size.Height / 2)
                { Transform = new RotateTransform(executed.Pose.AngleDeg, executed.Pose.X, executed.Pose.Y) };
                dc.DrawGeometry(null, pen, geometry);
            }
            else
            {
                var points = executed.Outline;
                var geometry = new StreamGeometry();
                using (var context = geometry.Open())
                    if (points.Count > 1) { context.BeginFigure(new Point(points[0].X, points[0].Y), false, true); context.PolyLineTo(points.Skip(1).Select(p => new Point(p.X, p.Y)).ToArray(), true, false); }
                dc.DrawGeometry(null, pen, geometry);
            }
        }
        if (drawResults && Mode == "结果选择") _resultDrawing.Draw(dc, resultPen, _scale, Results, ShowResultBounds, ShowResultCenters);
        if (ShowRois && LineSearch == null && Editor != null && CanEditImage && IsRoiEditingEnabled && Mode == "选择")
            foreach (var handle in Editor.ResizeHandles.Concat(Editor.VertexHandles))
            {
                var radius = 4 / _scale;
                dc.DrawRectangle(Brushes.Yellow, new Pen(Brushes.Black, 1 / _scale),
                    new Rect(handle.X - radius, handle.Y - radius, 2 * radius, 2 * radius));
            }
        if (ShowRois && LineSearch == null && Editor?.RotationHandle is { } rotation && CanEditImage && IsRoiEditingEnabled && Mode == "选择")
        {
            var center = Editor.Selected!.Pose;
            dc.DrawLine(new Pen(Brushes.Yellow, 1 / _scale), new Point(center.X, center.Y), new Point(rotation.X, rotation.Y));
            dc.DrawEllipse(Brushes.Orange, new Pen(Brushes.Black, 1 / _scale), new Point(rotation.X, rotation.Y), 5 / _scale, 5 / _scale);
        }
        if (_draft is { } draft)
        {
            var pen = new Pen(Brushes.Yellow, 1 / _scale);
            if (Mode is "圆" or "椭圆")
            {
                var rx = Mode == "圆" ? Math.Min(draft.Width, draft.Height) / 2 : draft.Width / 2;
                var ry = Mode == "圆" ? rx : draft.Height / 2;
                dc.DrawEllipse(null, pen, new Point(draft.X + draft.Width / 2, draft.Y + draft.Height / 2), rx, ry);
            }
            else dc.DrawRectangle(null, pen, draft);
        }
        DrawPolygonDraft(dc);
        dc.Pop();
    }

}
