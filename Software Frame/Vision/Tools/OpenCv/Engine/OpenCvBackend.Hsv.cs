using OpenCvSharp;
using Vision.Core.Imaging;
using Vision.Core.Operations;
using Vision.Core.Results;
using Vision.Tools.OpenCv.Imaging;

namespace Vision.Tools.OpenCv.Engine;

internal sealed partial class OpenCvBackend
{
    private static BinaryImageOutput SegmentHsv(HsvColorSegmentOperation operation)
    {
        var frame = operation.Image;
        var range = operation.Range;
        using var source = OpenCvFrameAdapter.Copy(frame);
        using var hsv = new Mat();
        Cv2.CvtColor(source, hsv, frame.Format == PixelFormat.Rgb24 ? ColorConversionCodes.RGB2HSV : ColorConversionCodes.BGR2HSV);
        using var binary = new Mat();
        // S8=0没有有效色相，即使原生H8填0也不把灰/白误计为红色；无色相只走显式分支。
        var minimumSaturation = Math.Max(1, (int)range.MinimumSaturation);
        if (range.HueWraps)
        {
            Cv2.InRange(hsv, new Scalar(range.MinimumHue, minimumSaturation, range.MinimumValue),
                new Scalar(179, range.MaximumSaturation, range.MaximumValue), binary);
            using var lowHue = new Mat();
            Cv2.InRange(hsv, new Scalar(0, minimumSaturation, range.MinimumValue),
                new Scalar(range.MaximumHue, range.MaximumSaturation, range.MaximumValue), lowHue);
            Cv2.BitwiseOr(binary, lowHue, binary);
        }
        else
        {
            Cv2.InRange(hsv, new Scalar(range.MinimumHue, minimumSaturation, range.MinimumValue),
                new Scalar(range.MaximumHue, range.MaximumSaturation, range.MaximumValue), binary);
        }
        if (range.IncludeAchromatic && range.MinimumSaturation == 0)
        {
            using var achromatic = new Mat();
            Cv2.InRange(hsv, new Scalar(0, 0, range.MinimumValue), new Scalar(179, 0, range.MaximumValue), achromatic);
            Cv2.BitwiseOr(binary, achromatic, binary);
        }
        if (operation.Region != null)
        {
            using var mask = OpenCvFrameAdapter.RegionMask(frame, operation.Region);
            Cv2.BitwiseAnd(binary, mask, binary);
        }
        return new(frame.FrameId, CopyOutput(binary, frame.Width, frame.Height, PixelFormat.Mono8, frame.CoordinateSpaceId));
    }
}
