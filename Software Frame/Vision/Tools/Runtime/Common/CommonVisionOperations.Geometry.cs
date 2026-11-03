using Vision.Core.Engine;
using Vision.Core.Geometry;
using Vision.Core.Operations;
using Vision.Core.Results;
using Vision.Tools.Execution;

namespace Vision.Tools.Common;

internal static partial class CommonVisionOperations
{
    private static BackendOutput MeasurePointDistance(PointDistanceOperation operation)
    {
        var first = operation.First.Position;
        var second = operation.Second.Position;
        // Hypot避免直接平方造成可表示大距离的中间溢出；真正不可表示的结果仍失败，不发布NaN/Inf。
        var value = double.Hypot(second.X - first.X, second.Y - first.Y);
        return Scalar(operation.First.FrameId, operation.First.CoordinateSpaceId, GeometryMeasurementKind.PointDistance, value);
    }

    private static BackendOutput MeasurePointLineDistance(PointLineDistanceOperation operation)
    {
        var direction = UnitDirection(operation.Line);
        var delta = Delta(operation.Point.Position, operation.Line.Start);
        return Scalar(operation.Point.FrameId, operation.Point.CoordinateSpaceId,
            GeometryMeasurementKind.PointLineDistance, Math.Abs(Cross(delta, direction)));
    }

    private static BackendOutput MeasureLineAngle(LineAngleOperation operation)
    {
        var first = UnitDirection(operation.First);
        var second = UnitDirection(operation.Second);
        var cross = Cross(first, second);
        var dot = first.X * second.X + first.Y * second.Y;
        var directed = operation.Mode == LineAngleMode.Directed;
        var value = Math.Atan2(directed ? cross : Math.Abs(cross), directed ? dot : Math.Abs(dot)) * (180 / Math.PI);
        if (directed && value <= -180)
        {
            value = 180;
        }

        return Scalar(operation.First.FrameId, operation.First.CoordinateSpaceId,
            directed ? GeometryMeasurementKind.DirectedLineAngle : GeometryMeasurementKind.UndirectedLineAngle, value);
    }

    private static BackendOutput IntersectLines(LineIntersectionOperation operation)
    {
        var first = UnitDirection(operation.First);
        var second = UnitDirection(operation.Second);
        var denominator = Cross(first, second);
        // 单位方向叉积的理论范围为[-1,1]，只吸收归一化的末位舍入，不改角度稳定性判据。
        var sinAngle = Math.Min(1, Math.Abs(denominator));
        if (sinAngle < operation.MinimumSinAngle)
        {
            return new(new LineIntersectionOutput(operation.First.FrameId, operation.First.CoordinateSpaceId, null, sinAngle), true);
        }

        var delta = Delta(operation.Second.Start, operation.First.Start);
        var distance = Cross(delta, second) / denominator;
        var position = new Point2D(operation.First.Start.X + distance * first.X,
            operation.First.Start.Y + distance * first.Y);
        var point = new PixelPoint(operation.First.FrameId, operation.First.CoordinateSpaceId, position);
        return new(new LineIntersectionOutput(point.FrameId, point.CoordinateSpaceId, point, sinAngle));
    }

    private static BackendOutput Scalar(Guid frame, Guid space, GeometryMeasurementKind kind, double value)
        => new(new ScalarMeasurementOutput(frame, space, kind, value));

    private static Point2D UnitDirection(PixelLine line)
    {
        var delta = Delta(line.End, line.Start);
        var length = double.Hypot(delta.X, delta.Y);
        return new(delta.X / length, delta.Y / length);
    }

    private static Point2D Delta(Point2D first, Point2D second) => new(first.X - second.X, first.Y - second.Y);
    private static double Cross(Point2D first, Point2D second) => first.X * second.Y - first.Y * second.X;

    internal static void ValidateOutput(VisionOperation operation, BackendOutput output)
    {
        if (operation is GrayStatisticsRangeOperation gray)
        {
            if (output.NoMatch || output.Value is not GrayStatisticsRangeOutput decision
                || !ReferenceEquals(decision.Source, gray.Source) || decision.Metric != gray.Metric
                || decision.Minimum != gray.Minimum || decision.Maximum != gray.Maximum)
            {
                throw new InvalidOperationException("灰度统计判定与本次来源、指标或范围不符。");
            }
            return;
        }

        if (operation is AiPredictionJudgmentOperation prediction)
        {
            if (output.NoMatch || output.Value is not AiPredictionJudgmentOutput result
                || !ReferenceEquals(result.Source, prediction.Source) || result.PredictionIndex != prediction.PredictionIndex
                || result.ExpectedClassId != prediction.ExpectedClassId || result.MinimumScore != prediction.MinimumScore)
            {
                throw new InvalidOperationException("分类判定与本次快照、显式下标、类别或分数不符。");
            }
            return;
        }

        if (operation is QrDecodedCountOperation count)
        {
            if (output.NoMatch || output.Value is not QrCountJudgmentOutput decision
                || !ReferenceEquals(decision.Source, count.Source) || decision.Minimum != count.Minimum || decision.Maximum != count.Maximum)
            {
                throw new InvalidOperationException("多QR解码计数与本次候选快照或范围不符。");
            }
            return;
        }

        if (operation is QrTextEqualsOperation text)
        {
            if (output.NoMatch || output.Value is not QrTextJudgmentOutput judgment
                || !ReferenceEquals(judgment.Source, text.Source) || judgment.ExpectedText != text.ExpectedText)
            {
                throw new InvalidOperationException("QR文本判定与本次读取快照或期望全文不符。");
            }
            return;
        }

        if (operation is AffineCalibrationOperation or PixelToMillimeterPointOperation
            or MillimeterPointToPixelOperation or CalibratedPointDistanceOperation
            or PixelLineToMillimetersOperation or CalibratedPointLineDistanceOperation)
        {
            ValidateAffineOutput(operation, output);
            return;
        }

        if (operation is PixelScaleCalibrationOperation or LengthToMillimetersOperation or MillimeterRangeOperation)
        {
            ValidateCalibrationOutput(operation, output);
            return;
        }

        var origin = operation switch
        {
            BlobJudgmentOperation judgment => (judgment.Source.FrameId, judgment.Source.CoordinateSpaceId),
            PointDistanceOperation distance => (distance.First.FrameId, distance.First.CoordinateSpaceId),
            PointLineDistanceOperation distance => (distance.Point.FrameId, distance.Point.CoordinateSpaceId),
            LineAngleOperation angle => (angle.First.FrameId, angle.First.CoordinateSpaceId),
            LineIntersectionOperation intersection => (intersection.First.FrameId, intersection.First.CoordinateSpaceId),
            MeasurementRangeOperation range => (range.Source.FrameId, range.Source.CoordinateSpaceId),
            _ => throw new InvalidOperationException("未知公共操作结果。")
        };
        var identity = output.Value switch
        {
            BlobJudgmentOutput judgment => (judgment.FrameId, judgment.CoordinateSpaceId),
            ScalarMeasurementOutput scalar => (scalar.FrameId, scalar.CoordinateSpaceId),
            LineIntersectionOutput intersection => (intersection.FrameId, intersection.CoordinateSpaceId),
            MeasurementRangeOutput range => (range.Measurement.FrameId, range.Measurement.CoordinateSpaceId),
            _ => throw new InvalidOperationException("公共操作返回了未约定结果。")
        };
        if (identity != origin)
        {
            throw new InvalidOperationException("公共几何/判定结果的帧或空间与本次来源不符。");
        }

        if (operation is BlobJudgmentOperation blob)
        {
            if (output.Value is not BlobJudgmentOutput decision || output.NoMatch
                || decision.Metric != blob.Metric || decision.ItemIndex != blob.ItemIndex
                || decision.ActualValue != ReadValue(blob) || decision.Minimum != blob.Minimum || decision.Maximum != blob.Maximum)
            {
                throw new InvalidOperationException("区域判定与本次来源或上下限不符。");
            }
            return;
        }

        if (operation is MeasurementRangeOperation rangeOperation)
        {
            if (output.Value is not MeasurementRangeOutput decision || output.NoMatch
                || !ReferenceEquals(decision.Measurement, rangeOperation.Source)
                || decision.Minimum != rangeOperation.Minimum || decision.Maximum != rangeOperation.Maximum)
            {
                throw new InvalidOperationException("测量公差结果与本次标量或上下限不符。");
            }
            return;
        }

        if (operation is LineIntersectionOperation intersectionOperation)
        {
            if (output.Value is not LineIntersectionOutput intersection
                || output.NoMatch != (intersection.Point == null)
                || (intersection.Point != null) != (intersection.SinAngle >= intersectionOperation.MinimumSinAngle))
            {
                throw new InvalidOperationException("交点、未找到状态或角度稳定性与本次请求不符。");
            }
            return;
        }

        var expectedKind = operation switch
        {
            PointDistanceOperation => GeometryMeasurementKind.PointDistance,
            PointLineDistanceOperation => GeometryMeasurementKind.PointLineDistance,
            LineAngleOperation angle => angle.Mode == LineAngleMode.Directed
                ? GeometryMeasurementKind.DirectedLineAngle : GeometryMeasurementKind.UndirectedLineAngle,
            _ => throw new InvalidOperationException("操作未约定几何标量。")
        };
        if (output.Value is not ScalarMeasurementOutput measurement || output.NoMatch || measurement.Kind != expectedKind)
        {
            throw new InvalidOperationException("几何标量含义或完成状态与本次请求不符。");
        }
    }
}
