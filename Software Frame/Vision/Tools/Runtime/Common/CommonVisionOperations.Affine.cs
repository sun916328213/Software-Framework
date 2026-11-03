using Vision.Core.Calibration;
using Vision.Core.Coordinate;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Geometry;
using Vision.Core.Operations;
using Vision.Core.Results;
using Vision.Tools.Execution;

namespace Vision.Tools.Common;

internal static partial class CommonVisionOperations
{
    private static BackendOutput CalibrateAffine(AffineCalibrationOperation operation)
    {
        double? designCondition = null;
        double? transformCondition = null;
        double[] residuals = [];

        BackendOutput Reject(string code, string message)
        {
            var first = operation.Points[0];
            return new(new AffineCalibrationOutput(first.Pixel.FrameId, first.Pixel.CoordinateSpaceId,
                first.World.WorldSpaceId, null, new VisionIssue(code, message), designCondition, transformCondition, residuals), true);
        }

        var pixels = NormalizeCalibrationPoints(operation.Points.Select(pair => pair.Pixel.Position).ToArray());
        if (pixels == null)
        {
            return Reject("UnrepresentableCalibration", "参考像素跨度无法有限表示。");
        }
        if (pixels.Scale == 0)
        {
            return Reject("DegeneratePixelGeometry", "像素参考点重合，不能建立二维仿射。");
        }

        var condition = CalibrationDesignCondition(pixels.Centered);
        if (condition == null)
        {
            return Reject("DegeneratePixelGeometry", "像素参考点共线或秩不足。");
        }
        designCondition = condition;
        if (condition > operation.MaximumConditionNumber)
        {
            return Reject("IllConditionedPixelGeometry", "像素参考点近共线或分布病态，超过设计矩阵条件数限。");
        }

        var world = NormalizeCalibrationPoints(operation.Points.Select(pair => pair.World.PositionMillimeters).ToArray());
        if (world == null)
        {
            return Reject("UnrepresentableCalibration", "毫米参考坐标跨度无法有限表示。");
        }
        if (world.Scale == 0)
        {
            return Reject("DegenerateWorldGeometry", "毫米参考点重合，不能形成可逆二维映射。");
        }

        // 归一化Householder QR解两组最小二乘，不以正规方程求逆，也不丢弃高残差参考点。
        var coefficients = SolveAffineQr(pixels.Centered, world.Centered);
        if (coefficients == null)
        {
            return Reject("UnrepresentableCalibration", "归一化最小二乘无法形成有限解。");
        }
        var ratio = world.Scale / pixels.Scale;
        var a = coefficients[0, 0] * ratio;
        var b = coefficients[1, 0] * ratio;
        var c = coefficients[0, 1] * ratio;
        var d = coefficients[1, 1] * ratio;
        var centerPixel = new Point2D(pixels.Anchor.X + pixels.Scale * pixels.MeanX, pixels.Anchor.Y + pixels.Scale * pixels.MeanY);
        var tx = world.Anchor.X + world.Scale * (world.MeanX + coefficients[2, 0]) - a * centerPixel.X - b * centerPixel.Y;
        var ty = world.Anchor.Y + world.Scale * (world.MeanY + coefficients[2, 1]) - c * centerPixel.X - d * centerPixel.Y;
        var forward = new AffineTransform2D(a, b, tx, c, d, ty);
        if (!FiniteCalibrationMatrix(forward))
        {
            return Reject("UnrepresentableCalibration", "毫米仿射系数无法有限表示。");
        }

        // 按线性部分相对尺度判可逆，不使用与单位相关的绝对行列式阈值。
        var scale = new[] { Math.Abs(a), Math.Abs(b), Math.Abs(c), Math.Abs(d) }.Max();
        if (scale == 0)
        {
            return Reject("SingularCalibrationTransform", "毫米映射线性部分为零。");
        }
        var an = a / scale;
        var bn = b / scale;
        var cn = c / scale;
        var dn = d / scale;
        var determinant = an * dn - bn * cn;
        if (determinant == 0)
        {
            return Reject("SingularCalibrationTransform", "毫米映射奇异，不能建立显式逆变换。");
        }
        var xx = an * an + cn * cn;
        var yy = bn * bn + dn * dn;
        var xy = an * bn + cn * dn;
        var maximumEigenvalue = (xx + yy + double.Hypot(xx - yy, 2 * xy)) * 0.5;
        var mappingCondition = Math.Max(1, maximumEigenvalue / Math.Abs(determinant));
        if (!double.IsFinite(mappingCondition))
        {
            return Reject("IllConditionedCalibrationTransform", "毫米映射条件数无法有限表示。");
        }
        transformCondition = mappingCondition;
        if (mappingCondition > operation.MaximumConditionNumber)
        {
            return Reject("IllConditionedCalibrationTransform", "毫米映射线性部分超过条件数限，逆映射不稳定。");
        }

        var ia = dn / determinant / scale;
        var ib = -bn / determinant / scale;
        var ic = -cn / determinant / scale;
        var id = an / determinant / scale;
        var inverse = new AffineTransform2D(ia, ib, -(ia * tx + ib * ty), ic, id, -(ic * tx + id * ty));
        if (!FiniteCalibrationMatrix(inverse))
        {
            return Reject("UnrepresentableCalibration", "毫米逆映射无法有限表示。");
        }

        var errors = operation.Points.Select(pair =>
        {
            var predicted = forward.Transform(pair.Pixel.Position);
            var target = pair.World.PositionMillimeters;
            return double.Hypot(predicted.X - target.X, predicted.Y - target.Y);
        }).ToArray();
        if (errors.Any(error => !double.IsFinite(error)))
        {
            return Reject("UnrepresentableCalibration", "参考点重投影残差无法有限表示。");
        }
        residuals = errors;
        var maximum = residuals.Max();
        if (maximum > operation.MaximumResidualMillimeters)
        {
            return Reject("CalibrationResidualExceeded", "参考点最大毫米拟合残差超过配方上限，未发布可用标定。");
        }
        var rms = maximum == 0 ? 0 : Math.Min(maximum, maximum * Math.Sqrt(residuals.Average(error => Math.Pow(error / maximum, 2))));
        var calibration = new AffinePixelCalibration(Guid.NewGuid(), forward, inverse, designCondition.Value,
            transformCondition.Value, rms, maximum, operation.Points, residuals);
        return new(new AffineCalibrationOutput(calibration.ReferenceFrameId, calibration.PixelSpaceId, calibration.WorldSpaceId,
            calibration, null, designCondition, transformCondition, residuals));
    }

    private sealed record NormalizedCalibrationPoints(Point2D Anchor, double Scale, double MeanX, double MeanY, Point2D[] Centered);

    private static NormalizedCalibrationPoints? NormalizeCalibrationPoints(Point2D[] points)
    {
        var anchor = points[0];
        var deltas = points.Select(point => new Point2D(point.X - anchor.X, point.Y - anchor.Y)).ToArray();
        if (deltas.Any(point => !double.IsFinite(point.X) || !double.IsFinite(point.Y)))
        {
            return null;
        }
        var scale = deltas.Max(point => Math.Max(Math.Abs(point.X), Math.Abs(point.Y)));
        if (scale == 0)
        {
            return new(anchor, 0, 0, 0, new Point2D[points.Length]);
        }
        var meanX = deltas.Average(point => point.X / scale);
        var meanY = deltas.Average(point => point.Y / scale);
        return new(anchor, scale, meanX, meanY,
            deltas.Select(point => new Point2D(point.X / scale - meanX, point.Y / scale - meanY)).ToArray());
    }

    private static double? CalibrationDesignCondition(Point2D[] points)
    {
        var xx = points.Sum(point => point.X * point.X);
        var yy = points.Sum(point => point.Y * point.Y);
        var xy = points.Sum(point => point.X * point.Y);
        var maximum = (xx + yy + double.Hypot(xx - yy, 2 * xy)) * 0.5;
        var determinant = xx * yy - xy * xy;
        if (maximum <= 0 || determinant <= 0)
        {
            return null;
        }
        var minimum = determinant / maximum;
        var condition = Math.Sqrt(Math.Max(points.Length, maximum) / Math.Min(points.Length, minimum));
        return double.IsFinite(condition) ? Math.Max(1, condition) : null;
    }

    private static double[,]? SolveAffineQr(Point2D[] pixels, Point2D[] world)
    {
        var count = pixels.Length;
        var matrix = new double[count, 5];
        for (var row = 0; row < count; row++)
        {
            matrix[row, 0] = pixels[row].X;
            matrix[row, 1] = pixels[row].Y;
            matrix[row, 2] = 1;
            matrix[row, 3] = world[row].X;
            matrix[row, 4] = world[row].Y;
        }
        for (var column = 0; column < 3; column++)
        {
            var vector = new double[count - column];
            var norm = 0.0;
            for (var row = column; row < count; row++)
            {
                vector[row - column] = matrix[row, column];
                norm = double.Hypot(norm, matrix[row, column]);
            }
            if (norm == 0 || !double.IsFinite(norm))
            {
                return null;
            }
            vector[0] += Math.CopySign(norm, vector[0]);
            var vectorNorm = 0.0;
            foreach (var value in vector)
            {
                vectorNorm = double.Hypot(vectorNorm, value);
            }
            for (var i = 0; i < vector.Length; i++)
            {
                vector[i] /= vectorNorm;
            }
            for (var target = column; target < 5; target++)
            {
                var dot = 0.0;
                for (var row = column; row < count; row++)
                {
                    dot += vector[row - column] * matrix[row, target];
                }
                for (var row = column; row < count; row++)
                {
                    matrix[row, target] -= 2 * vector[row - column] * dot;
                }
            }
        }

        var solution = new double[3, 2];
        for (var target = 0; target < 2; target++)
        {
            for (var row = 2; row >= 0; row--)
            {
                var value = matrix[row, target + 3];
                for (var column = row + 1; column < 3; column++)
                {
                    value -= matrix[row, column] * solution[column, target];
                }
                solution[row, target] = value / matrix[row, row];
                if (!double.IsFinite(solution[row, target]))
                {
                    return null;
                }
            }
        }
        return solution;
    }

    private static bool FiniteCalibrationMatrix(AffineTransform2D matrix) => double.IsFinite(matrix.A) && double.IsFinite(matrix.B)
        && double.IsFinite(matrix.C) && double.IsFinite(matrix.D) && double.IsFinite(matrix.Tx) && double.IsFinite(matrix.Ty);

    private static BackendOutput MapPixelPoint(PixelToMillimeterPointOperation operation)
        => new(new CalibratedMillimeterPointOutput(operation.Source, operation.Calibration,
            new MillimeterPoint(operation.Calibration.WorldSpaceId, operation.Calibration.PixelToMillimeters.Transform(operation.Source.Position))));

    private static BackendOutput ReprojectWorldPoint(MillimeterPointToPixelOperation operation)
        => new(new ReprojectedPixelPointOutput(operation.Source, operation.Calibration,
            new PixelPoint(operation.TargetFrameId, operation.Calibration.PixelSpaceId,
                operation.Calibration.MillimetersToPixel.Transform(operation.Source.PositionMillimeters))));

    private static BackendOutput MeasureCalibratedPointDistance(CalibratedPointDistanceOperation operation)
    {
        var first = operation.First.Point.PositionMillimeters;
        var second = operation.Second.Point.PositionMillimeters;
        return new(new AffineMillimeterDistanceOutput(operation.First, operation.Second, double.Hypot(second.X - first.X, second.Y - first.Y)));
    }

    private static void ValidateAffineOutput(VisionOperation operation, BackendOutput output)
    {
        var valid = operation switch
        {
            AffineCalibrationOperation request => output.Value is AffineCalibrationOutput result
                && result.FrameId == request.Points[0].Pixel.FrameId && result.PixelSpaceId == request.Points[0].Pixel.CoordinateSpaceId
                && result.WorldSpaceId == request.Points[0].World.WorldSpaceId && output.NoMatch == (result.Calibration == null)
                && (result.ResidualsMillimeters.Count == 0 || result.ResidualsMillimeters.Count == request.Points.Count)
                && (result.Calibration is not { } calibration
                    || calibration.ReferencePairs.Count == request.Points.Count
                    && calibration.ReferencePairs.Select((pair, index) => ReferenceEquals(pair, request.Points[index])).All(value => value)
                    && calibration.DesignConditionNumber <= request.MaximumConditionNumber
                    && calibration.TransformConditionNumber <= request.MaximumConditionNumber
                    && calibration.MaximumResidualMillimeters <= request.MaximumResidualMillimeters),
            PixelToMillimeterPointOperation request => !output.NoMatch && output.Value is CalibratedMillimeterPointOutput result
                && ReferenceEquals(result.Source, request.Source) && ReferenceEquals(result.Calibration, request.Calibration),
            MillimeterPointToPixelOperation request => !output.NoMatch && output.Value is ReprojectedPixelPointOutput result
                && ReferenceEquals(result.Source, request.Source) && ReferenceEquals(result.Calibration, request.Calibration)
                && result.Point.FrameId == request.TargetFrameId,
            CalibratedPointDistanceOperation request => !output.NoMatch && output.Value is AffineMillimeterDistanceOutput result
                && ReferenceEquals(result.First, request.First) && ReferenceEquals(result.Second, request.Second),
            PixelLineToMillimetersOperation request => !output.NoMatch && output.Value is CalibratedMillimeterLineOutput result
                && ReferenceEquals(result.Source, request.Source) && ReferenceEquals(result.Calibration, request.Calibration),
            CalibratedPointLineDistanceOperation request => !output.NoMatch && output.Value is AffineMillimeterPointLineDistanceOutput result
                && ReferenceEquals(result.Point, request.Point) && ReferenceEquals(result.Line, request.Line),
            _ => false
        };
        if (!valid)
        {
            throw new InvalidOperationException("仿射标定/毫米几何结果类型、来源或诊断与本次请求不符。");
        }
    }
}
