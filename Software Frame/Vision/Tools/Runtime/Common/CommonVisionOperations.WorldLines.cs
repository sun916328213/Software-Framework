using Vision.Core.Geometry;
using Vision.Core.Operations;
using Vision.Core.Results;
using Vision.Tools.Execution;

namespace Vision.Tools.Common;

internal static partial class CommonVisionOperations
{
    private static BackendOutput MapPixelLine(PixelLineToMillimetersOperation operation)
    {
        var calibration = operation.Calibration;
        return new(new CalibratedMillimeterLineOutput(operation.Source, calibration,
            new MillimeterPoint(calibration.WorldSpaceId, calibration.PixelToMillimeters.Transform(operation.Source.Start)),
            new MillimeterPoint(calibration.WorldSpaceId, calibration.PixelToMillimeters.Transform(operation.Source.End))));
    }

    private static BackendOutput MeasureCalibratedPointLineDistance(CalibratedPointLineDistanceOperation operation)
    {
        var start = operation.Line.Start.PositionMillimeters;
        var end = operation.Line.End.PositionMillimeters;
        var point = operation.Point.Point.PositionMillimeters;
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var length = double.Hypot(dx, dy);
        // 必须在已完整映射的毫米欧氏几何中重算法向，不能把像素法向直接作仿射变换后乘比例。
        var distance = Math.Abs((point.X - start.X) * (dy / length) - (point.Y - start.Y) * (dx / length));
        return new(new AffineMillimeterPointLineDistanceOutput(operation.Point, operation.Line, distance));
    }
}
