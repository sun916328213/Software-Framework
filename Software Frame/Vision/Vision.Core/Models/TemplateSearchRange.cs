#nullable enable
using Vision.Core.Geometry;

namespace Vision.Core.Models;

/// <summary>
/// 旋转搜索为起角+非负范围，允许跨越±180°；缩放只允许正的等比例缩放。
/// </summary>
public sealed class TemplateSearchRange
{
    // 仅吸收归一化角度在一个圆周内运算的 double 舍入误差，不代表算法采样步长或测量精度。
    private const double AngularBoundaryToleranceDegrees = 1e-12;

    /// <summary>
    /// 圆周搜索区间的起始角度，单位为度。
    /// </summary>
    public double AngleStartDegrees
    {
        get;
    }

    /// <summary>
    /// 角度搜索跨度，范围为零到三百六十度。
    /// </summary>
    public double AngleExtentDegrees
    {
        get;
    }

    /// <summary>
    /// 搜索允许的最小正等比例缩放。
    /// </summary>
    public double ScaleMinimum
    {
        get;
    }

    /// <summary>
    /// 搜索允许的最大等比例缩放。
    /// </summary>
    public double ScaleMaximum
    {
        get;
    }

    /// <summary>
    /// 创建有效的圆周角度和正等比例缩放搜索范围。
    /// </summary>
    public TemplateSearchRange(
        double angleStartDegrees = 0,
        double angleExtentDegrees = 0,
        double scaleMinimum = 1,
        double scaleMaximum = 1)
    {
        if (!double.IsFinite(angleStartDegrees))
        {
            throw new ArgumentOutOfRangeException(nameof(angleStartDegrees));
        }

        if (!double.IsFinite(angleExtentDegrees) || angleExtentDegrees < 0 || angleExtentDegrees > 360)
        {
            throw new ArgumentOutOfRangeException(nameof(angleExtentDegrees));
        }

        if (!double.IsFinite(scaleMinimum) || scaleMinimum <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(scaleMinimum));
        }

        if (!double.IsFinite(scaleMaximum) || scaleMaximum < scaleMinimum)
        {
            throw new ArgumentOutOfRangeException(nameof(scaleMaximum));
        }

        AngleStartDegrees = Pose2D.NormalizeAngle(angleStartDegrees);
        AngleExtentDegrees = angleExtentDegrees;
        ScaleMinimum = scaleMinimum;
        ScaleMaximum = scaleMaximum;
    }

    /// <summary>
    /// 运行搜索不能超出建模范围；按圆周区间判断，不能只比较角度端点。
    /// 角度边界使用微小数值容差吸收舍入误差，缩放边界仍严格比较。
    /// </summary>
    public bool Contains(TemplateSearchRange search)
    {
        ArgumentNullException.ThrowIfNull(search);
        if (search.ScaleMinimum < ScaleMinimum || search.ScaleMaximum > ScaleMaximum)
        {
            return false;
        }

        if (AngleExtentDegrees == 360)
        {
            return true;
        }

        var offset = (search.AngleStartDegrees - AngleStartDegrees) % 360;
        if (offset < 0)
        {
            offset += 360;
        }

        // 等价起角可能因归一化误差落在圆周末端，应按同一起角处理。
        if (360 - offset <= AngularBoundaryToleranceDegrees)
        {
            offset = 0;
        }

        return offset + search.AngleExtentDegrees <= AngleExtentDegrees + AngularBoundaryToleranceDegrees;
    }
}
