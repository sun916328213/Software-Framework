using OpenCvSharp;
using Vision.Core.Geometry;
using Vision.Core.Operations;
using Vision.Core.Results;
using Vision.Tools.OpenCv.Imaging;

namespace Vision.Tools.OpenCv.Engine;

internal sealed partial class OpenCvBackend
{
    private static CaliperEdgePairOutput MeasureEdgePairs(CaliperEdgePairOperation operation, CancellationToken cancellationToken)
    {
        using var source = OpenCvFrameAdapter.CopyGray(operation.Image);
        using var floating = new Mat();
        source.ConvertTo(floating, MatType.CV_32F);
        var sampling = new CaliperProfile(operation.Tangent, operation.ScanNormal, operation.TangentHalfWidth,
            operation.NormalHalfLength, operation.GaussianSigmaPixels, operation.MinimumContrast, CaliperEdgePolarity.Any, true);
        var samples = new List<CaliperEdgePairSample>(operation.CaliperCount);
        for (var index = 0; index < operation.CaliperCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fraction = index / (double)(operation.CaliperCount - 1);
            var center = new Point2D(operation.Start.X + fraction * (operation.End.X - operation.Start.X),
                operation.Start.Y + fraction * (operation.End.Y - operation.Start.Y));
            var peaks = ReadCaliperPeaks(floating, center, sampling);
            var pairs = new List<EdgePair>();
            var sign = operation.Polarity == CaliperPairPolarity.LightStripe ? 1 : -1;
            for (var i = 0; i + 1 < peaks.Count; i++)
            {
                var first = peaks[i];
                var second = peaks[i + 1];
                var width = second.Offset - first.Offset;
                // 只对同一剖面中的相邻合格峰配对，不跨卡尺，不跳过其他合格峰去拼接宽度。
                if (first.Contrast * sign > 0 && second.Contrast * sign < 0
                    && width >= operation.MinimumWidthPixels && width <= operation.MaximumWidthPixels)
                {
                    pairs.Add(new(first, second, width));
                }
            }

            if (pairs.Count == 0)
            {
                samples.Add(new(index, center, peaks.Count, 0, null, null, 0, 0, null));
                continue;
            }

            var chosen = operation.Selection switch
            {
                CaliperEdgeSelection.First => pairs[0],
                CaliperEdgeSelection.Last => pairs[^1],
                // 一对的强度取较弱边的绝对灰度差，避免一边很强就掩盖另一边不足。
                CaliperEdgeSelection.Strongest => pairs.OrderByDescending(pair =>
                    Math.Min(Math.Abs(pair.First.Contrast), Math.Abs(pair.Second.Contrast)))
                    .ThenBy(pair => pair.First.Offset).First(),
                _ => throw new NotSupportedException("未知边缘对选择方式。")
            };
            var firstPoint = center.Offset(operation.ScanNormal.X * chosen.First.Offset, operation.ScanNormal.Y * chosen.First.Offset);
            var secondPoint = center.Offset(operation.ScanNormal.X * chosen.Second.Offset, operation.ScanNormal.Y * chosen.Second.Offset);
            samples.Add(new(index, center, peaks.Count, pairs.Count, firstPoint, secondPoint,
                chosen.First.Contrast, chosen.Second.Contrast, chosen.Width));
        }

        var widths = samples.Where(sample => sample.WidthPixels != null).Select(sample => sample.WidthPixels!.Value).ToArray();
        CaliperWidthStatistics? statistics = null;
        if (widths.Length >= operation.MinimumPairs)
        {
            var mean = widths.Average();
            var deviation = Math.Sqrt(widths.Average(width => Math.Pow(width - mean, 2)));
            var measurement = new ScalarMeasurementOutput(operation.Image.FrameId, operation.Image.CoordinateSpaceId,
                GeometryMeasurementKind.CaliperPairMeanWidth, mean);
            statistics = new(widths.Length, measurement, widths.Min(), widths.Max(), deviation);
        }

        return new(operation.Image.FrameId, operation.Image.CoordinateSpaceId, statistics, samples);
    }

    private sealed record EdgePair(CaliperPeak First, CaliperPeak Second, double Width);
}
