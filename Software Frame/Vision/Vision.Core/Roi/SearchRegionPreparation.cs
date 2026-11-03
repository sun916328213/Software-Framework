using Vision.Core.Geometry;

#nullable enable

namespace Vision.Core.Roi;

/// <summary>
/// 单工具与节点共用刚体搜索域准备；每次由基准ROI推导，绝不累积改写几何。
/// </summary>
public static class SearchRegionPreparation
{
    public static SearchRegion? Capture(Roi? referenceRoi, Pose2D reference, Pose2D? current)
    {
        if (referenceRoi == null)
        {
            if (current != null)
            {
                throw new InvalidOperationException("定位跟随需要基准搜索 ROI。");
            }

            return null;
        }
        if (current is not { } pose)
        {
            return new(referenceRoi);
        }

        if (!Finite(reference) || !Finite(pose))
        {
            throw new ArgumentException("定位位姿坐标和角度必须有限。");
        }

        return new(referenceRoi.FollowFixture(reference, pose));
    }

    /// <summary>
    /// 与已准备面积域同一刚体变换的附加点；例如Hough候选排序所用期望圆心。
    /// </summary>
    public static Point2D FollowPoint(Point2D point, Pose2D reference, Pose2D? current)
    {
        if (!double.IsFinite(point.X) || !double.IsFinite(point.Y))
        {
            throw new ArgumentException("参考点必须有限。");
        }

        if (current is not { } pose)
        {
            return point;
        }

        if (!Finite(reference) || !Finite(pose))
        {
            throw new ArgumentException("定位位姿坐标和角度必须有限。");
        }

        var transformed = pose.Transform(reference.InverseTransform(point));
        if (!double.IsFinite(transformed.X) || !double.IsFinite(transformed.Y))
        {
            throw new ArgumentException("定位后的参考点不可表示。");
        }

        return transformed;
    }

    private static bool Finite(Pose2D p) => double.IsFinite(p.X) && double.IsFinite(p.Y) && double.IsFinite(p.AngleDeg);
}
