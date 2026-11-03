#nullable enable

namespace Vision.Core.Results;

/// <summary>一次卡尺直线结果及完整卡尺诊断；无拟合仍保留本次选点，不返回旧直线。</summary>
public sealed class CaliperLineOutput
{
    public Guid FrameId
    {
        get;
    }

    public Guid CoordinateSpaceId
    {
        get;
    }

    public CaliperLineFitState State
    {
        get;
    }

    public CaliperLineFit? Line
    {
        get;
    }

    public IReadOnlyList<CaliperEdgeSample> Samples
    {
        get;
    }

    public CaliperLineOutput(Guid frameId, Guid coordinateSpaceId, CaliperLineFitState state,
        CaliperLineFit? line, IEnumerable<CaliperEdgeSample> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (frameId == Guid.Empty || coordinateSpaceId == Guid.Empty || !Enum.IsDefined(state)
            || (state == CaliperLineFitState.Fitted) != (line != null))
        {
            throw new ArgumentException("卡尺结果必须具有有效来源与一致拟合状态。");
        }

        var snapshot = samples.ToArray();
        if (snapshot.Length < 2 || snapshot.Where((sample, index) => sample == null || sample.Index != index).Any()
            || line == null && snapshot.Any(sample => sample.UsedInFit)
            || line != null && snapshot.Count(sample => sample.UsedInFit) != line.PointCount)
        {
            throw new ArgumentException("卡尺诊断必须按零基顺序完整提供，参与点数须与直线一致。", nameof(samples));
        }

        FrameId = frameId;
        CoordinateSpaceId = coordinateSpaceId;
        State = state;
        Line = line;
        Samples = Array.AsReadOnly(snapshot);
    }
}
