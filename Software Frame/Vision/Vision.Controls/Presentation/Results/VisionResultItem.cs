using Vision.Core.Geometry;

#nullable enable
namespace Vision.Controls.Presentation;

/// <summary>同一次执行内的托管行/图形快照；区域轮廓、模板矩形、匹配中心和 AI 检测框有不同的拾取语义。</summary>
public sealed class VisionResultItem(int id, double? area, Point2D center, Rect2D bounds, IReadOnlyList<RegionContour> contours,
    VisionResultGeometryKind geometryKind = VisionResultGeometryKind.Region, double? score = null, double? angleDeg = null, double? scale = null,
    string label = "", int? classId = null, IReadOnlyDictionary<string, double>? featureValues = null)
{
    public IReadOnlyDictionary<string, double> FeatureValues { get; } = new System.Collections.ObjectModel.ReadOnlyDictionary<string, double>(
        featureValues?.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal) ?? new Dictionary<string, double>());
    public int Id { get; } = id;
    public double? Area { get; } = area;
    public double? Score { get; } = score;
    public string Label { get; } = label;
    public int? ClassId { get; } = classId;
    public double? AngleDeg { get; } = angleDeg;
    public double? Scale { get; } = scale;
    public VisionResultGeometryKind GeometryKind { get; } = geometryKind;
    public string GeometryDescription => GeometryKind switch
    {
        VisionResultGeometryKind.TemplateRectangle => "匹配矩形（非物体轮廓）",
        VisionResultGeometryKind.MatchCenter => "匹配中心（未提供轮廓）",
        VisionResultGeometryKind.DetectionRectangle => HasValidBounds ? "检测框（非分割轮廓）" : "检测框（无有效位置）",
        _ => "真实区域轮廓"
    };
    public Point2D Center { get; } = center;
    public double X => Center.X;
    public double Y => Center.Y;
    public Rect2D Bounds { get; } = bounds;
    public bool HasValidBounds => !Bounds.IsEmpty && double.IsFinite(Bounds.X) && double.IsFinite(Bounds.Y) &&
        double.IsFinite(Bounds.Width) && double.IsFinite(Bounds.Height) && double.IsFinite(Bounds.Right) && double.IsFinite(Bounds.Bottom);
    /// <summary>真实区域的轴对齐像素外接尺寸，不是标定后的物理长度，也不是面积。</summary>
    public double? BoundingWidth => GeometryKind == VisionResultGeometryKind.Region && HasValidBounds ? Bounds.Width : null;
    public double? BoundingHeight => GeometryKind == VisionResultGeometryKind.Region && HasValidBounds ? Bounds.Height : null;
    public IReadOnlyList<RegionContour> Contours { get; } = Array.AsReadOnly(contours.ToArray());

    public bool HitTest(Point2D point, double tolerance)
    {
        if (!double.IsFinite(point.X) || !double.IsFinite(point.Y) || !double.IsFinite(tolerance) || tolerance < 0) return false;
        if (GeometryKind == VisionResultGeometryKind.MatchCenter) return point.DistanceTo(Center) <= tolerance;
        // 区域的外接框只用于快速排除；矩形结果使用实际定位框，无效定位不得命中全图。
        var rectangle = GeometryKind is VisionResultGeometryKind.TemplateRectangle or VisionResultGeometryKind.DetectionRectangle;
        if (rectangle && !HasValidBounds) return false;
        if (HasValidBounds && (point.X < Bounds.X - tolerance || point.Y < Bounds.Y - tolerance ||
            point.X > Bounds.Right + tolerance || point.Y > Bounds.Bottom + tolerance)) return false;
        if (rectangle && Contours.Count == 0) return true; // 有姿态四角时按实际模板矩形拾取，不用其AABB冒充。
        var insideOuter = false;
        var insideHole = false;
        foreach (var contour in Contours)
        {
            var points = contour.Points;
            var inside = false;
            for (var i = 0; i < points.Count; i++)
            {
                var a = points[i];
                var b = points[(i + 1) % points.Count];
                var dx = b.X - a.X;
                var dy = b.Y - a.Y;
                var lengthSquared = dx * dx + dy * dy;
                var t = lengthSquared == 0 ? 0 : Math.Clamp(((point.X - a.X) * dx + (point.Y - a.Y) * dy) / lengthSquared, 0, 1);
                if (point.DistanceTo(new Point2D(a.X + t * dx, a.Y + t * dy)) <= tolerance) return true;
                if ((a.Y > point.Y) != (b.Y > point.Y) && point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X)
                    inside = !inside;
            }
            if (contour.IsHole) insideHole |= inside;
            else insideOuter |= inside;
        }
        return insideOuter && !insideHole;
    }
}
