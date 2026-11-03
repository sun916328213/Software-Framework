#nullable enable
using Vision.Core.Results;

namespace Vision.Core.Geometry;

/// <summary>
/// 带帧/像素空间的有向无限直线，由两个有限非重合点定义；不是有限线段距离或边缘对宽度。
/// 端点间距须大于1e-9像素且可表示；可来自独立托管拟合快照，原生上下文无需存活。
/// </summary>
public sealed class PixelLine
{
    public Guid FrameId
    {
        get;
    }

    public Guid CoordinateSpaceId
    {
        get;
    }

    public Point2D Start
    {
        get;
    }

    public Point2D End
    {
        get;
    }

    public PixelLine(Guid frameId, Guid coordinateSpaceId, Point2D start, Point2D end)
    {
        var length = double.Hypot(end.X - start.X, end.Y - start.Y);
        if (frameId == Guid.Empty || coordinateSpaceId == Guid.Empty
            || !Finite(start) || !Finite(end) || !double.IsFinite(length) || length <= 1e-9)
        {
            throw new ArgumentException("像素直线必须具有有效来源与可表示的非零长度。");
        }

        FrameId = frameId;
        CoordinateSpaceId = coordinateSpaceId;
        Start = start;
        End = end;
    }

    public static PixelLine FromCaliper(CaliperLineOutput source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Line is not { } fit)
        {
            throw new ArgumentException("本次没有有效卡尺直线，不能沿用旧结果。", nameof(source));
        }

        return new(source.FrameId, source.CoordinateSpaceId, fit.Start, fit.End);
    }

    private static bool Finite(Point2D point) => double.IsFinite(point.X) && double.IsFinite(point.Y);
}
