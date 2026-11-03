#nullable enable
using Vision.Core.Coordinate;

namespace Vision.Core.Calibration;

/// <summary>
/// 像素→毫米世界平面的二维仿射值及显式逆映射，允许坐标轴反向，不提供透视/畸变校正。
/// 残差只衡量参考点拟合，不是独立精度验收；矩阵为值拷贝，不暴露SDK对象。
/// </summary>
public sealed class AffinePixelCalibration
{
    public Guid CalibrationId
    {
        get;
    }

    public Guid ReferenceFrameId
    {
        get;
    }

    public Guid PixelSpaceId
    {
        get;
    }

    public Guid WorldSpaceId
    {
        get;
    }

    public AffineTransform2D PixelToMillimeters
    {
        get;
    }

    public AffineTransform2D MillimetersToPixel
    {
        get;
    }

    public double DesignConditionNumber
    {
        get;
    }

    public double TransformConditionNumber
    {
        get;
    }

    public double RmsResidualMillimeters
    {
        get;
    }

    public double MaximumResidualMillimeters
    {
        get;
    }

    public IReadOnlyList<CalibrationPointPair> ReferencePairs
    {
        get;
    }

    public IReadOnlyList<double> ResidualsMillimeters
    {
        get;
    }

    public bool ReversesOrientation => RelativeDeterminant(PixelToMillimeters) < 0;

    public AffinePixelCalibration(Guid calibrationId, AffineTransform2D pixelToMillimeters, AffineTransform2D millimetersToPixel,
        double designConditionNumber, double transformConditionNumber, double rmsResidualMillimeters,
        double maximumResidualMillimeters, IEnumerable<CalibrationPointPair> referencePairs, IEnumerable<double> residualsMillimeters)
    {
        ArgumentNullException.ThrowIfNull(referencePairs);
        ArgumentNullException.ThrowIfNull(residualsMillimeters);
        var pairs = referencePairs.Take(4097).ToArray();
        var residuals = residualsMillimeters.Take(4097).ToArray();
        if (calibrationId == Guid.Empty || pairs.Length < 3 || pairs.Length > 4096 || pairs.Any(pair => pair == null)
            || pairs.Length != residuals.Length || residuals.Any(value => !double.IsFinite(value) || value < 0)
            || !Finite(pixelToMillimeters) || !Finite(millimetersToPixel)
            || RelativeDeterminant(pixelToMillimeters) == 0 || RelativeDeterminant(millimetersToPixel) == 0
            || !double.IsFinite(designConditionNumber) || designConditionNumber < 1
            || !double.IsFinite(transformConditionNumber) || transformConditionNumber < 1
            || !double.IsFinite(rmsResidualMillimeters) || rmsResidualMillimeters < 0
            || !double.IsFinite(maximumResidualMillimeters) || maximumResidualMillimeters < rmsResidualMillimeters)
        {
            throw new ArgumentException("仿射标定矩阵、参考点、条件数或残差无效。");
        }

        var first = pairs[0];
        if (pairs.Any(pair => pair.Pixel.FrameId != first.Pixel.FrameId
            || pair.Pixel.CoordinateSpaceId != first.Pixel.CoordinateSpaceId || pair.World.WorldSpaceId != first.World.WorldSpaceId))
        {
            throw new ArgumentException("标定参考点必须属于同一参考帧、像素空间及毫米世界空间。");
        }

        var scale = LinearScale(pixelToMillimeters);
        var determinant = RelativeDeterminant(pixelToMillimeters);
        var ia = (pixelToMillimeters.D / scale) / determinant / scale;
        var ib = -(pixelToMillimeters.B / scale) / determinant / scale;
        var ic = -(pixelToMillimeters.C / scale) / determinant / scale;
        var id = (pixelToMillimeters.A / scale) / determinant / scale;
        var inverse = new AffineTransform2D(ia, ib, -(ia * pixelToMillimeters.Tx + ib * pixelToMillimeters.Ty),
            ic, id, -(ic * pixelToMillimeters.Tx + id * pixelToMillimeters.Ty));
        if (!Finite(inverse) || !Near(inverse.A, millimetersToPixel.A) || !Near(inverse.B, millimetersToPixel.B)
            || !Near(inverse.C, millimetersToPixel.C) || !Near(inverse.D, millimetersToPixel.D)
            || !Near(inverse.Tx, millimetersToPixel.Tx) || !Near(inverse.Ty, millimetersToPixel.Ty)
            || maximumResidualMillimeters != residuals.Max())
        {
            throw new ArgumentException("标定逆矩阵或最大残差与正向矩阵/样本不一致。");
        }
        var expectedRms = maximumResidualMillimeters == 0 ? 0 : Math.Min(maximumResidualMillimeters,
            maximumResidualMillimeters * Math.Sqrt(residuals.Average(value => Math.Pow(value / maximumResidualMillimeters, 2))));
        if (rmsResidualMillimeters != expectedRms)
        {
            throw new ArgumentException("RMS残差必须由本次全部参考点计算，不得虚构零误差。");
        }
        for (var index = 0; index < pairs.Length; index++)
        {
            var mapped = pixelToMillimeters.Transform(pairs[index].Pixel.Position);
            var target = pairs[index].World.PositionMillimeters;
            if (residuals[index] != double.Hypot(mapped.X - target.X, mapped.Y - target.Y))
            {
                throw new ArgumentException("标定残差必须对应同序参考点的毫米重投影误差。");
            }
        }

        CalibrationId = calibrationId;
        ReferenceFrameId = first.Pixel.FrameId;
        PixelSpaceId = first.Pixel.CoordinateSpaceId;
        WorldSpaceId = first.World.WorldSpaceId;
        PixelToMillimeters = pixelToMillimeters;
        MillimetersToPixel = millimetersToPixel;
        DesignConditionNumber = designConditionNumber;
        TransformConditionNumber = transformConditionNumber;
        RmsResidualMillimeters = rmsResidualMillimeters;
        MaximumResidualMillimeters = maximumResidualMillimeters;
        ReferencePairs = Array.AsReadOnly(pairs);
        ResidualsMillimeters = Array.AsReadOnly(residuals);
    }

    private static double LinearScale(AffineTransform2D value)
        => Math.Max(Math.Max(Math.Abs(value.A), Math.Abs(value.B)), Math.Max(Math.Abs(value.C), Math.Abs(value.D)));

    private static double RelativeDeterminant(AffineTransform2D value)
    {
        var scale = LinearScale(value);
        return scale == 0 ? 0 : value.A / scale * (value.D / scale) - value.B / scale * (value.C / scale);
    }

    private static bool Near(double first, double second)
        => first == second || Math.Abs(first - second) <= 1e-10 * Math.Max(Math.Abs(first), Math.Abs(second));

    private static bool Finite(AffineTransform2D value) => double.IsFinite(value.A) && double.IsFinite(value.B)
        && double.IsFinite(value.C) && double.IsFinite(value.D) && double.IsFinite(value.Tx) && double.IsFinite(value.Ty);
}
