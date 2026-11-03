#nullable enable
using Vision.Core.Geometry;

namespace Vision.Core.Results;

/// <summary>
/// 两条无限直线的交点快照；平行/重合或小于请求角度稳定性阈值时Point为空，执行为NoMatch。
/// 交点可在输入线段和图像视野之外，不暗中裁切或当作线段相交。
/// </summary>
public sealed class LineIntersectionOutput
{
    public Guid FrameId
    {
        get;
    }

    public Guid CoordinateSpaceId
    {
        get;
    }

    public PixelPoint? Point
    {
        get;
    }

    public double SinAngle
    {
        get;
    }

    public LineIntersectionOutput(Guid frameId, Guid coordinateSpaceId, PixelPoint? point, double sinAngle)
    {
        if (frameId == Guid.Empty || coordinateSpaceId == Guid.Empty || !double.IsFinite(sinAngle)
            || sinAngle < 0 || sinAngle > 1 || point != null && (sinAngle == 0
                || point.FrameId != frameId || point.CoordinateSpaceId != coordinateSpaceId))
        {
            throw new ArgumentException("交点来源或夹角稳定性数值无效。");
        }

        FrameId = frameId;
        CoordinateSpaceId = coordinateSpaceId;
        Point = point;
        SinAngle = sinAngle;
    }
}
