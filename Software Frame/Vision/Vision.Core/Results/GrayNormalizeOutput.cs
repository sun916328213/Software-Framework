#nullable enable
using Vision.Core.Imaging;

namespace Vision.Core.Results;

/// <summary>全图归一化结果及直接输入的原始编码极值；Image为独立同位深帧。</summary>
public sealed class GrayNormalizeOutput
{
    public Guid FrameId { get; }
    public VisionFrame Image { get; }
    public int InputMinimum { get; }
    public int InputMaximum { get; }

    public GrayNormalizeOutput(Guid frameId, VisionFrame image, int inputMinimum, int inputMaximum)
    {
        var transform = new ImageTransformOutput(frameId, image);
        var maximum = image.Format switch
        {
            PixelFormat.Mono8 => byte.MaxValue,
            PixelFormat.Mono16 => ushort.MaxValue,
            _ => throw new ArgumentException("归一化输出只支持整数灰度。", nameof(image))
        };
        if (inputMinimum < 0 || inputMaximum < inputMinimum || inputMaximum > maximum)
        {
            throw new ArgumentOutOfRangeException(nameof(inputMinimum), "输入极值必须属于输出位深且最小值不大于最大值。");
        }

        FrameId = transform.FrameId;
        Image = transform.Image;
        InputMinimum = inputMinimum;
        InputMaximum = inputMaximum;
    }
}
