using OpenCvSharp;
using Vision.Core.Imaging;
using Vision.Core.Operations;
using Vision.Core.Results;
using Vision.Tools.OpenCv.Imaging;

namespace Vision.Tools.OpenCv.Engine;

internal sealed partial class OpenCvBackend
{
    private const long MaximumDifferenceImagePixels = 20_000_000;

    /// <summary>显式读取两张输入，不缓存背景。原位深差异先计算，再按正闭区间筛为二值候选。</summary>
    private static ImageDifferenceOutput Difference(AlignedImageDifferenceOperation operation)
    {
        var frame = operation.Current;
        using var current = OpenCvFrameAdapter.CopyGray(frame);
        using var reference = OpenCvFrameAdapter.CopyGray(operation.Reference);
        using var difference = new Mat();
        switch (operation.Direction)
        {
            case ImageDifferenceDirection.Absolute:
                Cv2.Absdiff(current, reference, difference);
                break;
            case ImageDifferenceDirection.CurrentBrighter:
                // uint8/uint16饱和减法把反向差设0；阈值下限>0，不将其误算为正向缺陷。
                Cv2.Subtract(current, reference, difference);
                break;
            case ImageDifferenceDirection.CurrentDarker:
                Cv2.Subtract(reference, current, difference);
                break;
            default:
                throw new NotSupportedException("未定义的灰度差异方向。");
        }
        using var binary = new Mat();
        Cv2.InRange(difference, new Scalar(operation.MinimumDifference), new Scalar(operation.MaximumDifference), binary);
        if (operation.Region != null)
        {
            using var mask = OpenCvFrameAdapter.RegionMask(frame, operation.Region);
            Cv2.BitwiseAnd(binary, mask, binary);
        }
        return new(frame.FrameId, operation.Reference.FrameId, operation.Direction, operation.MinimumDifference, operation.MaximumDifference,
            CopyOutput(binary, frame.Width, frame.Height, PixelFormat.Mono8, frame.CoordinateSpaceId));
    }
}
