#nullable enable
using Vision.Core.Geometry;

namespace Vision.Core.Results;

/// <summary>
/// 被保留边缘点的拟合直线快照；端点为这些点在直线上的投影极值，按基准线方向排列。
/// 残差是正交距离，单位像素，不是灰度、置信度或毫米精度承诺。
/// </summary>
public sealed class CaliperLineFit
{
    public Point2D Start
    {
        get;
    }

    public Point2D End
    {
        get;
    }

    public int PointCount
    {
        get;
    }

    public double RmsResidualPixels
    {
        get;
    }

    public double MaximumResidualPixels
    {
        get;
    }

    public CaliperLineFit(Point2D start, Point2D end, int pointCount,
        double rmsResidualPixels, double maximumResidualPixels)
    {
        if (!Finite(start) || !Finite(end) || !double.IsFinite(start.DistanceTo(end)) || start.DistanceTo(end) <= 1e-6
            || pointCount < 2 || !double.IsFinite(rmsResidualPixels) || !double.IsFinite(maximumResidualPixels)
            || rmsResidualPixels < 0 || maximumResidualPixels < rmsResidualPixels)
        {
            throw new ArgumentException("直线、点数或正交残差无效。");
        }

        Start = start;
        End = end;
        PointCount = pointCount;
        RmsResidualPixels = rmsResidualPixels;
        MaximumResidualPixels = maximumResidualPixels;
    }

    private static bool Finite(Point2D point) => double.IsFinite(point.X) && double.IsFinite(point.Y);
}
