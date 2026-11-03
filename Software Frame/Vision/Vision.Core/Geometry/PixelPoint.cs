#nullable enable
using Vision.Core.Results;

namespace Vision.Core.Geometry;

/// <summary>明确帧/像素空间的有限点快照；可为显式参考点或交点，不自动裁切到视野内。</summary>
public sealed class PixelPoint
{
    public Guid FrameId
    {
        get;
    }

    public Guid CoordinateSpaceId
    {
        get;
    }

    public Point2D Position
    {
        get;
    }

    public PixelPoint(Guid frameId, Guid coordinateSpaceId, Point2D position)
    {
        if (frameId == Guid.Empty || coordinateSpaceId == Guid.Empty
            || !double.IsFinite(position.X) || !double.IsFinite(position.Y))
        {
            throw new ArgumentException("像素点必须具有有效帧、空间与有限坐标。");
        }

        FrameId = frameId;
        CoordinateSpaceId = coordinateSpaceId;
        Position = position;
    }

    /// <summary>仅当筛选后的Blob恰有一个时取得中心；空或多目标明确拒绝，不猜测目标。</summary>
    public static PixelPoint FromUniqueBlobCenter(BlobOutput source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Items.Count != 1)
        {
            throw new ArgumentException("唯一中心要求本次筛选结果恰有一个区域。", nameof(source));
        }

        return new(source.FrameId, source.CoordinateSpaceId, source.Items[0].Center);
    }

    /// <summary>显式选择本次Blob的一基Index，不默认首项，不赋予跨帧跟踪身份。</summary>
    public static PixelPoint FromBlobCenter(BlobOutput source, int itemIndex)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (itemIndex < 1 || itemIndex > source.Items.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(itemIndex));
        }

        return new(source.FrameId, source.CoordinateSpaceId, source.Items[itemIndex - 1].Center);
    }
}
