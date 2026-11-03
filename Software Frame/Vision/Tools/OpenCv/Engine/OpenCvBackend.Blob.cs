using System.Runtime.InteropServices;
using OpenCvSharp;
using Vision.Core.Geometry;
using Vision.Core.Operations;
using Vision.Core.Results;
using Vision.Tools.OpenCv.Imaging;

namespace Vision.Tools.OpenCv.Engine;

internal sealed partial class OpenCvBackend
{
    private const long MaximumBlobImagePixels = 20_000_000;
    private const int MaximumRawComponents = 10_000;
    private const long MaximumComponentWindowPixels = 100_000_000;

    private static BlobOutput FindBlobs(BlobOperation operation, CancellationToken cancellationToken)
    {
        var frame = operation.Image;
        using var source = OpenCvFrameAdapter.CopyGray(frame);
        using var binary = new Mat();
        Cv2.InRange(source, new Scalar(operation.Minimum), new Scalar(operation.Maximum), binary);
        using var domain = operation.Region == null ? null : OpenCvFrameAdapter.RegionMask(frame, operation.Region);
        if (domain != null)
        {
            Cv2.BitwiseAnd(binary, domain, binary);
        }

        if (operation.HolePolicy == BlobHolePolicy.Fill)
        {
            FillHoles(binary, operation.Connectivity);
            if (domain != null)
            {
                Cv2.BitwiseAnd(binary, domain, binary);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        using var labels = new Mat();
        using var stats = new Mat();
        using var centers = new Mat();
        // SAUF按行优先编号，为几何/面积均并列时提供确定的最终排序键。
        var count = Cv2.ConnectedComponentsWithStatsWithAlgorithm(binary, labels, stats, centers,
            (PixelConnectivity)(int)operation.Connectivity, MatType.CV_32S, ConnectedComponentsAlgorithmsTypes.SAUF);
        if (count - 1 > MaximumRawComponents)
        {
            throw new InvalidOperationException("原始连通域超过10000个，请收紧阈值或参与域；不发布截断计数。");
        }

        var labelPixels = new int[checked(frame.Width * frame.Height)];
        Marshal.Copy(labels.Data, labelPixels, 0, labelPixels.Length);
        var selected = new List<BlobCandidate>();
        long windowPixels = 0;
        for (var label = 1; label < count; label++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var x = stats.At<int>(label, (int)ConnectedComponentsTypes.Left);
            var y = stats.At<int>(label, (int)ConnectedComponentsTypes.Top);
            var width = stats.At<int>(label, (int)ConnectedComponentsTypes.Width);
            var height = stats.At<int>(label, (int)ConnectedComponentsTypes.Height);
            var area = stats.At<int>(label, (int)ConnectedComponentsTypes.Area);
            windowPixels += (long)width * height;
            if (windowPixels > MaximumComponentWindowPixels)
            {
                throw new InvalidOperationException("组件轮廓窗口累计超过1亿像素，不发布不完整结果。");
            }

            var (contours, metrics) = ComponentContours(labelPixels, frame.Width, label, x, y, width, height, cancellationToken);
            var item = new BlobItem(1, area,
                new Point2D(centers.At<double>(label, 0), centers.At<double>(label, 1)),
                new Rect2D(x, y, width, height), contours, metrics);
            if (!PassesFilters(operation, item))
            {
                continue;
            }

            selected.Add(new(label, item));
            if (selected.Count > operation.MaximumResults)
            {
                throw new InvalidOperationException("筛选后区域数量超过本次安全上限，不以TopN冒充完整计数。");
            }
        }

        IOrderedEnumerable<BlobCandidate> sorted = operation.Sort switch
        {
            BlobSort.AreaDescending => selected.OrderByDescending(candidate => candidate.Item.Area),
            BlobSort.AreaAscending => selected.OrderBy(candidate => candidate.Item.Area),
            BlobSort.XAscending => selected.OrderBy(candidate => candidate.Item.Center.X),
            BlobSort.YAscending => selected.OrderBy(candidate => candidate.Item.Center.Y),
            _ => throw new NotSupportedException("未知区域排序方式。")
        };
        var items = sorted.ThenBy(candidate => candidate.Item.Center.Y)
            .ThenBy(candidate => candidate.Item.Center.X).ThenBy(candidate => candidate.Label)
            .Select((candidate, index) => new BlobItem(index + 1, candidate.Item.Area,
                candidate.Item.Center, candidate.Item.Bounds, candidate.Item.Contours, candidate.Item.ContourMetrics));
        return new BlobOutput(frame.FrameId, frame.CoordinateSpaceId, items);
    }

    private static bool PassesFilters(BlobOperation operation, BlobItem item)
    {
        if (operation.Filters.Count == 0)
        {
            return true;
        }

        bool Matches(BlobFeatureRange filter)
        {
            double? value = filter.Feature switch
            {
                BlobFeature.Area => item.Area,
                BlobFeature.Width => item.Bounds.Width,
                BlobFeature.Height => item.Bounds.Height,
                BlobFeature.Extent => item.Extent,
                BlobFeature.HoleCount => item.HoleCount,
                BlobFeature.ContourArea => item.ContourMetrics.Area,
                BlobFeature.ContourPerimeter => item.ContourMetrics.Perimeter,
                BlobFeature.ContourCircularity => item.ContourMetrics.Circularity,
                _ => throw new NotSupportedException("未知区域特征。")
            };
            // 无定义的圆度不等于零，即使范围包含0也不能命中。
            return value is { } actual && actual >= filter.Minimum && actual <= filter.Maximum;
        }

        return operation.FilterLogic == BlobFilterLogic.All
            ? operation.Filters.All(Matches) : operation.Filters.Any(Matches);
    }

    private static (IReadOnlyList<RegionContour> Contours, BlobContourMetrics Metrics) ComponentContours(int[] labels, int stride,
        int label, int x, int y, int width, int height, CancellationToken cancellationToken)
    {
        var pixels = new byte[checked(width * height)];
        for (var row = 0; row < height; row++)
        {
            for (var column = 0; column < width; column++)
            {
                if (labels[(row + y) * stride + column + x] == label)
                {
                    pixels[row * width + column] = 255;
                }
            }
        }

        using var component = new Mat(height, width, MatType.CV_8UC1);
        Marshal.Copy(pixels, 0, component.Data, pixels.Length);
        Cv2.FindContours(component, out Point[][] contours, out HierarchyIndex[] hierarchy,
            RetrievalModes.Tree, ContourApproximationModes.ApproxSimple, new Point(x, y));
        var result = new List<RegionContour>(contours.Length);
        var netArea = 0d;
        var perimeter = 0d;
        for (var i = 0; i < contours.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var depth = 0;
            for (var parent = hierarchy[i].Parent; parent >= 0; parent = hierarchy[parent].Parent)
            {
                depth++;
            }
            var isHole = depth % 2 != 0;
            var area = Math.Abs(Cv2.ContourArea(contours[i]));
            netArea += isHole ? -area : area;
            perimeter += Cv2.ArcLength(contours[i], true);
            result.Add(new RegionContour(contours[i].Select(point => new Point2D(point.X, point.Y)).ToArray(), isHole));
        }
        // 仅在同一轮廓几何口径内计算圆度，禁止把前景像素计数混进公式再强行截成1。
        double? circularity = netArea > 0 && perimeter > 0 ? 4 * Math.PI * netArea / (perimeter * perimeter) : null;
        return (result, new BlobContourMetrics(netArea, perimeter, circularity));
    }

    private static void FillHoles(Mat binary, BlobConnectivity foreground)
    {
        using var padded = new Mat();
        Cv2.CopyMakeBorder(binary, padded, 1, 1, 1, 1, BorderTypes.Constant, Scalar.All(0));
        // 使用互补连通性，扩展的背景圈确保原图角点为前景时仍能从真正的外部开始填充。
        var background = foreground == BlobConnectivity.Eight ? 4 : 8;
        Cv2.FloodFill(padded, new Point(0, 0), Scalar.All(255), out _,
            flags: (FloodFillFlags)background);
        Cv2.BitwiseNot(padded, padded);
        using var holes = new Mat(padded, new Rect(1, 1, binary.Width, binary.Height));
        Cv2.BitwiseOr(binary, holes, binary);
    }

    private sealed record BlobCandidate(int Label, BlobItem Item);
}
