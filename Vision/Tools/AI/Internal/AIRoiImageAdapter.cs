using Vision.Core.AI;
using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Results;
using Vision.Core.Roi;

namespace Vision.Tools.AI.Internal;

/// <summary>在 Provider 边界裁剪矩形 ROI，并将位置结果还原为原图坐标。</summary>
internal static class AIRoiImageAdapter
{
    internal readonly record struct PreparedImage(VisionImage Image, int Left, int Top);

    internal static PreparedImage Crop(VisionImage image, IRoi roi)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(roi);
        if (roi.Kind != RoiKind.Rectangle || !double.IsFinite(roi.Pose.AngleDeg) ||
            Math.Abs(roi.Pose.AngleDeg) > 1e-9)
            throw new NotSupportedException($"AI ROI 当前仅支持轴对齐矩形，实际为 {roi.Kind}。");
        if (roi.IsEmpty)
            throw new ArgumentOutOfRangeException(nameof(roi), "AI ROI 必须为非空矩形。");
        if (image.IsEmpty || image.BytesPerPixel <= 0)
            throw new ArgumentException("输入图像为空或像素格式未知。", nameof(image));

        var bounds = roi.Bounds;
        var left = PixelEdge(bounds.X);
        var top = PixelEdge(bounds.Y);
        var width = PixelEdge(bounds.Width);
        var height = PixelEdge(bounds.Height);
        if (left < 0 || top < 0 || width <= 0 || height <= 0 ||
            (long)left + width > image.Width || (long)top + height > image.Height)
            throw new ArgumentOutOfRangeException(nameof(roi), "AI ROI 必须为落在图像内的非空矩形。");

        var bytesPerPixel = image.BytesPerPixel;
        var sourceRowBytes = checked(image.Width * bytesPerPixel);
        var sourceStride = image.Stride > 0 ? image.Stride : sourceRowBytes;
        if (sourceStride < sourceRowBytes || image.Pixels.LongLength < (long)sourceStride * image.Height)
            throw new ArgumentException("输入图像的步长或像素缓冲与尺寸不一致。", nameof(image));

        var croppedStride = checked(width * bytesPerPixel);
        var pixels = new byte[checked(croppedStride * height)];
        for (var row = 0; row < height; row++)
        {
            var sourceOffset = checked((top + row) * sourceStride + left * bytesPerPixel);
            Buffer.BlockCopy(image.Pixels, sourceOffset, pixels, row * croppedStride, croppedStride);
        }

        var cropped = VisionImage.FromBuffer(width, height, image.Format, pixels, source: image.Source);
        cropped.FrameId = image.FrameId;
        cropped.Timestamp = image.Timestamp;
        return new PreparedImage(cropped, left, top);
    }

    internal static void RestoreCoordinates(ToolResult result, PreparedImage prepared)
    {
        switch (result)
        {
            case AIDetectionResult detection:
                foreach (var box in detection.Boxes)
                    box.Box = Offset(box.Box, prepared.Left, prepared.Top);
                break;

            case AISegmentationResult segmentation:
                if (segmentation.Items.Any(item => item.Mask != null))
                    throw new NotSupportedException("带位图掩膜的分割结果尚不能映射回原图，请使用整图推理。");
                foreach (var item in segmentation.Items)
                {
                    item.Bounds = Offset(item.Bounds, prepared.Left, prepared.Top);
                    for (var i = 0; i < item.Polygon.Count; i++)
                        item.Polygon[i] = item.Polygon[i].Offset(prepared.Left, prepared.Top);
                }
                break;

            case AIAnomalyResult anomaly:
                anomaly.AnomalyBounds = Offset(anomaly.AnomalyBounds, prepared.Left, prepared.Top);
                break;
        }
    }

    private static int PixelEdge(double value)
    {
        if (!double.IsFinite(value) || value != Math.Truncate(value) || value < int.MinValue || value > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(value), "AI ROI 边界必须是整数像素坐标。");
        return (int)value;
    }

    private static Rect2D Offset(Rect2D rectangle, int left, int top) =>
        rectangle.IsEmpty
            ? rectangle
            : new Rect2D(rectangle.X + left, rectangle.Y + top, rectangle.Width, rectangle.Height);
}
