using System.Windows.Media;
using System.Windows.Media.Imaging;
using Vision.Core.Imaging;
using Vision.Core.Results;
using Vision.Tools.OpenCv.Processing;
using ImageFormat = Vision.Core.Imaging.PixelFormat;

#nullable enable
namespace Vision.Controls.Presentation;

/// <summary>只转换公共像素缓冲；不访问厂商显示窗口和原生句柄。</summary>
public static class VisionDisplay
{
    public static BitmapSource ToBitmap(VisionImage image)
    {
        var format = image.Format switch
        {
            ImageFormat.Mono8 => PixelFormats.Gray8,
            ImageFormat.Mono16 => PixelFormats.Gray16,
            ImageFormat.Rgb24 => PixelFormats.Rgb24,
            ImageFormat.Bgr24 => PixelFormats.Bgr24,
            ImageFormat.Bgra32 => PixelFormats.Bgra32,
            _ => throw new NotSupportedException($"暂不支持显示 {image.Format}；请使用整数灰度或彩色输入。")
        };
        var bitmap = BitmapSource.Create(image.Width, image.Height, 96, 96, format, null,
            image.Pixels, image.Stride > 0 ? image.Stride : image.ExpectedStride);
        bitmap.Freeze();
        return bitmap;
    }

    public static VisionImage? RoiImage(ToolResult? result) =>
        result is { Success: true } and IRoiImageResult { RoiImage: { IsEmpty: false } image } ? image : null;

    public static string RoiImageInfo(ToolResult? result) => RoiImage(result) != null && result is IRoiImageResult stage
        ? $"算法中间 ROI 图：{stage.RoiImageDescription}"
        : "当前没有有效的算法中间 ROI 图；暂显示输入图像与 ROI 标注，非处理结果。";

    public static VisionImage? OutputImage(ToolResult? result) =>
        result is { Success: true } and IOutputImageResult { OutputImage: { IsEmpty: false } image } ? image : null;

    public static IReadOnlyList<VisionResultItem> ResultItems(ToolResult? result) => result?.Success == true ? result switch
    {
        Vision.Tools.OpenCv.Inspection.BlobResult blob => blob.Items.Select(i => new VisionResultItem(i.Index, i.Area, i.Center, i.Bounds, i.Contours, featureValues: i.FeatureValues)).ToArray(),
        Vision.Tools.Halcon.Inspection.BlobResult blob => blob.Items.Select(i => new VisionResultItem(i.Index, i.Area, i.Center, i.Bounds, i.Contours, featureValues: i.FeatureValues)).ToArray(),
        Vision.Tools.OpenCv.Matching.TemplateMatchResult match => match.Matches.Select((i, n) =>
            new VisionResultItem(n + 1, null, i.Center, i.Bounds,
                i.Corners.Count == 0 ? [] : [new Vision.Core.Geometry.RegionContour(i.Corners)],
                VisionResultGeometryKind.TemplateRectangle, i.Score, i.AngleDeg, i.Scale)).ToArray(),
        Vision.Tools.Halcon.Matching.ShapeMatchResult match => match.Matches.Select((i, n) =>
            new VisionResultItem(n + 1, null, i.Center, default, [], VisionResultGeometryKind.MatchCenter, i.Score, i.AngleDeg, i.Scale)).ToArray(),
        Vision.Core.AI.AIDetectionResult detection => detection.Boxes.Select((i, n) =>
            new VisionResultItem(n + 1, null, i.Box.Center, i.Box, [], VisionResultGeometryKind.DetectionRectangle,
                i.Score, label: i.Label, classId: i.ClassId)).ToArray(),
        _ => Array.Empty<VisionResultItem>()
    } : Array.Empty<VisionResultItem>();

    public static string Details(ToolResult? result)
    {
        if (result == null) return "尚未执行。修改参数或输入后请重新执行；结果坐标以当前输入图像左上角为原点。";
        if (!result.Success) return $"执行失败\n{result.Message}\n{result.Validation?.ToMessage()}";
        var lines = result switch
        {
            Vision.Tools.OpenCv.Inspection.GrayStatisticsResult statistics =>
                new[] { $"原始灰度：{statistics.InputFormat}；范围[{statistics.Minimum},{statistics.Maximum}]；中位数{statistics.Median:0.###}。总体标准差分母为有效像素数N。",
                    $"直方图共{statistics.Histogram.Length}档，档宽{statistics.HistogramBinWidth}；下方仅列前995个非零档（灰度范围:像素数）；完整计数保存在本次结果Histogram中。" }
                    .Concat(statistics.Histogram.Select((count, bin) => (count, bin)).Where(b => b.count > 0).Take(995).Select(b =>
                        statistics.HistogramBinWidth == 1 ? $"{b.bin}: {b.count}" :
                        $"{(long)b.bin * statistics.HistogramBinWidth}~{Math.Min((long)(b.bin + 1) * statistics.HistogramBinWidth - 1, statistics.InputFormat == ImageFormat.Mono16 ? 65535 : 255)}: {b.count}")),
            CropResult crop => new[] { $"裁剪输出 {crop.OutputImage.Width}×{crop.OutputImage.Height}；输入坐标范围 {crop.SourceBounds}；局部坐标加偏移 {crop.InputOffset} 还原输入坐标。" },
            Vision.Tools.OpenCv.Inspection.BlobResult blob => blob.Items.Select(i => $"#{i.Index}  面积 {i.Area:0.###}  中心 {i.Center}  外接框 {i.Bounds}"),
            Vision.Tools.OpenCv.Matching.TemplateMatchResult match => match.Matches.Select((i, n) => $"#{n + 1}  分数 {i.Score:0.####}  中心 {i.Center}  角度 {i.AngleDeg:0.###}°  缩放 {i.Scale:0.####}  外接框 {i.Bounds}"),
            Vision.Tools.Halcon.Inspection.BlobResult blob => blob.Items.Select(i => $"#{i.Index}  面积 {i.Area:0.###}  中心 {i.Center}"),
            Vision.Tools.Halcon.Matching.ShapeMatchResult match => match.Matches.Select((i, n) => $"#{n + 1}  分数 {i.Score:0.####}  中心 {i.Center}"),
            Vision.Tools.Halcon.Identification.BarcodeResult codes => codes.Codes.Select((i, n) => $"#{n + 1}  {i.Text}  码制字段 {i.Symbology}（后端尚未提供真实位置）"),
            Vision.Tools.Halcon.Identification.DataCodeResult codes => codes.Codes.Select((i, n) => $"#{n + 1}  {i.Text}  码制字段 {i.Symbology}（后端尚未提供真实位置）"),
            Vision.Tools.Halcon.Calibration.NPointsCalibrationResult calibration => new[] { $"变换矩阵：{calibration.Matrix}；世界坐标单位由标定点配置决定。" },
            Vision.Core.AI.AIDetectionResult detection => detection.Boxes.Select((i, n) => $"#{n + 1}  {i.Label}  分数 {i.Score:0.####}  外接框 {i.Box}"),
            Vision.Core.AI.AIClassificationResult classification => classification.Scores.Select(i => $"{i.Label}  分数 {i.Score:0.####}"),
            Vision.Core.AI.AISegmentationResult segmentation => segmentation.Items.Select((i, n) => $"#{n + 1}  {i.Label}  分数 {i.Score:0.####}  外接框 {i.Bounds}"),
            _ => Enumerable.Empty<string>()
        };
        return ToolResultText.Summarize(result) + "\n" + string.Join("\n", lines.Take(1000));
    }
}
