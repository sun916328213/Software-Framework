namespace Vision.Core.Geometry;

/// <summary>
/// 卡尺阵列和连续边缘搜索带是不同的工具几何，不属于普通面积 ROI。
/// </summary>
public enum LineSearchKind
{
    Calipers, EdgeBand
}

/// <summary>
/// 由实际运行参数实现，图像编辑直接修改原参数；派生几何成员应显式实现，避免被当作可编辑配方字段。
/// </summary>
public interface ILineSearchRegion
{
    Point2D LineStart
    {
        get;
        set;
    }
    Point2D LineEnd
    {
        get;
        set;
    }
    LineSearchKind SearchKind
    {
        get;
    }
    double NormalHalfLength
    {
        get;
        set;
    }
    double TangentHalfWidth
    {
        get;
    }
    int SampleCount
    {
        get;
    }
}

public static class LineSearchGeometry
{
    /// <summary>
    /// 当前位姿∘基准逆位姿；仅返回两个新值，宽度/数量由单位尺度刚体保持，不改写配方。
    /// </summary>
    public static (Point2D Start, Point2D End) FollowFixture(ILineSearchRegion region, Pose2D reference, Pose2D? current)
    {
        _ = Normal(region);
        if (current is not { } pose)
        {
            return (region.LineStart, region.LineEnd);
        }

        if (!Finite(reference) || !Finite(pose))
        {
            throw new ArgumentException("定位位姿必须有限。");
        }

        var start = pose.Transform(reference.InverseTransform(region.LineStart));
        var end = pose.Transform(reference.InverseTransform(region.LineEnd));
        if (!double.IsFinite(start.X) || !double.IsFinite(start.Y) || !double.IsFinite(end.X) || !double.IsFinite(end.Y) ||
            !double.IsFinite(start.DistanceTo(end)) || start == end)
        {
            throw new ArgumentException("定位后的找线路径退化或不可表示。");
        }

        return (start, end);
    }

    private static bool Finite(Pose2D pose) => double.IsFinite(pose.X) && double.IsFinite(pose.Y) && double.IsFinite(pose.AngleDeg);

    /// <summary>
    /// 与 HALCON 的 phi=atan2(-dy,dx)+pi/2 对应的图像坐标测量轴。
    /// </summary>
    public static Point2D Normal(ILineSearchRegion region)
    {
        var dx = region.LineEnd.X - region.LineStart.X;
        var dy = region.LineEnd.Y - region.LineStart.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (!double.IsFinite(length) || length <= 0 || !double.IsFinite(region.NormalHalfLength) || region.NormalHalfLength <= 0)
        {
            throw new ArgumentException("找线路径必须是有限、非零线段，测量半长必须为正。");
        }

        return new(dy / length, -dx / length);
    }

    /// <summary>
    /// 统一编辑外框，不是算法搜索域；卡尺模式包含端点处的沿线半宽，真实采样仍由 Rectangles 表达。
    /// </summary>
    public static Roi.Roi EditingFrame(ILineSearchRegion region)
    {
        _ = Normal(region);
        var extension = region.SearchKind == LineSearchKind.Calipers ? region.TangentHalfWidth : 0;
        if (!double.IsFinite(extension) || extension < 0)
        {
            throw new ArgumentException("编辑外框的沿线半宽无效。");
        }

        var dx = region.LineEnd.X - region.LineStart.X;
        var dy = region.LineEnd.Y - region.LineStart.Y;
        return Roi.Roi.RotatedRectangle((region.LineStart.X + region.LineEnd.X) / 2,
            (region.LineStart.Y + region.LineEnd.Y) / 2, Math.Sqrt(dx * dx + dy * dy) + 2 * extension,
            2 * region.NormalHalfLength, Math.Atan2(dy, dx) * 180 / Math.PI);
    }

    public static IReadOnlyList<Roi.Roi> Rectangles(ILineSearchRegion region)
    {
        var n = Normal(region);
        if (region.SearchKind == LineSearchKind.EdgeBand)
        {
            var dx = region.LineEnd.X - region.LineStart.X;
            var dy = region.LineEnd.Y - region.LineStart.Y;
            return [Roi.Roi.RotatedRectangle((region.LineStart.X + region.LineEnd.X) / 2,
                (region.LineStart.Y + region.LineEnd.Y) / 2, Math.Sqrt(dx * dx + dy * dy),
                2 * region.NormalHalfLength, Math.Atan2(dy, dx) * 180 / Math.PI)];
        }
        if (region.SampleCount < 2 || region.SampleCount > 10000 || !double.IsFinite(region.TangentHalfWidth) || region.TangentHalfWidth <= 0)
        {
            throw new ArgumentException("卡尺数量或半宽无效。");
        }

        var rectangles = new List<Roi.Roi>(region.SampleCount);
        for (var i = 0; i < region.SampleCount; i++)
        {
            var t = (double)i / (region.SampleCount - 1);
            rectangles.Add(Roi.Roi.RotatedRectangle(region.LineStart.X + (region.LineEnd.X - region.LineStart.X) * t,
                region.LineStart.Y + (region.LineEnd.Y - region.LineStart.Y) * t,
                2 * region.NormalHalfLength, 2 * region.TangentHalfWidth, Math.Atan2(n.Y, n.X) * 180 / Math.PI));
        }
        return rectangles;
    }
}
