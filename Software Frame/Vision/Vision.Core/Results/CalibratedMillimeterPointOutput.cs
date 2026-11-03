#nullable enable
using Vision.Core.Calibration;
using Vision.Core.Geometry;

namespace Vision.Core.Results;

/// <summary>带原像素点及标定身份的毫米世界点，不能直接混用另一标定的坐标。</summary>
public sealed class CalibratedMillimeterPointOutput
{
    public PixelPoint Source
    {
        get;
    }

    public AffinePixelCalibration Calibration
    {
        get;
    }

    public MillimeterPoint Point
    {
        get;
    }

    public CalibratedMillimeterPointOutput(PixelPoint source, AffinePixelCalibration calibration, MillimeterPoint point)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(calibration);
        ArgumentNullException.ThrowIfNull(point);
        if (source.CoordinateSpaceId != calibration.PixelSpaceId || point.WorldSpaceId != calibration.WorldSpaceId
            || calibration.PixelToMillimeters.Transform(source.Position) != point.PositionMillimeters)
        {
            throw new ArgumentException("毫米世界点必须来自本次像素空间与明确仿射映射。");
        }
        Source = source;
        Calibration = calibration;
        Point = point;
    }
}
