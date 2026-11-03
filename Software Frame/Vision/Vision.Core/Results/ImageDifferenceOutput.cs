#nullable enable
using Vision.Core.Imaging;
using Vision.Core.Operations;

namespace Vision.Core.Results;

/// <summary>显式双图来源、方向/原位深差异阈值与独立二值候选图；全黑为有效成功，不代表整件合格。</summary>
public sealed class ImageDifferenceOutput
{
    public Guid FrameId
    {
        get;
    }
    public Guid ReferenceFrameId
    {
        get;
    }
    public ImageDifferenceDirection Direction
    {
        get;
    }
    public ushort MinimumDifference
    {
        get;
    }
    public ushort MaximumDifference
    {
        get;
    }
    public VisionFrame Image
    {
        get;
    }

    public ImageDifferenceOutput(Guid frameId, Guid referenceFrameId, ImageDifferenceDirection direction,
        ushort minimumDifference, ushort maximumDifference, VisionFrame image)
    {
        var binary = new BinaryImageOutput(frameId, image);
        if (referenceFrameId == Guid.Empty || referenceFrameId == image.FrameId || !Enum.IsDefined(direction)
            || minimumDifference == 0 || minimumDifference > maximumDifference)
        {
            throw new ArgumentException("差分结果必须有明确双图来源、方向及正有序阈值，输出帧不能冒充任一来源。");
        }
        FrameId = binary.FrameId;
        ReferenceFrameId = referenceFrameId;
        Direction = direction;
        MinimumDifference = minimumDifference;
        MaximumDifference = maximumDifference;
        Image = binary.Image;
    }
}
