#nullable enable

namespace Vision.Core.Geometry;

/// <summary>明确物理平面世界空间的有限毫米坐标，世界轴须为正交、同毫米单位的欧氏坐标，不是像素改标签。</summary>
public sealed class MillimeterPoint
{
    public Guid WorldSpaceId
    {
        get;
    }

    public Point2D PositionMillimeters
    {
        get;
    }

    public MillimeterPoint(Guid worldSpaceId, Point2D positionMillimeters)
    {
        if (worldSpaceId == Guid.Empty || !double.IsFinite(positionMillimeters.X) || !double.IsFinite(positionMillimeters.Y))
        {
            throw new ArgumentException("世界点必须有有效空间及有限毫米坐标。");
        }
        WorldSpaceId = worldSpaceId;
        PositionMillimeters = positionMillimeters;
    }
}
