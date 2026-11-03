#nullable enable
using Vision.Core.Coordinate;
using Vision.Core.Geometry;

namespace Vision.Core.Results;

/// <summary>
/// 模型锚点在图中的位置。正角度在Y向下图像上为顺时针，比例只允许正的等比例缩放。
/// </summary>
public sealed class TemplateMatchCandidate
{
    /// <summary>
    /// 模型锚点在搜索图像中的像素坐标。
    /// </summary>
    public Point2D Position
    {
        get;
    }

    /// <summary>
    /// 模板局部坐标中的原始锚点。
    /// </summary>
    public Point2D ModelOrigin
    {
        get;
    }

    /// <summary>
    /// 目标相对模板的旋转角度，单位为度，图像上顺时针为正。
    /// </summary>
    public double AngleDegrees
    {
        get;
    }

    /// <summary>
    /// 目标相对模板的正等比例缩放。
    /// </summary>
    public double Scale
    {
        get;
    }

    /// <summary>
    /// 匹配或分类得分，范围为零到一。
    /// </summary>
    public double Score
    {
        get;
    }

    /// <summary>
    /// 由锚点位置、角度和尺度推导的模板到图像仿射变换。
    /// </summary>
    public AffineTransform2D TemplateToImage
    {
        get
        {
            var (cos, sin) = RotationMath.SinCosDegrees(AngleDegrees);
            var a = Scale * cos;
            var b = -Scale * sin;
            var c = Scale * sin;
            var d = Scale * cos;
            return new(a, b, Position.X - a * ModelOrigin.X - b * ModelOrigin.Y,
                c, d, Position.Y - c * ModelOrigin.X - d * ModelOrigin.Y);
        }
    }

    /// <summary>
    /// 创建匹配候选，校验几何、尺度、得分和仿射变换可表示性。
    /// </summary>
    public TemplateMatchCandidate(
        Point2D position,
        Point2D modelOrigin,
        double angleDegrees,
        double scale,
        double score)
    {
        if (!double.IsFinite(position.X) || !double.IsFinite(position.Y) ||
            !double.IsFinite(modelOrigin.X) || !double.IsFinite(modelOrigin.Y))
        {
            throw new ArgumentException("位置和锚点必须有限。");
        }

        if (!double.IsFinite(angleDegrees))
        {
            throw new ArgumentOutOfRangeException(nameof(angleDegrees));
        }

        if (!double.IsFinite(scale) || scale <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(scale));
        }

        if (!double.IsFinite(score) || score < 0 || score > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(score));
        }

        Position = position;
        ModelOrigin = modelOrigin;
        AngleDegrees = Pose2D.NormalizeAngle(angleDegrees);
        Scale = scale;
        Score = score;
        var transform = TemplateToImage;
        if (!double.IsFinite(transform.A) || !double.IsFinite(transform.B) || !double.IsFinite(transform.C) ||
            !double.IsFinite(transform.D) || !double.IsFinite(transform.Tx) || !double.IsFinite(transform.Ty))
        {
            throw new ArgumentException("锚点/缩放组合导致变换溢出。");
        }
    }
}
