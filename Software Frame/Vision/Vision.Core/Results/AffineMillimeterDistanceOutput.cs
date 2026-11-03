#nullable enable

namespace Vision.Core.Results;

/// <summary>先分别映射两像素点再在毫米世界空间求距，不用单比例换算非等比/剪切下的像素标量。</summary>
public sealed class AffineMillimeterDistanceOutput : IMillimeterLengthMeasurement
{
    public CalibratedMillimeterPointOutput First
    {
        get;
    }

    public CalibratedMillimeterPointOutput Second
    {
        get;
    }

    public double ValueMillimeters
    {
        get;
    }

    public Guid FrameId => First.Source.FrameId;
    public Guid CoordinateSpaceId => First.Source.CoordinateSpaceId;
    public Guid CalibrationId => First.Calibration.CalibrationId;

    public AffineMillimeterDistanceOutput(CalibratedMillimeterPointOutput first, CalibratedMillimeterPointOutput second, double valueMillimeters)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        var a = first.Point.PositionMillimeters;
        var b = second.Point.PositionMillimeters;
        if (!ReferenceEquals(first.Calibration, second.Calibration)
            || first.Source.FrameId != second.Source.FrameId || first.Source.CoordinateSpaceId != second.Source.CoordinateSpaceId
            || !double.IsFinite(valueMillimeters) || valueMillimeters < 0
            || valueMillimeters == 0 && first.Source.Position != second.Source.Position
            || valueMillimeters != double.Hypot(b.X - a.X, b.Y - a.Y))
        {
            throw new ArgumentException("毫米点距必须来自同帧/空间/同一标定，且为有限欧氏距离。");
        }
        First = first;
        Second = second;
        ValueMillimeters = valueMillimeters;
    }
}
