using OpenCvSharp;
using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Models;
using Vision.Core.Operations;
using Vision.Core.Results;

namespace Vision.Tools.OpenCv.Matching;

/// <summary>
/// 有界的离散角度/等比缩放灰度相关搜索。
/// 只借用本次调用的 Mat，不持有会话，不做隐式连续精化或厂商回退。
/// </summary>
internal static class TemplateMatchSearch
{
    internal const int MaximumPoseSamples = 1024;
    internal const int MaximumMatches = 100;
    private const long MaximumResponsePixels = 100_000_000;

    internal static double PoseSampleCount(TemplateMatchOperation operation)
    {
        var range = operation.SearchRange;
        return (Math.Ceiling(range.AngleExtentDegrees / operation.Sampling.AngleStepDegrees) + 1)
            * (Math.Ceiling((range.ScaleMaximum - range.ScaleMinimum) / operation.Sampling.ScaleStep) + 1);
    }

    internal static IReadOnlyList<TemplateMatchCandidate> Search(
        Mat source,
        Mat template,
        TemplateMatchOperation operation,
        CancellationToken cancellationToken)
    {
        if (operation.Sampling.UsesNativeSearch || PoseSampleCount(operation) > MaximumPoseSamples
            || operation.MaximumMatches > MaximumMatches)
        {
            throw new NotSupportedException("仅支持有界离散采样，最多1024姿态和100个目标。");
        }

        var definition = (TemplateModelDefinition)operation.Model!.Definition;
        var variants = PlanVariants(source, template, operation);
        if (variants.Count == 0)
        {
            return [];
        }

        using var coverage = definition.Mask == null
            ? new Mat(template.Size(), MatType.CV_8UC1, Scalar.All(255))
            : MaskMat(definition.Mask);
        using var searchMask = operation.SearchMask == null ? null : MaskMat(operation.SearchMask);
        var candidates = new List<Candidate>();
        var validVariants = 0;
        var peakLimit = Math.Min(operation.MaximumMatches * 4, 256);
        double? omittedUpperBound = null;

        foreach (var variant in variants)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var direction = new Pose2D(0, 0, variant.Angle).Transform(new Point2D(variant.Scale, 0));
            var cos = direction.X;
            var sin = direction.Y;
            using var matrix = Mat.FromArray(new double[,]
            {
                { cos, -sin, (variant.Width - 1) / 2d - cos * (template.Width - 1) / 2d + sin * (template.Height - 1) / 2d },
                { sin, cos, (variant.Height - 1) / 2d - sin * (template.Width - 1) / 2d - cos * (template.Height - 1) / 2d }
            });
            using var warped = new Mat();
            using var mask = new Mat();
            var size = new Size(variant.Width, variant.Height);
            Cv2.WarpAffine(template, warped, matrix, size, InterpolationFlags.Linear, BorderTypes.Constant, Scalar.All(0));
            Cv2.WarpAffine(coverage, mask, matrix, size, InterpolationFlags.Linear, BorderTypes.Constant, Scalar.All(0));
            // 旋转补边和部分覆盖像素不能被当作模板纹理。
            Cv2.Threshold(mask, mask, 254, 255, ThresholdTypes.Binary);
            if (Cv2.CountNonZero(mask) < 4)
            {
                continue;
            }

            Cv2.MeanStdDev(warped, out Scalar _, out Scalar deviation, mask);
            if (deviation.Val0 <= 1e-6)
            {
                continue;
            }

            validVariants++;
            using var response = new Mat();
            Cv2.MatchTemplate(source, warped, response, TemplateMatchModes.CCoeffNormed, mask);
            using var valid = new Mat();
            using var invalid = new Mat();
            // 掩膜相关的零分母可能产生 NaN/Inf，不能截成高分后发布。
            Cv2.InRange(response, Scalar.All(-1.0001), Scalar.All(1.0001), valid);
            if (searchMask != null)
            {
                RestrictAnchors(valid, searchMask, definition, variant);
            }

            Cv2.BitwiseNot(valid, invalid);
            response.SetTo(Scalar.All(float.MinValue), invalid);
            for (var i = 0; i <= peakLimit; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Cv2.MinMaxLoc(response, out _, out var score, out _, out var location);
                if (!double.IsFinite(score) || score < operation.MinimumScore)
                {
                    break;
                }

                if (i == peakLimit)
                {
                    omittedUpperBound = Math.Max(omittedUpperBound ?? double.NegativeInfinity, score);
                    break;
                }

                candidates.Add(CreateCandidate(location, variant, definition, Math.Clamp(score, 0, 1)));
                var x = Math.Max(0, location.X - Math.Max(1, variant.Width / 4));
                var y = Math.Max(0, location.Y - Math.Max(1, variant.Height / 4));
                var right = Math.Min(response.Width, location.X + Math.Max(1, variant.Width / 4) + 1);
                var bottom = Math.Min(response.Height, location.Y + Math.Max(1, variant.Height / 4) + 1);
                using var suppressed = new Mat(response, new Rect(x, y, right - x, bottom - y));
                suppressed.SetTo(Scalar.All(float.MinValue));
            }
        }

        if (validVariants == 0)
        {
            throw new InvalidOperationException("变换后的模板过小或有效纹理不足。");
        }

        var selected = new List<Candidate>();
        foreach (var candidate in candidates.OrderByDescending(item => item.Value.Score)
            .ThenBy(item => item.Value.AngleDegrees).ThenBy(item => item.Value.Scale))
        {
            if (selected.Any(previous => previous.Center.DistanceTo(candidate.Center) < 1e-6
                || Overlap(previous, candidate) > operation.MaximumOverlap))
            {
                continue;
            }

            selected.Add(candidate);
            if (selected.Count == operation.MaximumMatches)
            {
                break;
            }
        }

        if (omittedUpperBound is { } omitted
            && (selected.Count < operation.MaximumMatches || omitted >= selected[^1].Value.Score))
        {
            throw new InvalidOperationException("姿态候选超过安全预算，请提高最低得分或缩小搜索域；本次不发布不完整结果。");
        }

        return selected.Select(item => item.Value).ToArray();
    }

    private static List<Variant> PlanVariants(Mat source, Mat template, TemplateMatchOperation operation)
    {
        var variants = new List<Variant>();
        long responsePixels = 0;
        var range = operation.SearchRange;
        foreach (var angle in Samples(range.AngleStartDegrees, range.AngleExtentDegrees, operation.Sampling.AngleStepDegrees)
            .Select(Pose2D.NormalizeAngle).Distinct())
        {
            foreach (var scale in Samples(range.ScaleMinimum, range.ScaleMaximum - range.ScaleMinimum, operation.Sampling.ScaleStep))
            {
                var direction = new Pose2D(0, 0, angle).Transform(new Point2D(1, 0));
                var width = Math.Ceiling(scale * (Math.Abs(direction.X) * template.Width + Math.Abs(direction.Y) * template.Height));
                var height = Math.Ceiling(scale * (Math.Abs(direction.Y) * template.Width + Math.Abs(direction.X) * template.Height));
                if (width < 2 || height < 2 || width > source.Width || height > source.Height)
                {
                    continue;
                }

                responsePixels += ((long)source.Width - (int)width + 1) * (source.Height - (int)height + 1);
                if (responsePixels > MaximumResponsePixels)
                {
                    throw new InvalidOperationException("姿态搜索响应像素预算超过1亿，请缩小图像/搜索范围或增大步长。");
                }

                variants.Add(new(angle, scale, (int)width, (int)height));
            }
        }

        return variants;
    }

    private static IEnumerable<double> Samples(double start, double extent, double step)
    {
        var count = (int)(Math.Ceiling(extent / step) + 1);
        for (var i = 0; i < count; i++)
        {
            yield return start + Math.Min(i * step, extent);
        }
    }

    internal static Mat MaskMat(VisionMask mask)
    {
        var result = new Mat(mask.Height, mask.Width, MatType.CV_8UC1);
        try
        {
            System.Runtime.InteropServices.Marshal.Copy(mask.Pixels.ToArray(), 0, result.Data, mask.Width * mask.Height);
            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    private static Candidate CreateCandidate(Point location, Variant variant, TemplateModelDefinition definition, double score)
    {
        // NMS 使用像素边界中心；公开锚点以像素中心索引0为原点，二者相差半像素。
        var center = new Point2D(location.X + variant.Width / 2d, location.Y + variant.Height / 2d);
        var offset = new Pose2D(0, 0, variant.Angle).Transform(new Point2D(
            (definition.Origin.X - (definition.Template.Width - 1) / 2d) * variant.Scale,
            (definition.Origin.Y - (definition.Template.Height - 1) / 2d) * variant.Scale));
        var value = new TemplateMatchCandidate(new Point2D(center.X - 0.5 + offset.X, center.Y - 0.5 + offset.Y),
            definition.Origin, variant.Angle, variant.Scale, score);
        var box = new RotatedRect(new Point2f((float)center.X, (float)center.Y),
            new Size2f((float)(definition.Template.Width * variant.Scale), (float)(definition.Template.Height * variant.Scale)),
            (float)variant.Angle);
        return new(value, center, box);
    }

    private static double Overlap(Candidate first, Candidate second)
    {
        Cv2.RotatedRectangleIntersection(first.Box, second.Box, out Point2f[] points);
        var intersection = points.Length < 3 ? 0 : Math.Abs(Cv2.ContourArea(points));
        var union = (double)first.Box.Size.Width * first.Box.Size.Height
            + (double)second.Box.Size.Width * second.Box.Size.Height - intersection;
        return union <= 0 ? 0 : Math.Clamp(intersection / union, 0, 1);
    }

    private static void RestrictAnchors(Mat valid, Mat searchMask, TemplateModelDefinition definition, Variant variant)
    {
        var offset = new Pose2D(0, 0, variant.Angle).Transform(new Point2D(
            (definition.Origin.X - (definition.Template.Width - 1) / 2d) * variant.Scale,
            (definition.Origin.Y - (definition.Template.Height - 1) / 2d) * variant.Scale));
        var shiftX = Math.Floor(variant.Width / 2d + offset.X);
        var shiftY = Math.Floor(variant.Height / 2d + offset.Y);
        using var allowed = new Mat(valid.Size(), MatType.CV_8UC1, Scalar.All(0));
        if (double.IsFinite(shiftX) && double.IsFinite(shiftY)
            && Math.Abs(shiftX) < int.MaxValue && Math.Abs(shiftY) < int.MaxValue)
        {
            var dx = (int)shiftX;
            var dy = (int)shiftY;
            var left = Math.Max(0, -dx);
            var top = Math.Max(0, -dy);
            var right = Math.Min((long)valid.Width, (long)searchMask.Width - dx);
            var bottom = Math.Min((long)valid.Height, (long)searchMask.Height - dy);
            if (right > left && bottom > top)
            {
                var region = new Rect(left, top, (int)(right - left), (int)(bottom - top));
                using var source = new Mat(searchMask, new Rect(left + dx, top + dy, region.Width, region.Height));
                using var target = new Mat(allowed, region);
                source.CopyTo(target);
            }
        }

        Cv2.BitwiseAnd(valid, allowed, valid);
    }

    private sealed record Variant(double Angle, double Scale, int Width, int Height);
    private sealed record Candidate(TemplateMatchCandidate Value, Point2D Center, RotatedRect Box);
}
