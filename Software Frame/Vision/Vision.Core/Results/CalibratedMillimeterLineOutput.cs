#nullable enable
using Vision.Core.Calibration;
using Vision.Core.Geometry;

namespace Vision.Core.Results;

/// <summary>
/// 有向像素无限直线完整映射到毫米世界平面。Start/End只定义方向，不将距离限制在有限线段；
/// 长度无与单位相关的固定阈值，但必须有限且非零，不能发布被浮点表示压塌的直线。
/// 映射像素拟合线不等于按毫米欧氏残差重新拟合，也不把原像素拟合残差改为毫米。
/// </summary>
public sealed class CalibratedMillimeterLineOutput
{
    public PixelLine Source
    {
        get;
    }

    public AffinePixelCalibration Calibration
    {
        get;
    }

    public MillimeterPoint Start
    {
        get;
    }

    public MillimeterPoint End
    {
        get;
    }

    public CalibratedMillimeterLineOutput(PixelLine source, AffinePixelCalibration calibration, MillimeterPoint start, MillimeterPoint end)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(calibration);
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(end);
        var first = start.PositionMillimeters;
        var second = end.PositionMillimeters;
        var length = double.Hypot(second.X - first.X, second.Y - first.Y);
        if (source.CoordinateSpaceId != calibration.PixelSpaceId
            || start.WorldSpaceId != calibration.WorldSpaceId || end.WorldSpaceId != calibration.WorldSpaceId
            || calibration.PixelToMillimeters.Transform(source.Start) != first
            || calibration.PixelToMillimeters.Transform(source.End) != second
            || !double.IsFinite(length) || length <= 0)
        {
            throw new ArgumentException("毫米直线必须来自明确仿射映射，且有可表示的非零方向。");
        }
        Source = source;
        Calibration = calibration;
        Start = start;
        End = end;
    }
}
