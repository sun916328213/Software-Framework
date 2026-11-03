#nullable enable
using Vision.Core.Geometry;

namespace Vision.Core.Results;

/// <summary>
/// 零基卡尺的本次配对诊断。未找到完整合格对时两边缘/宽度都为空、对比度为0；
/// QualifiedEdgeCount为阈值后峰数，CandidatePairCount为极性/宽度过滤后的相邻峰对数。
/// </summary>
public sealed class CaliperEdgePairSample
{
    public int Index
    {
        get;
    }

    public Point2D Center
    {
        get;
    }

    public int QualifiedEdgeCount
    {
        get;
    }

    public int CandidatePairCount
    {
        get;
    }

    public Point2D? FirstEdge
    {
        get;
    }

    public Point2D? SecondEdge
    {
        get;
    }

    public double FirstContrast
    {
        get;
    }

    public double SecondContrast
    {
        get;
    }

    public double? WidthPixels
    {
        get;
    }

    public CaliperEdgePairSample(int index, Point2D center, int qualifiedEdgeCount, int candidatePairCount,
        Point2D? firstEdge, Point2D? secondEdge, double firstContrast, double secondContrast, double? widthPixels)
    {
        if (index < 0 || !Finite(center) || qualifiedEdgeCount < 0 || candidatePairCount < 0
            || candidatePairCount > Math.Max(0, qualifiedEdgeCount - 1)
            || !double.IsFinite(firstContrast) || !double.IsFinite(secondContrast))
        {
            throw new ArgumentException("边缘对诊断序号、峰数或对比度无效。");
        }

        var complete = firstEdge != null && secondEdge != null && widthPixels != null;
        if (complete)
        {
            if (!Finite(firstEdge!.Value) || !Finite(secondEdge!.Value)
                || !double.IsFinite(widthPixels!.Value) || widthPixels.Value <= 0
                || candidatePairCount < 1 || firstContrast == 0 || secondContrast == 0
                || Math.Sign(firstContrast) == Math.Sign(secondContrast))
            {
                throw new ArgumentException("完整边缘对必须具有有限正宽度及异极性边缘。");
            }
        }
        else if (firstEdge != null || secondEdge != null || widthPixels != null
            || firstContrast != 0 || secondContrast != 0 || candidatePairCount != 0)
        {
            throw new ArgumentException("缺少完整边缘对时不能发布半边测量或伪造宽度。");
        }

        Index = index;
        Center = center;
        QualifiedEdgeCount = qualifiedEdgeCount;
        CandidatePairCount = candidatePairCount;
        FirstEdge = firstEdge;
        SecondEdge = secondEdge;
        FirstContrast = firstContrast;
        SecondContrast = secondContrast;
        WidthPixels = widthPixels;
    }

    private static bool Finite(Point2D point) => double.IsFinite(point.X) && double.IsFinite(point.Y);
}
