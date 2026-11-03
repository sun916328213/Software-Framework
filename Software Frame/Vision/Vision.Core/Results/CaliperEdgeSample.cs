#nullable enable
using Vision.Core.Geometry;

namespace Vision.Core.Results;

/// <summary>
/// 一把卡尺的托管诊断快照；Index为零基卡尺位置，不是目标跟踪ID。
/// 无边缘时Edge/Residual为空、对比度为0；被剔除点保留坐标与最终拟合几何距离（直线正交/圆径向）。
/// </summary>
public sealed class CaliperEdgeSample
{
    public int Index
    {
        get;
    }

    public Point2D Center
    {
        get;
    }

    public Point2D? Edge
    {
        get;
    }

    public double SignedContrast
    {
        get;
    }

    public bool UsedInFit
    {
        get;
    }

    public double? ResidualPixels
    {
        get;
    }

    public CaliperEdgeSample(int index, Point2D center, Point2D? edge,
        double signedContrast, bool usedInFit, double? residualPixels)
    {
        if (index < 0 || !Finite(center) || edge is { } point && !Finite(point)
            || !double.IsFinite(signedContrast)
            || residualPixels is { } residual && (!double.IsFinite(residual) || residual < 0)
            || edge == null && (signedContrast != 0 || usedInFit || residualPixels != null)
            || edge != null && signedContrast == 0 || usedInFit && residualPixels == null)
        {
            throw new ArgumentException("卡尺样本坐标、边缘或残差状态无效。");
        }

        Index = index;
        Center = center;
        Edge = edge;
        SignedContrast = signedContrast;
        UsedInFit = usedInFit;
        ResidualPixels = residualPixels;
    }

    private static bool Finite(Point2D point) => double.IsFinite(point.X) && double.IsFinite(point.Y);
}
