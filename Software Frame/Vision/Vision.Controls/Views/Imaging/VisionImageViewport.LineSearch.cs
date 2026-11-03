using System.Windows;
using System.Windows.Media;
using Vision.Controls.ViewModels;
using Vision.Core.Geometry;

#nullable enable
namespace Vision.Controls.View;

public sealed partial class VisionImageViewport
{
    public static readonly DependencyProperty LineSearchProperty = DependencyProperty.Register(nameof(LineSearch), typeof(ILineSearchRegion), typeof(VisionImageViewport),
        new FrameworkPropertyMetadata(null, (d, _) => { var view = (VisionImageViewport)d; view.CancelGesture(); view.InvalidateVisual(); }));
    public ILineSearchRegion? LineSearch { get => (ILineSearchRegion?)GetValue(LineSearchProperty); set => SetValue(LineSearchProperty, value); }
    public event EventHandler<LineSearchEditedEventArgs>? LineSearchEdited;
    private LineSearchEditorSession? _lineGesture;
    private (Point2D Start, Point2D End, double Length, double Width, int Count, LineSearchKind Kind)? _lineKey;
    private ResultOutlineCache _lineOutline = ResultOutlineCache.Empty;
    private ResultOutlineCache _lineFrame = ResultOutlineCache.Empty;
    public void RefreshLineSearch() { CancelGesture(); InvalidateVisual(); }
    private bool BeginLineSearch(Point point)
    {
        if (LineSearch == null) return false;
        var session = new LineSearchEditorSession(LineSearch);
        if (!session.Begin(new(point.X, point.Y), 6 / _scale)) return false;
        _lineGesture = session;
        return true;
    }
    private void UpdateLineSearch(Point point)
    {
        if (_lineGesture?.Update(new(point.X, point.Y)) == true)
            LineSearchEdited?.Invoke(this, new(_lineGesture.Target, false));
    }
    private void DrawLineSearch(DrawingContext drawing)
    {
        if (LineSearch is not { } region) return;
        try
        {
            var key = (region.LineStart, region.LineEnd, region.NormalHalfLength, region.TangentHalfWidth, region.SampleCount, region.SearchKind);
            if (_lineKey != key)
            {
                var contours = LineSearchGeometry.Rectangles(region).Select(r => new RegionContour(r.Outline)).ToList();
                _lineOutline = new(contours);
                _lineFrame = new([new RegionContour(LineSearchGeometry.EditingFrame(region).Outline)]);
                _lineKey = key;
            }
            // 两后端共用黄色编辑外框；仅真实卡尺额外显示青色采样阵列，外框不作为算法域。
            _lineFrame.Draw(drawing, new Pen(Brushes.Yellow, 1.5 / _scale), _scale);
            if (region.SearchKind == LineSearchKind.Calipers)
                _lineOutline.Draw(drawing, new Pen(Brushes.Cyan, 1 / _scale), _scale);
            var start = new Point(region.LineStart.X, region.LineStart.Y); var end = new Point(region.LineEnd.X, region.LineEnd.Y);
            drawing.DrawLine(new Pen(Brushes.Cyan, 1 / _scale), start, end);
            var n = LineSearchGeometry.Normal(region);
            var center = new Point((start.X + end.X) / 2, (start.Y + end.Y) / 2);
            var axis = new Point(center.X + n.X * region.NormalHalfLength, center.Y + n.Y * region.NormalHalfLength);
            drawing.DrawLine(new Pen(Brushes.Yellow, 1.5 / _scale), center, axis);
            if (Mode == "选择" && IsRoiEditingEnabled)
            {
                var editor = new LineSearchEditorSession(region);
                foreach (var handle in editor.ResizeHandles.Select(p => new Point(p.X, p.Y)).Concat(new[] { start, end, axis }))
                    drawing.DrawRectangle(Brushes.Yellow, new Pen(Brushes.Black, 1 / _scale), new Rect(handle.X - 4 / _scale, handle.Y - 4 / _scale, 8 / _scale, 8 / _scale));
                var rotation = editor.RotationHandle;
                drawing.DrawLine(new Pen(Brushes.Yellow, 1 / _scale), center, new(rotation.X, rotation.Y));
                drawing.DrawEllipse(Brushes.Orange, new Pen(Brushes.Black, 1 / _scale), new(rotation.X, rotation.Y), 4 / _scale, 4 / _scale);
            }
        }
        catch (ArgumentException error) { SetValue(StatusPropertyKey, $"搜索几何不可显示：{error.Message}"); }
    }
}
