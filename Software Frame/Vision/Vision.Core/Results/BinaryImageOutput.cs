#nullable enable
using Vision.Core.Imaging;

namespace Vision.Core.Results;

/// <summary>
/// 对应输入FrameId的独立二值图；新图有独立帧身份，不把全黑成功输出误当NoMatch。
/// </summary>
public sealed class BinaryImageOutput
{
    /// <summary>
    /// 产生本结果的输入帧身份；不是派生Image.FrameId。
    /// </summary>
    public Guid FrameId
    {
        get;
    }

    /// <summary>
    /// 独立像素快照，其FrameId必须不同于输入，尺寸/空间由实现按操作校验。
    /// </summary>
    public VisionFrame Image
    {
        get;
    }

    /// <summary>
    /// 创建二值图结果，校验派生图身份和有效像素值。
    /// </summary>
    public BinaryImageOutput(
        Guid frameId,
        VisionFrame image)
    {
        if (frameId == Guid.Empty)
        {
            throw new ArgumentException("必须注明输入帧。", nameof(frameId));
        }

        ArgumentNullException.ThrowIfNull(image);
        if (image.FrameId == frameId)
        {
            throw new ArgumentException("派生二值图必须有独立帧身份。", nameof(image));
        }

        if (image.Format != PixelFormat.Mono8)
        {
            throw new ArgumentException("二值输出必须为Mono8。", nameof(image));
        }

        for (var row = 0; row < image.Height; row++)
        {
            foreach (var pixel in image.Pixels.Slice(row * image.Stride, image.Width))
            {
                if (pixel is not (0 or 255))
                {
                    throw new ArgumentException("二值输出像素只能是0/255。", nameof(image));
                }
            }
        }

        FrameId = frameId;
        Image = image;
    }
}
