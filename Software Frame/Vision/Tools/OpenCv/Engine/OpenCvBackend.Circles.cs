using OpenCvSharp;
using Vision.Core.Geometry;
using Vision.Core.Operations;
using Vision.Core.Results;
using Vision.Tools.OpenCv.Imaging;

namespace Vision.Tools.OpenCv.Engine;

internal sealed partial class OpenCvBackend
{
    private static CaliperCircleOutput FindCaliperCircle(CaliperCircleOperation operation, CancellationToken token)
    {
        using var source = OpenCvFrameAdapter.CopyGray(operation.Image);
        using var floating = new Mat();
        source.ConvertTo(floating, MatType.CV_32F);
        var picks = new List<EdgePick>(operation.CaliperCount);
        for (var index = 0; index < operation.CaliperCount; index++)
        {
            token.ThrowIfCancellationRequested();
            var placement = operation.Placement(index);
            var sampling = new CaliperProfile(placement.Tangent, placement.Normal, operation.TangentHalfWidth,
                operation.NormalHalfLength, operation.GaussianSigmaPixels, operation.MinimumContrast, operation.Polarity);
            var peaks = ReadCaliperPeaks(floating, placement.Center, sampling);
            if (peaks.Count == 0)
            {
                picks.Add(new(index, placement.Center, null, 0));
                continue;
            }

            var chosen = operation.Selection switch
            {
                CaliperEdgeSelection.First => peaks[0],
                CaliperEdgeSelection.Last => peaks[^1],
                CaliperEdgeSelection.Strongest => peaks.OrderByDescending(peak => Math.Abs(peak.Contrast)).ThenBy(peak => peak.Offset).First(),
                _ => throw new NotSupportedException("未知圆卡尺选边方式。")
            };
            picks.Add(new(index, placement.Center, placement.Center.Offset(placement.Normal.X * chosen.Offset,
                placement.Normal.Y * chosen.Offset), chosen.Contrast));
        }

        CaliperCircleOutput Unfitted(CaliperCircleFitState state)
        {
            return new(operation.Image.FrameId, operation.Image.CoordinateSpaceId, state, null,
                picks.Select(pick => new CaliperEdgeSample(pick.Index, pick.Center, pick.Edge, pick.Contrast, false, null)));
        }

        var found = picks.Where(pick => pick.Edge != null).ToList();
        if (found.Count < operation.MinimumPoints)
        {
            return Unfitted(CaliperCircleFitState.InsufficientEdges);
        }

        // 最多64组按样本次序分散的确定性三点种子；是有限共识初始化，不宣称穷举/全局RANSAC保证。
        var trials = Math.Min(found.Count, 64);
        var retained = new List<EdgePick>();
        var bestError = double.PositiveInfinity;
        var hasSeed = false;
        for (var trial = 0; trial < trials; trial++)
        {
            token.ThrowIfCancellationRequested();
            var i = trial * found.Count / trials;
            var candidate = CircleThroughThree(found[i].Edge!.Value,
                found[(i + found.Count / 3) % found.Count].Edge!.Value,
                found[(i + 2 * found.Count / 3) % found.Count].Edge!.Value);
            if (candidate == null)
            {
                continue;
            }

            hasSeed = true;
            var inliers = found.Where(pick => candidate.Residual(pick.Edge!.Value) <= operation.MaximumRadialResidualPixels).ToList();
            var error = inliers.Sum(pick => Math.Pow(candidate.Residual(pick.Edge!.Value), 2));
            if (inliers.Count > retained.Count || inliers.Count == retained.Count && error < bestError)
            {
                retained = inliers;
                bestError = error;
            }
        }

        if (!hasSeed)
        {
            return Unfitted(CaliperCircleFitState.Degenerate);
        }

        var readmissions = 0;
        while (retained.Count >= operation.MinimumPoints)
        {
            token.ThrowIfCancellationRequested();
            var circle = RefineCircle(retained, token);
            if (circle == null)
            {
                return Unfitted(CaliperCircleFitState.Degenerate);
            }

            // 初始三点种子可能偏移；精化后满足同一残差限的观测应可重新参与，避免留下低残差的假剔除点。
            var currentIndices = retained.Select(pick => pick.Index).ToHashSet();
            var recovered = found.Where(pick => !currentIndices.Contains(pick.Index)
                && circle.Residual(pick.Edge!.Value) <= operation.MaximumRadialResidualPixels).ToArray();
            if (recovered.Length > 0 && readmissions < 5)
            {
                retained.AddRange(recovered);
                retained = retained.OrderBy(pick => pick.Index).ToList();
                readmissions++;
                continue;
            }

            var worst = retained.OrderByDescending(pick => circle.Residual(pick.Edge!.Value)).ThenBy(pick => pick.Index).First();
            var maximum = circle.Residual(worst.Edge!.Value);
            if (maximum > operation.MaximumRadialResidualPixels)
            {
                retained.Remove(worst);
                continue;
            }

            var coverage = CircleCoverage(retained, circle.Center);
            if (coverage < operation.MinimumCoverageDegrees)
            {
                return Unfitted(CaliperCircleFitState.InsufficientCoverage);
            }

            var rms = Math.Sqrt(retained.Average(pick => Math.Pow(circle.Residual(pick.Edge!.Value), 2)));
            var diameter = new ScalarMeasurementOutput(operation.Image.FrameId, operation.Image.CoordinateSpaceId,
                GeometryMeasurementKind.CaliperCircleDiameter, 2 * circle.Radius);
            var fit = new CaliperCircleFit(circle.Center, circle.Radius, retained.Count, rms, Math.Max(maximum, rms), coverage, diameter);
            var used = retained.Select(pick => pick.Index).ToHashSet();
            var samples = picks.Select(pick => new CaliperEdgeSample(pick.Index, pick.Center, pick.Edge, pick.Contrast,
                used.Contains(pick.Index), pick.Edge is { } edge ? circle.Residual(edge) : null));
            return new(operation.Image.FrameId, operation.Image.CoordinateSpaceId, CaliperCircleFitState.Fitted, fit, samples);
        }

        return Unfitted(CaliperCircleFitState.InsufficientInliers);
    }

    private static CircleCandidate? CircleThroughThree(Point2D first, Point2D second, Point2D third)
    {
        var ux = second.X - first.X;
        var uy = second.Y - first.Y;
        var vx = third.X - first.X;
        var vy = third.Y - first.Y;
        var scale = Math.Max(double.Hypot(ux, uy), double.Hypot(vx, vy));
        if (!double.IsFinite(scale) || scale <= 1e-9)
        {
            return null;
        }

        ux /= scale;
        uy /= scale;
        vx /= scale;
        vy /= scale;
        var determinant = 2 * (ux * vy - uy * vx);
        if (Math.Abs(determinant) <= 1e-8)
        {
            return null;
        }

        var u2 = ux * ux + uy * uy;
        var v2 = vx * vx + vy * vy;
        var dx = (u2 * vy - v2 * uy) / determinant * scale;
        var dy = (ux * v2 - vx * u2) / determinant * scale;
        var center = first.Offset(dx, dy);
        var radius = double.Hypot(dx, dy);
        return double.IsFinite(center.X) && double.IsFinite(center.Y) && double.IsFinite(radius) && radius > 0
            ? new(center, radius) : null;
    }

    private static CircleCandidate? RefineCircle(IReadOnlyList<EdgePick> picks, CancellationToken token)
    {
        var origin = new Point2D(picks.Average(pick => pick.Edge!.Value.X), picks.Average(pick => pick.Edge!.Value.Y));
        var scale = picks.Max(pick => pick.Edge!.Value.DistanceTo(origin));
        if (!double.IsFinite(scale) || scale <= 1e-9)
        {
            return null;
        }

        var points = picks.Select(pick => new Point2D((pick.Edge!.Value.X - origin.X) / scale,
            (pick.Edge!.Value.Y - origin.Y) / scale)).ToArray();
        var xx = points.Sum(point => point.X * point.X);
        var yy = points.Sum(point => point.Y * point.Y);
        var xy = points.Sum(point => point.X * point.Y);
        if ((xx * yy - xy * xy) / ((xx + yy) * (xx + yy)) <= 1e-8)
        {
            return null;
        }

        using var matrix = new Mat(points.Length, 3, MatType.CV_64FC1);
        using var target = new Mat(points.Length, 1, MatType.CV_64FC1);
        using var solution = new Mat();
        for (var i = 0; i < points.Length; i++)
        {
            matrix.Set(i, 0, 2 * points[i].X);
            matrix.Set(i, 1, 2 * points[i].Y);
            matrix.Set(i, 2, 1d);
            target.Set(i, 0, points[i].X * points[i].X + points[i].Y * points[i].Y);
        }
        if (!Cv2.Solve(matrix, target, solution, DecompTypes.SVD))
        {
            return null;
        }

        var cx = solution.At<double>(0);
        var cy = solution.At<double>(1);
        var squaredRadius = solution.At<double>(2) + cx * cx + cy * cy;
        if (!double.IsFinite(squaredRadius) || squaredRadius <= 0)
        {
            return null;
        }

        var radius = Math.Sqrt(squaredRadius);
        // 归一化代数初值后，最多15轮径向几何最小二乘；不以外接圆/最小包围圆冒充测量拟合。
        for (var iteration = 0; iteration < 15; iteration++)
        {
            token.ThrowIfCancellationRequested();
            for (var i = 0; i < points.Length; i++)
            {
                var dx = cx - points[i].X;
                var dy = cy - points[i].Y;
                var length = double.Hypot(dx, dy);
                if (!double.IsFinite(length) || length <= 1e-12)
                {
                    return null;
                }
                matrix.Set(i, 0, dx / length);
                matrix.Set(i, 1, dy / length);
                matrix.Set(i, 2, -1d);
                target.Set(i, 0, radius - length);
            }
            if (!Cv2.Solve(matrix, target, solution, DecompTypes.SVD))
            {
                return null;
            }

            var dcx = solution.At<double>(0);
            var dcy = solution.At<double>(1);
            var dr = solution.At<double>(2);
            cx += dcx;
            cy += dcy;
            radius += dr;
            if (!double.IsFinite(cx) || !double.IsFinite(cy) || !double.IsFinite(radius) || radius <= 0)
            {
                return null;
            }
            if (double.Hypot(dcx, dcy) + Math.Abs(dr) <= 1e-7)
            {
                break;
            }
        }

        var center = origin.Offset(cx * scale, cy * scale);
        return double.IsFinite(center.X) && double.IsFinite(center.Y) && double.IsFinite(radius * scale)
            ? new(center, radius * scale) : null;
    }

    private static double CircleCoverage(IReadOnlyList<EdgePick> picks, Point2D center)
    {
        var angles = picks.Select(pick => Math.Atan2(pick.Edge!.Value.Y - center.Y, pick.Edge.Value.X - center.X)).OrderBy(angle => angle).ToArray();
        var largestGap = angles[0] + 2 * Math.PI - angles[^1];
        for (var i = 1; i < angles.Length; i++)
        {
            largestGap = Math.Max(largestGap, angles[i] - angles[i - 1]);
        }
        return (2 * Math.PI - largestGap) * (180 / Math.PI);
    }

    private sealed record CircleCandidate(Point2D Center, double Radius)
    {
        internal double Residual(Point2D point) => Math.Abs(double.Hypot(point.X - Center.X, point.Y - Center.Y) - Radius);
    }
}
