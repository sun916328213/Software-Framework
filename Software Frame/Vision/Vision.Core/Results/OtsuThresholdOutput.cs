#nullable enable
using Vision.Core.Imaging;

namespace Vision.Core.Results;

/// <summary>本次全图Otsu选出的原位深阈值及独立二值结果，不是上次或显示用阈值。</summary>
public sealed class OtsuThresholdOutput
{
    public Guid FrameId
    {
        get;
    }
    public VisionFrame Image
    {
        get;
    }
    public double Threshold
    {
        get;
    }

    public OtsuThresholdOutput(Guid frameId, VisionFrame image, double threshold)
    {
        var binary = new BinaryImageOutput(frameId, image);
        if (!double.IsFinite(threshold) || threshold < 0 || threshold > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(threshold));
        }

        FrameId = binary.FrameId;
        Image = binary.Image;
        Threshold = threshold;
    }
}
