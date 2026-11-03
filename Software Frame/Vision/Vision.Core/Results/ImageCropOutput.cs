#nullable enable
using Vision.Core.Coordinate;
using Vision.Core.Imaging;

namespace Vision.Core.Results;

/// <summary>
/// 本次矩形裁剪结果及其到直接输入图像的映射。
/// 不推断祖先图像、世界坐标或其他处理分支；多次裁剪需显式组合各级映射。
/// </summary>
public sealed class ImageCropOutput
{
    /// <summary>本次直接输入的源帧身份，不是裁剪图的身份。</summary>
    public Guid FrameId
    {
        get;
    }

    /// <summary>映射目标的源图像坐标空间。</summary>
    public Guid SourceCoordinateSpaceId
    {
        get;
    }

    /// <summary>具有独立帧和坐标空间的裁剪图，像素坐标从0开始。</summary>
    public VisionFrame Image
    {
        get;
    }

    /// <summary>裁剪原点在源图中的列坐标，单位为像素。</summary>
    public int SourceX
    {
        get;
    }

    /// <summary>裁剪原点在源图中的行坐标，单位为像素。</summary>
    public int SourceY
    {
        get;
    }

    /// <summary>裁剪图像素中心到直接源图像素中心的平移映射；返回值不共享可变变换对象。</summary>
    public AffineTransform2D ImageToSource => new(1, 0, SourceX, 0, 1, SourceY);

    public ImageCropOutput(
        Guid frameId,
        Guid sourceCoordinateSpaceId,
        VisionFrame image,
        int sourceX,
        int sourceY)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (frameId == Guid.Empty || sourceCoordinateSpaceId == Guid.Empty
            || image.FrameId == frameId || image.CoordinateSpaceId == sourceCoordinateSpaceId)
        {
            throw new ArgumentException("裁剪必须注明来源，并使用独立的帧和坐标空间身份。");
        }

        if (sourceX < 0 || sourceY < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceX));
        }

        FrameId = frameId;
        SourceCoordinateSpaceId = sourceCoordinateSpaceId;
        Image = image;
        SourceX = sourceX;
        SourceY = sourceY;
    }
}
