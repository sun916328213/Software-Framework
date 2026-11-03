using Vision.Core.Geometry;

#nullable enable
namespace Vision.Controls.ViewModels;

/// <summary>找线参数的拖动事务；只借用原 DTO，换工具/失去捕获/卸载时回滚。</summary>
public sealed class LineSearchEditorSession(ILineSearchRegion target)
{
    private Point2D _start, _end, _down;
    private double _halfLength;
    private string? _handle;
    private RoiEditorViewModel? _frameEditor;
    public IReadOnlyList<Point2D> ResizeHandles => LineSearchGeometry.EditingFrame(Target).Outline;
    public Point2D RotationHandle
    {
        get
        {
            var frame = LineSearchGeometry.EditingFrame(Target);
            return frame.Pose.Transform(new(0, -frame.Size.Height / 2 - 20));
        }
    }
    public ILineSearchRegion Target { get; } = target;
    public bool IsActive => _handle != null;
    public Point2D WidthHandle
    {
        get
        {
            var n = LineSearchGeometry.Normal(Target);
            return new((Target.LineStart.X + Target.LineEnd.X) / 2 + n.X * Target.NormalHalfLength,
                (Target.LineStart.Y + Target.LineEnd.Y) / 2 + n.Y * Target.NormalHalfLength);
        }
    }
    public bool Begin(Point2D point, double tolerance)
    {
        End(cancel: true);
        if (!double.IsFinite(point.X) || !double.IsFinite(point.Y) || !double.IsFinite(tolerance) || tolerance < 0) return false;
        try
        {
            _handle = Distance(point, Target.LineStart) <= tolerance ? "start" :
                Distance(point, Target.LineEnd) <= tolerance ? "end" : Distance(point, WidthHandle) <= tolerance ? "width" : null;
            if (_handle == null && (ResizeHandles.Any(corner => Distance(point, corner) <= tolerance) || Distance(point, RotationHandle) <= tolerance))
            {
                // 只在手势期间借用派生外框，复用普通旋转ROI的缩放/旋转规则；不加入工程ROI列表。
                var frame = LineSearchGeometry.EditingFrame(Target);
                _frameEditor = new();
                _frameEditor.Rois.Add(frame); _frameEditor.Selected = frame;
                if (!_frameEditor.TryBeginManipulation(point, tolerance)) { _frameEditor = null; return false; }
                _handle = "frame";
            }
            if (_handle == null)
            {
                var dx = Target.LineEnd.X - Target.LineStart.X; var dy = Target.LineEnd.Y - Target.LineStart.Y;
                var length = Math.Sqrt(dx * dx + dy * dy); var n = LineSearchGeometry.Normal(Target);
                var rx = point.X - Target.LineStart.X; var ry = point.Y - Target.LineStart.Y;
                var along = (rx * dx + ry * dy) / length;
                if (along < -Target.TangentHalfWidth - tolerance || along > length + Target.TangentHalfWidth + tolerance ||
                    Math.Abs(rx * n.X + ry * n.Y) > Target.NormalHalfLength + tolerance) return false;
                _handle = "move";
            }
            _start = Target.LineStart; _end = Target.LineEnd; _halfLength = Target.NormalHalfLength; _down = point;
            return true;
        }
        catch (ArgumentException) { _handle = null; return false; }
    }
    public bool Update(Point2D point)
    {
        if (!IsActive || !double.IsFinite(point.X) || !double.IsFinite(point.Y)) return false;
        if (_handle == "frame")
        {
            if (_frameEditor?.UpdateManipulation(point) != true) return false;
            var frame = _frameEditor.Selected!;
            var extension = Target.SearchKind == LineSearchKind.Calipers ? Target.TangentHalfWidth : 0;
            var halfSpan = (frame.Size.Width - 2 * extension) / 2;
            if (halfSpan < 0.5 || frame.Size.Height < 2) return false;
            Target.LineStart = frame.Pose.Transform(new(-halfSpan, 0));
            Target.LineEnd = frame.Pose.Transform(new(halfSpan, 0));
            Target.NormalHalfLength = frame.Size.Height / 2;
        }
        else if (_handle == "start") { if (Distance(point, Target.LineEnd) < 1) return false; Target.LineStart = point; }
        else if (_handle == "end") { if (Distance(point, Target.LineStart) < 1) return false; Target.LineEnd = point; }
        else if (_handle == "width")
        {
            var n = LineSearchGeometry.Normal(Target);
            Target.NormalHalfLength = Math.Max(1, Math.Abs((point.X - (_start.X + _end.X) / 2) * n.X + (point.Y - (_start.Y + _end.Y) / 2) * n.Y));
        }
        else
        {
            var dx = point.X - _down.X; var dy = point.Y - _down.Y;
            Target.LineStart = new(_start.X + dx, _start.Y + dy); Target.LineEnd = new(_end.X + dx, _end.Y + dy);
        }
        return true;
    }
    public void End(bool cancel = false)
    {
        if (!IsActive) return;
        _handle = null;
        _frameEditor?.EndManipulation(cancel); _frameEditor = null;
        if (!cancel) return;
        Target.LineStart = _start; Target.LineEnd = _end; Target.NormalHalfLength = _halfLength;
    }
    private static double Distance(Point2D a, Point2D b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}
