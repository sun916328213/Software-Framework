using OpenCvSharp;
using Vision.Core.Imaging;
using Vision.Core.Operations;
using Vision.Core.Results;
using Vision.Tools.OpenCv.Imaging;

namespace Vision.Tools.OpenCv.Engine;

internal sealed partial class OpenCvBackend
{
    /// <summary>只统计本次参与域的像素，标准差使用总体定义；不读取行尾填充。</summary>
    private static GrayStatisticsOutput Statistics(GrayStatisticsOperation operation)
    {
        var frame = operation.Image;
        using var source = OpenCvFrameAdapter.CopyGray(frame);
        using var mask = operation.Region == null ? null : OpenCvFrameAdapter.RegionMask(frame, operation.Region);
        double minimum;
        double maximum;
        Scalar mean;
        Scalar deviation;
        if (mask == null)
        {
            Cv2.MinMaxLoc(source, out minimum, out maximum);
            Cv2.MeanStdDev(source, out mean, out deviation);
        }
        else
        {
            Cv2.MinMaxLoc(source, out minimum, out maximum, out _, out _, mask);
            Cv2.MeanStdDev(source, out mean, out deviation, mask);
        }

        var count = mask == null ? (long)frame.Width * frame.Height : Cv2.CountNonZero(mask);
        return new(frame.FrameId, frame.CoordinateSpaceId, frame.Format, count, minimum, maximum, mean.Val0, deviation.Val0);
    }

    /// <summary>闭区间阈值；参与域外始终为0，不将域外灰度0误算为命中。</summary>
    private static BinaryImageOutput Threshold(ThresholdRangeOperation operation)
    {
        var frame = operation.Image;
        using var source = OpenCvFrameAdapter.CopyGray(frame);
        using var binary = new Mat();
        Cv2.InRange(source, new Scalar(operation.Minimum), new Scalar(operation.Maximum), binary);
        if (operation.Region != null)
        {
            using var mask = OpenCvFrameAdapter.RegionMask(frame, operation.Region);
            Cv2.BitwiseAnd(binary, mask, binary);
        }

        return new(frame.FrameId, CopyOutput(binary, frame.Width, frame.Height, PixelFormat.Mono8, frame.CoordinateSpaceId));
    }

    /// <summary>实际调用 OpenCV 裁剪并复制，不借用源 Mat 的局部视图。</summary>
    private static ImageCropOutput Crop(ImageCropOperation operation)
    {
        var frame = operation.Image;
        using var source = OpenCvFrameAdapter.Copy(frame);
        using var view = new Mat(source, new Rect(operation.X, operation.Y, operation.Width, operation.Height));
        using var cropped = view.Clone();
        var image = OpenCvFrameAdapter.Snapshot(cropped, frame.Format, Guid.NewGuid());
        return new(frame.FrameId, frame.CoordinateSpaceId, image, operation.X, operation.Y);
    }

    private static VisionFrame CopyOutput(Mat image, int width, int height, PixelFormat format, Guid space)
    {
        var type = format == PixelFormat.Mono16 ? MatType.CV_16UC1 : MatType.CV_8UC1;
        if (image.Type() != type || image.Width != width || image.Height != height || !image.IsContinuous())
        {
            throw new InvalidDataException("OpenCV返回的图像格式、尺寸或连续存储与请求不符。");
        }

        return OpenCvFrameAdapter.Snapshot(image, format, space);
    }
}
