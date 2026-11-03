#nullable enable

namespace Vision.Core.Results;

/// <summary>
/// 像素中心闭合折线的几何快照，计算由Tools完成，不携带SDK对象。
/// Area为外轮廓多边形面积减孔洞面积（像素平方），不是前景像素计数。
/// Perimeter包含所有外边界和孔洞的闭合折线长度（像素）；两点线段按往返长度计。
/// Circularity=4π×Area/Perimeter²，无量纲；单点/线段等零面积轮廓为null，不伪造为零圆度。
/// </summary>
public sealed class BlobContourMetrics
{
    public double Area
    {
        get;
    }

    public double Perimeter
    {
        get;
    }

    public double? Circularity
    {
        get;
    }

    public BlobContourMetrics(double area, double perimeter, double? circularity)
    {
        if (!double.IsFinite(area) || !double.IsFinite(perimeter) || area < 0 || perimeter < 0
            || circularity is { } value && (!double.IsFinite(value) || value <= 0 || value > 1)
            || area == 0 && circularity != null
            || area > 0 && (perimeter <= 0 || circularity == null))
        {
            throw new ArgumentException("轮廓几何必须有限、非负；只有正面积及正周长才有有效圆度。");
        }

        Area = area;
        Perimeter = perimeter;
        Circularity = circularity;
    }
}
