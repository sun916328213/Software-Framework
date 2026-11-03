#nullable enable

namespace Vision.Core.Results;

/// <summary>
/// 多QR的完整原生返回候选快照，包括未解码候选，不按相同文本去重。
/// Candidates按角点Ymin、Xmin、Ymax、Xmax排序，同键保留原生顺序，不保证跨次身份；
/// 零基下标不是跨帧跟踪ID或真实场景码总数。
/// </summary>
public sealed class QrCodeReadManyOutput
{
    public Guid FrameId
    {
        get;
    }

    public Guid CoordinateSpaceId
    {
        get;
    }

    public IReadOnlyList<QrCodeReadOutput> Candidates
    {
        get;
    }

    public int DetectedCount => Candidates.Count;
    public int DecodedCount => Candidates.Sum(candidate => candidate.DecodedCount);

    public QrCodeReadManyOutput(Guid frameId, Guid coordinateSpaceId, IEnumerable<QrCodeReadOutput> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        var snapshot = candidates.Take(33).ToArray();
        if (frameId == Guid.Empty || coordinateSpaceId == Guid.Empty || snapshot.Length > 32
            || snapshot.Any(candidate => candidate == null || candidate.FrameId != frameId
                || candidate.CoordinateSpaceId != coordinateSpaceId || candidate.State == QrCodeReadState.NotDetected))
        {
            throw new ArgumentException("多QR候选须同源、最多32且确已检测，不以无检测占位或异帧候选充数。");
        }
        for (var i = 0; i < snapshot.Length; i++)
        {
            for (var j = 0; j < i; j++)
            {
                if (snapshot[i].Corners.All(point => snapshot[j].Corners.Contains(point)))
                {
                    throw new ArgumentException("不能把完全相同四角几何重复计为不同候选；不会按文本去重。");
                }
            }
        }
        var ordered = snapshot.OrderBy(candidate => candidate.Corners.Min(point => point.Y))
            .ThenBy(candidate => candidate.Corners.Min(point => point.X))
            .ThenBy(candidate => candidate.Corners.Max(point => point.Y))
            .ThenBy(candidate => candidate.Corners.Max(point => point.X)).ToArray();
        if (!snapshot.SequenceEqual(ordered))
        {
            throw new ArgumentException("多QR候选必须按声明几何键排序；同键不提供稳定候选身份。");
        }
        FrameId = frameId;
        CoordinateSpaceId = coordinateSpaceId;
        Candidates = Array.AsReadOnly(snapshot);
    }
}
