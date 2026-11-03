#nullable enable

namespace Vision.Core.Results;

/// <summary>本次所有卡尺配对快照；完整对数量不足时Statistics为空，仍保留本次局部诊断。</summary>
public sealed class CaliperEdgePairOutput
{
    public Guid FrameId
    {
        get;
    }

    public Guid CoordinateSpaceId
    {
        get;
    }

    public CaliperWidthStatistics? Statistics
    {
        get;
    }

    public IReadOnlyList<CaliperEdgePairSample> Samples
    {
        get;
    }

    public CaliperEdgePairOutput(Guid frameId, Guid coordinateSpaceId,
        CaliperWidthStatistics? statistics, IEnumerable<CaliperEdgePairSample> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        var snapshot = samples.ToArray();
        if (frameId == Guid.Empty || coordinateSpaceId == Guid.Empty || snapshot.Length < 2
            || snapshot.Where((sample, index) => sample == null || sample.Index != index).Any()
            || statistics != null && (statistics.Mean.FrameId != frameId || statistics.Mean.CoordinateSpaceId != coordinateSpaceId
                || statistics.PairCount != snapshot.Count(sample => sample.WidthPixels != null)))
        {
            throw new ArgumentException("边缘对结果的来源、诊断顺序或统计数量无效。");
        }

        FrameId = frameId;
        CoordinateSpaceId = coordinateSpaceId;
        Statistics = statistics;
        Samples = Array.AsReadOnly(snapshot);
    }
}
