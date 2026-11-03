using OpenCvSharp;
using Vision.Core.Imaging;
using Vision.Core.Operations;
using Vision.Core.Results;
using Vision.Tools.OpenCv.Imaging;

namespace Vision.Tools.OpenCv.Engine;

internal sealed partial class OpenCvBackend
{
    private const long MaximumColorImagePixels = 20_000_000;

    /// <summary>按明确RGB含义重排上下限而不隐式转灰，三个通道同时满足，处理域外清零。</summary>
    private static BinaryImageOutput SegmentRgb(RgbColorSegmentOperation operation)
    {
        var frame = operation.Image;
        var range = operation.Range;
        using var source = OpenCvFrameAdapter.Copy(frame);
        using var binary = new Mat();
        var rgb = frame.Format == PixelFormat.Rgb24;
        var minimum = rgb ? new Scalar(range.MinimumRed, range.MinimumGreen, range.MinimumBlue)
            : new Scalar(range.MinimumBlue, range.MinimumGreen, range.MinimumRed);
        var maximum = rgb ? new Scalar(range.MaximumRed, range.MaximumGreen, range.MaximumBlue)
            : new Scalar(range.MaximumBlue, range.MaximumGreen, range.MaximumRed);
        Cv2.InRange(source, minimum, maximum, binary);
        if (operation.Region != null)
        {
            using var mask = OpenCvFrameAdapter.RegionMask(frame, operation.Region);
            Cv2.BitwiseAnd(binary, mask, binary);
        }
        return new(frame.FrameId, CopyOutput(binary, frame.Width, frame.Height, PixelFormat.Mono8, frame.CoordinateSpaceId));
    }
}
