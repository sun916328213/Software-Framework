using OpenCvSharp;
using Vision.Core.Imaging;
using Vision.Core.Operations;
using Vision.Core.Results;
using Vision.Tools.OpenCv.Imaging;

namespace Vision.Tools.OpenCv.Engine;

internal sealed partial class OpenCvBackend
{
    private static ImageTransformOutput Filter(ImageFilterOperation operation)
    {
        var frame = operation.Image;
        using var source = OpenCvFrameAdapter.CopyGray(frame);
        using var filtered = new Mat();
        var size = new Size(operation.KernelSize, operation.KernelSize);
        switch (operation.Kind)
        {
            case ImageFilterKind.Mean:
                Cv2.Blur(source, filtered, size, borderType: Border(operation.Border));
                break;
            case ImageFilterKind.Gaussian:
                var sigma = operation.GaussianSigma!.Value;
                Cv2.GaussianBlur(source, filtered, size, sigma, sigma, Border(operation.Border));
                break;
            case ImageFilterKind.Median:
                // OpenCV中值滤波固定使用复制边界，其他选择已在Validate中拒绝。
                Cv2.MedianBlur(source, filtered, operation.KernelSize);
                break;
            default:
                throw new NotSupportedException("未接通的滤波方式。");
        }

        return new ImageTransformOutput(frame.FrameId,
            OpenCvFrameAdapter.Snapshot(filtered, frame.Format, frame.CoordinateSpaceId));
    }

    private static OtsuThresholdOutput Otsu(OtsuThresholdOperation operation)
    {
        var frame = operation.Image;
        using var source = OpenCvFrameAdapter.CopyGray(frame);
        using var nativeBinary = new Mat();
        var mode = operation.Inverse ? ThresholdTypes.BinaryInv : ThresholdTypes.Binary;
        var threshold = Cv2.Threshold(source, nativeBinary, 0, 255, mode | ThresholdTypes.Otsu);
        using var binary = new Mat();
        // uint16输入仍按原灰度直方图选阈值；只把已经0/255的输出无损转为Mono8。
        nativeBinary.ConvertTo(binary, MatType.CV_8UC1);
        return new OtsuThresholdOutput(frame.FrameId,
            OpenCvFrameAdapter.Snapshot(binary, PixelFormat.Mono8, frame.CoordinateSpaceId), threshold);
    }

    private static BinaryImageOutput Adaptive(AdaptiveThresholdOperation operation)
    {
        var frame = operation.Image;
        using var source = OpenCvFrameAdapter.CopyGray(frame);
        using var binary = new Mat();
        var method = operation.Method == AdaptiveThresholdMethod.Mean
            ? AdaptiveThresholdTypes.MeanC : AdaptiveThresholdTypes.GaussianC;
        var mode = operation.Inverse ? ThresholdTypes.BinaryInv : ThresholdTypes.Binary;
        // SDK以复制边界计算局部邻域；这里不先填0，也不拿全图均值代替局部统计。
        Cv2.AdaptiveThreshold(source, binary, 255, method, mode, operation.BlockSize, operation.Offset);
        return new BinaryImageOutput(frame.FrameId,
            OpenCvFrameAdapter.Snapshot(binary, PixelFormat.Mono8, frame.CoordinateSpaceId));
    }

    private static BinaryImageOutput Morphology(MorphologyOperation operation)
    {
        var frame = operation.Image;
        using var source = OpenCvFrameAdapter.CopyGray(frame);
        var shape = operation.KernelShape switch
        {
            MorphologyKernelShape.Rectangle => MorphShapes.Rect,
            MorphologyKernelShape.Ellipse => MorphShapes.Ellipse,
            MorphologyKernelShape.Cross => MorphShapes.Cross,
            _ => throw new NotSupportedException("未接通的结构元素形状。")
        };
        var kind = operation.Kind switch
        {
            MorphologyKind.Erode => MorphTypes.Erode,
            MorphologyKind.Dilate => MorphTypes.Dilate,
            MorphologyKind.Open => MorphTypes.Open,
            MorphologyKind.Close => MorphTypes.Close,
            _ => throw new NotSupportedException("未接通的形态学方式。")
        };
        using var kernel = Cv2.GetStructuringElement(shape, new Size(operation.KernelWidth, operation.KernelHeight));
        using var binary = new Mat();
        // 显式补零，不用SDK的形态学特殊默认边界值（腐蚀时可能代表正无穷）。
        Cv2.MorphologyEx(source, binary, kind, kernel, iterations: operation.Iterations,
            borderType: Border(operation.Border), borderValue: Scalar.All(0));
        return new BinaryImageOutput(frame.FrameId,
            OpenCvFrameAdapter.Snapshot(binary, PixelFormat.Mono8, frame.CoordinateSpaceId));
    }

    private static BorderTypes Border(ImageBorderMode mode) => mode switch
    {
        ImageBorderMode.ConstantZero => BorderTypes.Constant,
        ImageBorderMode.Replicate => BorderTypes.Replicate,
        ImageBorderMode.Reflect101 => BorderTypes.Reflect101,
        _ => throw new NotSupportedException("未知边界模式。")
    };
}
