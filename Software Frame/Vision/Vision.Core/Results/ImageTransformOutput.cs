#nullable enable
using Vision.Core.Imaging;

namespace Vision.Core.Results;

/// <summary>
/// 不改变像素位置的图像变换结果。FrameId表示直接输入，Image是独立输出帧；实现核验原尺寸/空间。
/// </summary>
public sealed class ImageTransformOutput
{
    public Guid FrameId
    {
        get;
    }
    public VisionFrame Image
    {
        get;
    }

    public ImageTransformOutput(Guid frameId, VisionFrame image)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (frameId == Guid.Empty || frameId == image.FrameId)
        {
            throw new ArgumentException("图像变换必须注明直接输入，并生成独立输出帧。", nameof(frameId));
        }

        FrameId = frameId;
        Image = image;
    }
}
