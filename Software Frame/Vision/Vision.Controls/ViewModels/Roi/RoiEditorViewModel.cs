using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Vision.Core.Geometry;
using Vision.Core.Roi;

#nullable enable
namespace Vision.Controls.ViewModels;

/// <summary>ROI 列表及图像坐标编辑。手势只借用 ROI；取消、换选中项和移除时回滚未提交的几何。</summary>
public sealed partial class RoiEditorViewModel : ObservableObject
{
    private const double DefaultRoiWidth = 200;
    private const double DefaultRoiHeight = 100;
    private int _maxRois = int.MaxValue;
    public ObservableCollection<Roi> Rois { get; } = new();
    /// <summary>当前工具允许保留的 ROI 数量；搜索域工具为 1，其它工具不限制。</summary>
    public int MaxRois
    {
        get => _maxRois;
        set
        {
            if (value < 1) throw new ArgumentOutOfRangeException(nameof(value));
            if (_maxRois == value) return;
            _maxRois = value;
            TrimToLimit();
        }
    }
    public IReadOnlyList<string> RoiTypes { get; } = Array.AsReadOnly(new[] { "矩形", "旋转矩形", "圆", "椭圆", "多边形" });
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddSelectedRoiCommand))]
    private string _selectedRoiType = "矩形";
    [ObservableProperty] private Roi? _selected;
    public int Count => Rois.Count;
    public bool HasSelection => Selected != null && Rois.Contains(Selected);
    public bool CanResizeSelected => HasSelection && !Selected!.IsEmpty && Selected.Kind is
        RoiKind.Rectangle or RoiKind.RotatedRectangle or RoiKind.Circle or RoiKind.Ellipse;
    public bool CanRotateSelected => HasSelection && Selected!.Kind is
        RoiKind.RotatedRectangle or RoiKind.Ellipse;

    private Roi? _editing;
    private Point2D _start;
    private Pose2D _originalPose;
    private Size2D _originalSize;
    private List<Point2D> _originalPoints = [];
    private Point2D? _handle;
    private RoiKind _originalKind;
    private bool _rotating;
    private int _vertexIndex = -1;
    public Point2D? RotationHandle => CanRotateSelected
        ? Selected!.Pose.Transform(new(0, -Selected.Size.Height / 2 - 20)) : null;
    public IReadOnlyList<Point2D> VertexHandles => HasSelection && Selected!.Kind == RoiKind.Polygon
        ? Selected.OutlinePoints.AsReadOnly() : Array.Empty<Point2D>();
    public bool IsManipulating => _editing != null;

    public RoiEditorViewModel()
    {
        Rois.CollectionChanged += (_, _) =>
        {
            if (_editing != null && !Rois.Contains(_editing)) EndManipulation(cancel: true);
            if (Selected != null && !Rois.Contains(Selected)) Selected = null;
            OnPropertyChanged(nameof(Count));
            NotifyGeometry();
        };
    }
    partial void OnSelectedChanged(Roi? value)
    {
        EndManipulation(cancel: true);
        NotifyGeometry();
    }

    public double SelectedX { get => Selected?.Pose.X ?? 0; set => SetGeometry(value, SelectedY, SelectedWidth, SelectedHeight, SelectedAngle); }
    public double SelectedY { get => Selected?.Pose.Y ?? 0; set => SetGeometry(SelectedX, value, SelectedWidth, SelectedHeight, SelectedAngle); }
    public double SelectedWidth { get => Selected?.Size.Width ?? 0; set => SetGeometry(SelectedX, SelectedY, value, Selected?.Kind == RoiKind.Circle ? value : SelectedHeight, SelectedAngle); }
    public double SelectedHeight { get => Selected?.Size.Height ?? 0; set => SetGeometry(SelectedX, SelectedY, Selected?.Kind == RoiKind.Circle ? value : SelectedWidth, value, SelectedAngle); }
    public double SelectedAngle
    {
        get => Selected?.Pose.AngleDeg ?? 0;
        set { if (CanRotateSelected) SetGeometry(SelectedX, SelectedY, SelectedWidth, SelectedHeight, value); }
    }

    private void SetGeometry(double x, double y, double width, double height, double angle)
    {
        if (!HasSelection) return;
        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(angle) ||
            !double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "坐标必须为有限数值，尺寸必须大于 0。");
        EndManipulation(cancel: true);
        var roi = Selected!;
        if (roi.Kind == RoiKind.Polygon)
        {
            if (width != roi.Size.Width || height != roi.Size.Height || angle != roi.Pose.AngleDeg)
                throw new InvalidOperationException("多边形只支持整体移动；此处不提供顶点/尺寸编辑。");
            var dx = x - roi.Pose.X;
            var dy = y - roi.Pose.Y;
            roi.OutlinePoints = roi.OutlinePoints.Select(p => new Point2D(p.X + dx, p.Y + dy)).ToList();
        }
        angle = roi.Kind is RoiKind.Rectangle or RoiKind.Circle ? 0 : Pose2D.NormalizeAngle(angle);
        roi.Pose = new(x, y, angle);
        roi.Size = new(width, height);
        NotifyGeometry();
    }
    private void NotifyGeometry()
    {
        OnPropertyChanged(nameof(Selected));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(CanResizeSelected));
        OnPropertyChanged(nameof(CanRotateSelected));
        OnPropertyChanged(nameof(RotationHandle));
        OnPropertyChanged(nameof(VertexHandles));
        OnPropertyChanged(nameof(SelectedX));
        OnPropertyChanged(nameof(SelectedY));
        OnPropertyChanged(nameof(SelectedWidth));
        OnPropertyChanged(nameof(SelectedHeight));
        OnPropertyChanged(nameof(SelectedAngle));
    }

    private bool CanAddSelectedRoi() => RoiTypes.Contains(SelectedRoiType);
    [RelayCommand(CanExecute = nameof(CanAddSelectedRoi))]
    public void AddSelectedRoi()
    {
        switch (SelectedRoiType)
        {
            case "矩形": AddDefaultRectangle(); break;
            case "旋转矩形": AddDefaultRotatedRectangle(); break;
            case "圆": AddDefaultCircle(); break;
            case "椭圆": AddDefaultEllipse(); break;
            case "多边形": AddDefaultPolygon(); break;
        }
    }
    [RelayCommand] public void AddDefaultRectangle() => AddRectangle(32, 32, DefaultRoiWidth, DefaultRoiHeight);
    [RelayCommand] public void AddDefaultCircle() => AddCircle(32, 32, DefaultRoiHeight); // 圆只有单一直径，取默认高度。
    [RelayCommand] public void AddDefaultRotatedRectangle() => AddRotatedRectangle(32, 32, DefaultRoiWidth, DefaultRoiHeight, 0);
    [RelayCommand] public void AddDefaultEllipse() => AddEllipse(32, 32, DefaultRoiWidth, DefaultRoiHeight);
    [RelayCommand] public void AddDefaultPolygon() => AddPolygon([new(10, 10), new(50, 10), new(32, 50)]);
    public Roi AddRotatedRectangle(double x, double y, double width, double height, double angle)
    {
        var roi = Roi.RotatedRectangle(x, y, width, height, angle); EnsureCapacity(); Rois.Add(roi); Selected = roi; return roi;
    }
    public Roi AddEllipse(double x, double y, double width, double height)
    {
        var roi = Roi.Ellipse(x, y, width, height, 0); EnsureCapacity(); Rois.Add(roi); Selected = roi; return roi;
    }
    public Roi AddPolygon(IReadOnlyList<Point2D> points)
    {
        ValidatePolygon(points);
        var roi = Roi.Polygon(points); EnsureCapacity(); Rois.Add(roi); Selected = roi; return roi;
    }
    private static void ValidatePolygon(IReadOnlyList<Point2D> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count < 3 || points.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y)))
            throw new ArgumentException("多边形至少需要三个有效顶点。");
        var area = 0.0;
        for (var i = 0; i < points.Count; i++) { var p = points[i]; var q = points[(i + 1) % points.Count]; area += p.X * q.Y - q.X * p.Y; }
        if (!double.IsFinite(area) || Math.Abs(area) < 1e-6) throw new ArgumentException("多边形必须有有限、非零面积，顶点不能共线。");
    }
    public Roi AddRectangle(double centerX, double centerY, double width, double height)
    {
        var roi = Roi.Rectangle(centerX, centerY, width, height);
        EnsureCapacity();
        Rois.Add(roi);
        Selected = roi;
        return roi;
    }
    public Roi AddCircle(double centerX, double centerY, double diameter)
    {
        var roi = Roi.Circle(centerX, centerY, diameter);
        EnsureCapacity();
        Rois.Add(roi);
        Selected = roi;
        return roi;
    }
    private void EnsureCapacity()
    {
        while (Rois.Count >= _maxRois) Rois.RemoveAt(0);
    }
    private void TrimToLimit()
    {
        while (Rois.Count > _maxRois)
        {
            var keep = Selected != null && Rois.Contains(Selected) ? Selected : Rois[Rois.Count - 1];
            var remove = Rois.FirstOrDefault(r => !ReferenceEquals(r, keep));
            if (remove == null) return;
            Rois.Remove(remove);
        }
    }
    [RelayCommand] public void RemoveSelected()
    {
        if (Selected != null) Rois.Remove(Selected);
    }
    [RelayCommand] public void Clear()
    {
        EndManipulation(cancel: true);
        Rois.Clear();
        Selected = null;
    }
    public bool MoveSelected(double dx, double dy)
    {
        if (!HasSelection || !double.IsFinite(dx) || !double.IsFinite(dy)) return false;
        SetGeometry(SelectedX + dx, SelectedY + dy, SelectedWidth, SelectedHeight, SelectedAngle);
        return true;
    }
    public bool ResizeSelected(double width, double height)
    {
        if (!CanResizeSelected || !double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0) return false;
        if (Selected!.Kind == RoiKind.Circle && width != height) return false;
        SetGeometry(SelectedX, SelectedY, width, height, SelectedAngle);
        return true;
    }
    public void FollowFixture(Pose2D oldFixture, Pose2D newFixture)
    {
        EndManipulation(cancel: true);
        var selectedIndex = Selected == null ? -1 : Rois.IndexOf(Selected);
        for (var i = 0; i < Rois.Count; i++) Rois[i] = Rois[i].FollowFixture(oldFixture, newFixture);
        Selected = selectedIndex >= 0 ? Rois[selectedIndex] : null;
        NotifyGeometry();
    }

    /// <summary>尺寸手柄的图像坐标。矩形/椭圆四角；圆四个径向手柄，圆心不随缩放改变。</summary>
    public IReadOnlyList<Point2D> ResizeHandles
    {
        get
        {
            if (!CanResizeSelected) return Array.Empty<Point2D>();
            var roi = Selected!;
            var hw = roi.Size.Width / 2;
            var hh = roi.Size.Height / 2;
            Point2D[] local = roi.Kind == RoiKind.Circle
                ? [new(hw, 0), new(0, hw), new(-hw, 0), new(0, -hw)]
                : [new(-hw, -hh), new(hw, -hh), new(hw, hh), new(-hw, hh)];
            return local.Select(roi.Pose.Transform).ToArray();
        }
    }
    public bool TryBeginManipulation(Point2D point, double tolerance)
    {
        EndManipulation(cancel: true);
        if (!double.IsFinite(point.X) || !double.IsFinite(point.Y) || !double.IsFinite(tolerance) || tolerance < 0) return false;
        Point2D? handle = null;
        if (CanResizeSelected)
            foreach (var candidate in ResizeHandles)
                if (Distance(point, candidate) <= tolerance) { handle = candidate; break; }
        var vertex = -1;
        var vertices = VertexHandles;
        for (var i = 0; i < vertices.Count; i++)
            if (Distance(point, vertices[i]) <= tolerance) { vertex = i; break; }
        var rotating = handle == null && vertex < 0 && RotationHandle is { } rotation && Distance(point, rotation) <= tolerance;
        var hit = handle != null || vertex >= 0 || rotating ? Selected : Rois.Reverse().FirstOrDefault(r => Contains(r, point, tolerance));
        Selected = hit;
        if (hit == null) return false;
        _editing = hit;
        _start = point;
        _originalPose = hit.Pose;
        _originalSize = hit.Size;
        _originalKind = hit.Kind; _rotating = rotating; _vertexIndex = vertex;
        _originalPoints = hit.OutlinePoints.ToList();
        _handle = handle == null ? null : hit.Pose.Inverse().Transform(handle.Value);
        return true;
    }
    public bool UpdateManipulation(Point2D point)
    {
        if (_editing == null || !ReferenceEquals(_editing, Selected) || !Rois.Contains(_editing)) return false;
        if (!double.IsFinite(point.X) || !double.IsFinite(point.Y)) return false;
        var roi = _editing;
        if (_rotating)
        {
            if (Distance(point, new(_originalPose.X, _originalPose.Y)) < 1e-6) return false;
            var before = Math.Atan2(_start.Y - _originalPose.Y, _start.X - _originalPose.X);
            var after = Math.Atan2(point.Y - _originalPose.Y, point.X - _originalPose.X);
            var angle = Pose2D.NormalizeAngle(_originalPose.AngleDeg + (after - before) * 180 / Math.PI);
            roi.Pose = new(_originalPose.X, _originalPose.Y, angle);
        }
        else if (_vertexIndex >= 0)
        {
            var points = _originalPoints.ToArray(); points[_vertexIndex] = point;
            try { ValidatePolygon(points); } catch (ArgumentException) { return false; }
            var frame = Roi.Polygon(points);
            roi.OutlinePoints = frame.OutlinePoints; roi.Pose = frame.Pose; roi.Size = frame.Size;
        }
        else if (_handle is { } handle)
        {
            var local = _originalPose.Inverse().Transform(point);
            if (roi.Kind == RoiKind.Circle)
            {
                var diameter = Math.Max(1, 2 * Math.Sqrt(local.X * local.X + local.Y * local.Y));
                roi.Size = new(diameter, diameter);
            }
            else
            {
                var sx = Math.Sign(handle.X);
                var sy = Math.Sign(handle.Y);
                var anchor = new Point2D(-sx * _originalSize.Width / 2, -sy * _originalSize.Height / 2);
                var width = Math.Max(1, sx * (local.X - anchor.X));
                var height = Math.Max(1, sy * (local.Y - anchor.Y));
                var center = _originalPose.Transform(new(anchor.X + sx * width / 2, anchor.Y + sy * height / 2));
                roi.Pose = new(center.X, center.Y, _originalPose.AngleDeg);
                roi.Size = new(width, height);
            }
        }
        else
        {
            var dx = point.X - _start.X;
            var dy = point.Y - _start.Y;
            roi.Pose = new(_originalPose.X + dx, _originalPose.Y + dy, _originalPose.AngleDeg);
            if (roi.Kind == RoiKind.Polygon)
                roi.OutlinePoints = _originalPoints.Select(p => new Point2D(p.X + dx, p.Y + dy)).ToList();
        }
        NotifyGeometry();
        return true;
    }
    public void EndManipulation(bool cancel = false)
    {
        var roi = _editing;
        _editing = null;
        _handle = null; _rotating = false; _vertexIndex = -1;
        if (cancel && roi != null)
        {
            roi.Kind = _originalKind;
            roi.Pose = _originalPose;
            roi.Size = _originalSize;
            roi.OutlinePoints = _originalPoints.ToList();
            NotifyGeometry();
        }
        if (roi != null && roi.Kind != _originalKind && Rois.Contains(roi))
        {
            // 提交后刷新普通 DTO 的类型行；不能在拖动中替换集合项触发手势回滚。
            var selected = Selected;
            Rois[Rois.IndexOf(roi)] = roi;
            if (ReferenceEquals(selected, roi)) Selected = roi;
            NotifyGeometry();
        }
        _originalPoints.Clear();
    }
    private static double Distance(Point2D a, Point2D b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    private static bool Contains(Roi roi, Point2D point, double tolerance)
    {
        if (roi.IsEmpty) return false;
        var local = roi.Pose.Inverse().Transform(point);
        if (roi.Kind is RoiKind.Rectangle or RoiKind.RotatedRectangle)
            return Math.Abs(local.X) <= roi.Size.Width / 2 + tolerance && Math.Abs(local.Y) <= roi.Size.Height / 2 + tolerance;
        if (roi.Kind is RoiKind.Circle or RoiKind.Ellipse)
        {
            var a = roi.Size.Width / 2 + tolerance;
            var b = (roi.Kind == RoiKind.Circle ? roi.Size.Width : roi.Size.Height) / 2 + tolerance;
            return local.X * local.X / (a * a) + local.Y * local.Y / (b * b) <= 1;
        }
        if (roi.Kind != RoiKind.Polygon) return false;
        var points = roi.OutlinePoints;
        var inside = false;
        for (var i = 0; i < points.Count; i++)
        {
            var p = points[i];
            var q = points[(i + 1) % points.Count];
            if ((p.Y > point.Y) != (q.Y > point.Y) && point.X < (q.X - p.X) * (point.Y - p.Y) / (q.Y - p.Y) + p.X)
                inside = !inside;
        }
        return inside;
    }
}
