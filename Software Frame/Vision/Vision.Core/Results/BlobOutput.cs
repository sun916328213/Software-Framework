#nullable enable

namespace Vision.Core.Results;

/// <summary>本次已完成全部筛选/排序的区域快照；空集合表示本次未找到，不携带旧区域。</summary>
public sealed class BlobOutput
{
    public Guid FrameId
    {
        get;
    }
    public Guid CoordinateSpaceId
    {
        get;
    }
    public IReadOnlyList<BlobItem> Items
    {
        get;
    }

    public BlobOutput(Guid frameId, Guid coordinateSpaceId, IEnumerable<BlobItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (frameId == Guid.Empty || coordinateSpaceId == Guid.Empty)
        {
            throw new ArgumentException("区域结果必须注明输入帧和坐标空间。");
        }

        var snapshot = items.ToArray();
        for (var i = 0; i < snapshot.Length; i++)
        {
            if (snapshot[i] == null || snapshot[i].Index != i + 1)
            {
                throw new ArgumentException("区域序号必须对应本次排序，连续从1开始。", nameof(items));
            }
        }

        FrameId = frameId;
        CoordinateSpaceId = coordinateSpaceId;
        Items = Array.AsReadOnly(snapshot);
    }
}
