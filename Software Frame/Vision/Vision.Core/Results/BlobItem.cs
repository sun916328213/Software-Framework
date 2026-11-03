#nullable enable
using Vision.Core.Geometry;

namespace Vision.Core.Results;

/// <summary>
/// 单个区域的独立只读快照。面积为有效前景像素计数，不等于轮廓多边形面积。
/// Index是一基本次排序位置，不是跨帧跟踪ID；轮廓使用像素中心，孔洞单独标记。
/// </summary>
public sealed class BlobItem
{
    public int Index
    {
        get;
    }
    public long Area
    {
        get;
    }
    public Point2D Center
    {
        get;
    }
    public Rect2D Bounds
    {
        get;
    }
    public double Extent => Area / (Bounds.Width * Bounds.Height);
    public int HoleCount => Contours.Count(contour => contour.IsHole);
    /// <summary>像素中心轮廓几何；面积/圆度不能与上方像素Area混用。</summary>
    public BlobContourMetrics ContourMetrics
    {
        get;
    }
    public IReadOnlyList<RegionContour> Contours
    {
        get;
    }

    public BlobItem(int index, long area, Point2D center, Rect2D bounds,
        IEnumerable<RegionContour> contours, BlobContourMetrics contourMetrics)
    {
        ArgumentNullException.ThrowIfNull(contours);
        ArgumentNullException.ThrowIfNull(contourMetrics);
        if (index < 1 || area <= 0 || !double.IsFinite(center.X) || !double.IsFinite(center.Y)
            || !double.IsFinite(bounds.X) || !double.IsFinite(bounds.Y)
            || !double.IsFinite(bounds.Width) || !double.IsFinite(bounds.Height)
            || bounds.X < 0 || bounds.Y < 0 || bounds.Width <= 0 || bounds.Height <= 0
            || !double.IsFinite(bounds.Right) || !double.IsFinite(bounds.Bottom)
            || !double.IsFinite(bounds.Width * bounds.Height) || area > bounds.Width * bounds.Height
            || center.X < bounds.Left || center.X >= bounds.Right || center.Y < bounds.Top || center.Y >= bounds.Bottom)
        {
            throw new ArgumentException("区域几何、像素面积或结果序号无效。");
        }

        var snapshot = contours.ToArray();
        if (snapshot.Any(contour => contour == null
            || contour.Points.Any(point => !double.IsFinite(point.X) || !double.IsFinite(point.Y)
                || point.X < bounds.Left || point.X >= bounds.Right || point.Y < bounds.Top || point.Y >= bounds.Bottom)))
        {
            throw new ArgumentException("轮廓必须为有限坐标快照。", nameof(contours));
        }

        Index = index;
        Area = area;
        Center = center;
        Bounds = bounds;
        Contours = Array.AsReadOnly(snapshot);
        ContourMetrics = contourMetrics;
    }
}
