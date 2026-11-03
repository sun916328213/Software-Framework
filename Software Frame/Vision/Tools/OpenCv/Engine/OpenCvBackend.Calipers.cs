using OpenCvSharp;
using Vision.Core.Geometry;
using Vision.Core.Operations;
using Vision.Core.Results;
using Vision.Tools.OpenCv.Imaging;

namespace Vision.Tools.OpenCv.Engine;

internal sealed partial class OpenCvBackend
{
    private static CaliperLineOutput FindCaliperLine(CaliperLineOperation operation, CancellationToken cancellationToken)
    {
        using var source = OpenCvFrameAdapter.CopyGray(operation.Image);
        using var floating = new Mat();
        // 内部浮点插值保留原灰度数值，不归一化Mono16，也不创建伪装为原图的新帧。
        source.ConvertTo(floating, MatType.CV_32F);
        var picks = new List<EdgePick>(operation.CaliperCount);
        for (var index = 0; index < operation.CaliperCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fraction = index / (double)(operation.CaliperCount - 1);
            var center = new Point2D(operation.Start.X + fraction * (operation.End.X - operation.Start.X),
                operation.Start.Y + fraction * (operation.End.Y - operation.Start.Y));
            picks.Add(PickCaliper(floating, operation, index, center));
        }

        CaliperLineOutput Unfitted(CaliperLineFitState state)
        {
            return new(operation.Image.FrameId, operation.Image.CoordinateSpaceId, state, null,
                picks.Select(pick => new CaliperEdgeSample(pick.Index, pick.Center, pick.Edge, pick.Contrast, false, null)));
        }

        var found = picks.Where(pick => pick.Edge != null).ToList();
        if (found.Count < operation.MinimumPoints)
        {
            return Unfitted(CaliperLineFitState.InsufficientEdges);
        }

        var initial = FitCaliperPoints(found, operation.Tangent, DistanceTypes.Huber);
        if (initial == null)
        {
            return Unfitted(CaliperLineFitState.Degenerate);
        }

        var retained = found.Where(pick => initial.Distance(pick.Edge!.Value) <= operation.MaximumResidualPixels).ToList();
        while (retained.Count >= operation.MinimumPoints)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fit = FitCaliperPoints(retained, operation.Tangent, DistanceTypes.L2);
            if (fit == null)
            {
                return Unfitted(CaliperLineFitState.Degenerate);
            }

            var worst = retained.OrderByDescending(pick => fit.Distance(pick.Edge!.Value)).ThenBy(pick => pick.Index).First();
            var maximum = fit.Distance(worst.Edge!.Value);
            if (maximum > operation.MaximumResidualPixels)
            {
                // 每轮只删除一个最大残差点，随后重拟合；不把SDK返回点全部冒充有效参与点。
                retained.Remove(worst);
                continue;
            }

            var projections = retained.Select(pick => fit.Projection(pick.Edge!.Value)).ToArray();
            if (projections.Max() - projections.Min() <= 1e-6)
            {
                return Unfitted(CaliperLineFitState.Degenerate);
            }

            var rms = Math.Sqrt(retained.Average(pick => Math.Pow(fit.Distance(pick.Edge!.Value), 2)));
            var line = new CaliperLineFit(fit.At(projections.Min()), fit.At(projections.Max()), retained.Count,
                rms, Math.Max(maximum, rms));
            var indices = retained.Select(pick => pick.Index).ToHashSet();
            var samples = picks.Select(pick => new CaliperEdgeSample(pick.Index, pick.Center, pick.Edge,
                pick.Contrast, indices.Contains(pick.Index), pick.Edge is { } edge ? fit.Distance(edge) : null));
            return new(operation.Image.FrameId, operation.Image.CoordinateSpaceId, CaliperLineFitState.Fitted, line, samples);
        }

        return Unfitted(CaliperLineFitState.InsufficientInliers);
    }

    private static EdgePick PickCaliper(Mat image, CaliperLineOperation operation, int index, Point2D center)
    {
        var profile = new CaliperProfile(operation.Tangent, operation.ScanNormal, operation.TangentHalfWidth,
            operation.NormalHalfLength, operation.GaussianSigmaPixels, operation.MinimumContrast, operation.Polarity);
        var peaks = ReadCaliperPeaks(image, center, profile);
        if (peaks.Count == 0)
        {
            return new(index, center, null, 0);
        }

        var chosen = operation.Selection switch
        {
            CaliperEdgeSelection.First => peaks[0],
            CaliperEdgeSelection.Last => peaks[^1],
            CaliperEdgeSelection.Strongest => peaks.OrderByDescending(peak => Math.Abs(peak.Contrast)).ThenBy(peak => peak.Offset).First(),
            _ => throw new NotSupportedException("未知卡尺选边方式。")
        };
        return new(index, center, center.Offset(operation.ScanNormal.X * chosen.Offset,
            operation.ScanNormal.Y * chosen.Offset), chosen.Contrast);
    }

    /// <summary>唯一的卡尺采样/平滑/峰检测内核；找线保留原极性，配对请求取得所有合格极性峰。</summary>
    private static IReadOnlyList<CaliperPeak> ReadCaliperPeaks(Mat image, Point2D center, CaliperProfile sampling)
    {
        var tangent = sampling.Tangent;
        var normal = sampling.Normal;
        var halfWidth = sampling.HalfWidth;
        var halfLength = sampling.HalfLength;
        using var inverse = Mat.FromArray(new double[,]
        {
            { tangent.X, normal.X, center.X - tangent.X * halfWidth - normal.X * halfLength },
            { tangent.Y, normal.Y, center.Y - tangent.Y * halfWidth - normal.Y * halfLength }
        });
        using var patch = new Mat();
        Cv2.WarpAffine(image, patch, inverse, new Size(2 * halfWidth + 1, 2 * halfLength + 1),
            InterpolationFlags.Linear | InterpolationFlags.WarpInverseMap, BorderTypes.Constant, Scalar.All(0));
        var radius = (int)Math.Ceiling(3 * sampling.Sigma);
        if (radius > 0)
        {
            Cv2.GaussianBlur(patch, patch, new Size(1, 2 * radius + 1),
                sampling.Sigma, sampling.Sigma, BorderTypes.Replicate);
        }

        var profile = new double[patch.Rows];
        for (var row = 0; row < patch.Rows; row++)
        {
            for (var column = 0; column < patch.Cols; column++)
            {
                profile[row] += patch.At<float>(row, column);
            }
            profile[row] /= patch.Cols;
        }

        var differences = new double[profile.Length - 1];
        var strengths = new double[differences.Length];
        for (var i = radius; i < differences.Length - radius; i++)
        {
            var difference = profile[i + 1] - profile[i];
            differences[i] = difference;
            if (sampling.Polarity == CaliperEdgePolarity.Any
                || sampling.Polarity == CaliperEdgePolarity.DarkToLight && difference > 0
                || sampling.Polarity == CaliperEdgePolarity.LightToDark && difference < 0)
            {
                strengths[i] = Math.Abs(difference);
            }
        }

        var peaks = new List<CaliperPeak>();
        for (var i = radius; i < differences.Length - radius; i++)
        {
            var previous = i == radius || sampling.SeparateOppositePeaks && differences[i - 1] * differences[i] < 0
                ? 0 : strengths[i - 1];
            var next = i == differences.Length - radius - 1 || sampling.SeparateOppositePeaks && differences[i + 1] * differences[i] < 0
                ? 0 : strengths[i + 1];
            // 找线保留原平顶规则；测宽中相邻异号峰是两条边，不能因为绝对强度相等就合成一个平顶。
            if (strengths[i] >= sampling.MinimumContrast && strengths[i] > previous && strengths[i] >= next)
            {
                peaks.Add(new(i + 0.5 - halfLength, differences[i]));
            }
        }
        return peaks;
    }

    private sealed record CaliperProfile(Point2D Tangent, Point2D Normal, int HalfWidth, int HalfLength,
        double Sigma, double MinimumContrast, CaliperEdgePolarity Polarity, bool SeparateOppositePeaks = false);
    private sealed record CaliperPeak(double Offset, double Contrast);

    private static FittedLine? FitCaliperPoints(IReadOnlyList<EdgePick> picks, Point2D tangent, DistanceTypes distance)
    {
        var anchor = picks[0].Edge!.Value;
        if (picks.All(pick => pick.Edge!.Value.DistanceTo(anchor) <= 1e-6))
        {
            return null;
        }

        // 先减局部原点再交给原生float拟合，避免大图平移坐标无谓损失小数位。
        var points = picks.Select(pick => new Point2f((float)(pick.Edge!.Value.X - anchor.X),
            (float)(pick.Edge!.Value.Y - anchor.Y))).ToArray();
        var native = Cv2.FitLine(points, distance, 0, 0.01, 0.01);
        var length = Math.Sqrt(native.Vx * native.Vx + native.Vy * native.Vy);
        if (!double.IsFinite(length) || length <= 0 || !double.IsFinite(native.X1) || !double.IsFinite(native.Y1))
        {
            return null;
        }

        var sign = native.Vx * tangent.X + native.Vy * tangent.Y < 0 ? -1 : 1;
        return new(new Point2D(anchor.X + native.X1, anchor.Y + native.Y1),
            new Point2D(sign * native.Vx / length, sign * native.Vy / length));
    }

    private sealed record EdgePick(int Index, Point2D Center, Point2D? Edge, double Contrast);

    private sealed record FittedLine(Point2D Origin, Point2D Direction)
    {
        internal double Distance(Point2D point) => Math.Abs((point.X - Origin.X) * Direction.Y - (point.Y - Origin.Y) * Direction.X);
        internal double Projection(Point2D point) => (point.X - Origin.X) * Direction.X + (point.Y - Origin.Y) * Direction.Y;
        internal Point2D At(double offset) => new(Origin.X + offset * Direction.X, Origin.Y + offset * Direction.Y);
    }
}
