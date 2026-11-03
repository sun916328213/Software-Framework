using Vision.Core.Geometry;

#nullable enable

namespace Vision.Core.Roi;

/// <summary>
/// 单次执行的不可变搜索域。坐标为原图像素中心；矩形采用左上包含、右下不包含。
/// </summary>
public sealed class SearchRegion
{
    private readonly RoiKind _kind;
    private readonly Pose2D _pose;
    private readonly Size2D _size;
    private readonly Point2D[] _points;
    private readonly double _cos, _sin;
    public Rect2D Bounds
    {
        get;
    }

    public SearchRegion(Roi roi)
    {
        ArgumentNullException.ThrowIfNull(roi);
        if (roi.Kind is not (RoiKind.Rectangle or RoiKind.RotatedRectangle or RoiKind.Circle or RoiKind.Ellipse or RoiKind.Polygon))
        {
            throw new ArgumentException("当前工具不支持此搜索域类型。");
        }

        if (!double.IsFinite(roi.Pose.X) || !double.IsFinite(roi.Pose.Y) || !double.IsFinite(roi.Pose.AngleDeg) ||
            !double.IsFinite(roi.Size.Width) || !double.IsFinite(roi.Size.Height) || roi.Size.Width <= 0 || roi.Size.Height <= 0)
        {
            throw new ArgumentException("搜索 ROI 坐标必须有限，尺寸必须为正。");
        }

        if (roi.Kind == RoiKind.Circle && roi.Size.Width != roi.Size.Height)
        {
            throw new ArgumentException("圆 ROI 的宽高必须一致。");
        }

        _kind = roi.Kind;
        _pose = roi.Pose;
        _size = roi.Size;
        _points = roi.OutlinePoints?.ToArray() ?? throw new ArgumentException("ROI 顶点列表不能为 null。");
        if (_kind == RoiKind.Polygon && (_points.Length < 3 || _points.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y))))
        {
            throw new ArgumentException("多边形搜索域至少需要三个有效顶点。");
        }

        (_cos, _sin) = RotationMath.SinCosDegrees(_pose.AngleDeg);
        Bounds = roi.Bounds;
        if (Bounds.IsEmpty || !double.IsFinite(Bounds.Left) || !double.IsFinite(Bounds.Right) ||
            !double.IsFinite(Bounds.Top) || !double.IsFinite(Bounds.Bottom))
        {
            throw new ArgumentException("搜索域边界无效或超出数值范围。");
        }
    }

    /// <summary>
    /// 对冻结几何施加正等比相似变换，不累积修改基准ROI；不接受镜像、剪切或非有限映射。
    /// </summary>
    public SearchRegion TransformSimilarity(Vision.Core.Coordinate.AffineTransform2D transform)
    {
        if (!double.IsFinite(transform.A) || !double.IsFinite(transform.B) || !double.IsFinite(transform.C)
            || !double.IsFinite(transform.D) || !double.IsFinite(transform.Tx) || !double.IsFinite(transform.Ty)
            || transform.Determinant <= 0 || !transform.IsSimilarity(1e-9, out _, out _))
        {
            throw new ArgumentException("ROI跟随只接受可表示的正等比旋转/平移映射。", nameof(transform));
        }

        var reference = new Roi
        {
            Kind = _kind,
            Pose = _pose,
            Size = _size,
            OutlinePoints = _points.ToList()
        };
        return new SearchRegion(reference.Transform(transform));
    }

    public bool Contains(double x, double y)
    {
        var dx = x - _pose.X;
        var dy = y - _pose.Y;
        var lx = dx * _cos + dy * _sin;
        var ly = -dx * _sin + dy * _cos;
        var hw = _size.Width / 2;
        var hh = _size.Height / 2;
        if (_kind is RoiKind.Rectangle or RoiKind.RotatedRectangle)
        {
            return lx >= -hw && lx < hw && ly >= -hh && ly < hh;
        }

        if (_kind is RoiKind.Circle or RoiKind.Ellipse)
        {
            if (_kind == RoiKind.Circle)
            {
                hh = hw;
            }

            var nx = lx / hw;
            var ny = ly / hh;
            return nx * nx + ny * ny <= 1;
        }
        var inside = false;
        for (var i = 0; i < _points.Length; i++)
        {
            var p = _points[i];
            var q = _points[(i + 1) % _points.Length];
            var cross = (x - p.X) * (q.Y - p.Y) - (y - p.Y) * (q.X - p.X);
            if (Math.Abs(cross) < 1e-9 && x >= Math.Min(p.X, q.X) && x <= Math.Max(p.X, q.X) &&
                y >= Math.Min(p.Y, q.Y) && y <= Math.Max(p.Y, q.Y))
            {
                return true;
            }

            if ((p.Y > y) != (q.Y > y) && x < (q.X - p.X) * (y - p.Y) / (q.Y - p.Y) + p.X)
            {
                inside = !inside;
            }
        }
        return inside;
    }

    /// <summary>
    /// 几何域的像素游程，左右端均包含；裁切图像边界但不改变坐标原点。
    /// </summary>
    public IEnumerable<(int Row, int Start, int End)> Runs(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);
        var left = (int)Math.Clamp(Math.Floor(Bounds.Left), 0, width);
        var right = (int)Math.Clamp(Math.Ceiling(Bounds.Right), 0, width);
        var top = (int)Math.Clamp(Math.Floor(Bounds.Top), 0, height);
        var bottom = (int)Math.Clamp(Math.Ceiling(Bounds.Bottom), 0, height);
        // 包含圆和多边形的边界像素中心，矩形仍由 Contains 的半开规则决定。
        right = Math.Min(width, right + (right < width ? 1 : 0));
        bottom = Math.Min(height, bottom + (bottom < height ? 1 : 0));
        for (var y = top; y < bottom; y++)
        {
            var start = -1;
            for (var x = left; x < right; x++)
            {
                if (Contains(x, y))
                {
                    if (start < 0)
                    {
                        start = x;
                    }
                }
                else if (start >= 0)
                {
                    yield return (y, start, x - 1);
                    start = -1;
                }
            }
            if (start >= 0)
            {
                yield return (y, start, right - 1);
            }
        }
    }
}

/// <summary>
/// 工具显式声明搜索域能力；不是普通参数 JSON 或显示图层开关。
/// </summary>
public interface ISearchRegionTool
{
    SearchRegion? SearchRegion
    {
        get;
        set;
    }
}
