namespace Vision.Core.Geometry;

/// <summary>
/// 区域边界或模型轮廓（X=列，Y=行）；区域使用输入图像像素中心，模型可使用局部坐标。
/// 外边界和孔洞分别保存；允许单点/线段退化区域，不代表外接框。
/// 只拥有托管坐标快照，不依赖任何 SDK 或显示类型。
/// </summary>
public sealed class RegionContour
{
    public IReadOnlyList<Point2D> Points
    {
        get;
    }

    public bool IsHole
    {
        get;
    }

    public bool IsClosed
    {
        get;
    }

    public RegionContour(IReadOnlyList<Point2D> points, bool isHole = false, bool isClosed = true)
    {
        ArgumentNullException.ThrowIfNull(points);
        Points = Array.AsReadOnly(points.ToArray());
        IsHole = isHole;
        IsClosed = isClosed;
    }
}
