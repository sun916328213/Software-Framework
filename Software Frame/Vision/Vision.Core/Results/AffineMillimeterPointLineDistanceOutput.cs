#nullable enable
using Vision.Core.Geometry;

namespace Vision.Core.Results;

/// <summary>
/// 带原像素点/线及同一仿射标定的毫米垂距，无方向，针对无限直线，不是线段端点距或扫描轴边缘点距。
/// 只表达本次点到直线的几何距离，不单凭它断言两条实际工件边界处处平行、等厚。
/// </summary>
public sealed class AffineMillimeterPointLineDistanceOutput : IMillimeterLengthMeasurement
{
    public CalibratedMillimeterPointOutput Point
    {
        get;
    }

    public CalibratedMillimeterLineOutput Line
    {
        get;
    }

    public double ValueMillimeters
    {
        get;
    }

    public Guid FrameId => Point.Source.FrameId;
    public Guid CoordinateSpaceId => Point.Source.CoordinateSpaceId;
    public Guid CalibrationId => Point.Calibration.CalibrationId;

    public AffineMillimeterPointLineDistanceOutput(CalibratedMillimeterPointOutput point, CalibratedMillimeterLineOutput line, double valueMillimeters)
    {
        ArgumentNullException.ThrowIfNull(point);
        ArgumentNullException.ThrowIfNull(line);
        PixelGeometryValidation.RequireSameSource(point.Source.FrameId, point.Source.CoordinateSpaceId,
            line.Source.FrameId, line.Source.CoordinateSpaceId);
        var start = line.Start.PositionMillimeters;
        var end = line.End.PositionMillimeters;
        var position = point.Point.PositionMillimeters;
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var length = double.Hypot(dx, dy);
        var expected = Math.Abs((position.X - start.X) * (dy / length) - (position.Y - start.Y) * (dx / length));
        var pixelDx = line.Source.End.X - line.Source.Start.X;
        var pixelDy = line.Source.End.Y - line.Source.Start.Y;
        var pixelLength = double.Hypot(pixelDx, pixelDy);
        var pixelDistance = Math.Abs((point.Source.Position.X - line.Source.Start.X) * (pixelDy / pixelLength)
            - (point.Source.Position.Y - line.Source.Start.Y) * (pixelDx / pixelLength));
        if (!ReferenceEquals(point.Calibration, line.Calibration)
            || !double.IsFinite(valueMillimeters) || valueMillimeters < 0 || valueMillimeters != expected
            || valueMillimeters == 0 && pixelDistance != 0)
        {
            throw new ArgumentException("毫米垂距必须来自同一标定且有限，不发布非共线像素几何被数值压成的假0。");
        }
        Point = point;
        Line = line;
        ValueMillimeters = valueMillimeters;
    }
}
