#nullable enable
using Vision.Core.Geometry;

namespace Vision.Core.Results;

/// <summary>圆心、半径及径向残差快照；CoverageDegrees为360度减内点最大角间隙，不是配方搜索角。</summary>
public sealed class CaliperCircleFit
{
    public Point2D Center
    {
        get;
    }

    public double RadiusPixels
    {
        get;
    }

    public int PointCount
    {
        get;
    }

    public double RmsRadialResidualPixels
    {
        get;
    }

    public double MaximumRadialResidualPixels
    {
        get;
    }

    public double CoverageDegrees
    {
        get;
    }

    public ScalarMeasurementOutput Diameter
    {
        get;
    }

    public CaliperCircleFit(Point2D center, double radiusPixels, int pointCount,
        double rmsRadialResidualPixels, double maximumRadialResidualPixels, double coverageDegrees,
        ScalarMeasurementOutput diameter)
    {
        ArgumentNullException.ThrowIfNull(diameter);
        if (!double.IsFinite(center.X) || !double.IsFinite(center.Y) || !double.IsFinite(radiusPixels) || radiusPixels <= 0
            || pointCount < 3 || !double.IsFinite(rmsRadialResidualPixels) || rmsRadialResidualPixels < 0
            || !double.IsFinite(maximumRadialResidualPixels) || maximumRadialResidualPixels < rmsRadialResidualPixels
            || !double.IsFinite(coverageDegrees) || coverageDegrees <= 0 || coverageDegrees >= 360
            || diameter.Kind != GeometryMeasurementKind.CaliperCircleDiameter || diameter.Value != 2 * radiusPixels)
        {
            throw new ArgumentException("圆几何、径向残差、覆盖角或直径测量无效。");
        }

        Center = center;
        RadiusPixels = radiusPixels;
        PointCount = pointCount;
        RmsRadialResidualPixels = rmsRadialResidualPixels;
        MaximumRadialResidualPixels = maximumRadialResidualPixels;
        CoverageDegrees = coverageDegrees;
        Diameter = diameter;
    }
}
