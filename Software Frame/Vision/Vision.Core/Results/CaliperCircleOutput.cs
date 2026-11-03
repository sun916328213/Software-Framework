#nullable enable

namespace Vision.Core.Results;

/// <summary>本次圆卡尺全部样本及有效圆；NoMatch保留诊断但Circle为空，残差是径向距离。</summary>
public sealed class CaliperCircleOutput
{
    public Guid FrameId
    {
        get;
    }

    public Guid CoordinateSpaceId
    {
        get;
    }

    public CaliperCircleFitState State
    {
        get;
    }

    public CaliperCircleFit? Circle
    {
        get;
    }

    public IReadOnlyList<CaliperEdgeSample> Samples
    {
        get;
    }

    public CaliperCircleOutput(Guid frameId, Guid coordinateSpaceId, CaliperCircleFitState state,
        CaliperCircleFit? circle, IEnumerable<CaliperEdgeSample> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        var snapshot = samples.ToArray();
        if (frameId == Guid.Empty || coordinateSpaceId == Guid.Empty || !Enum.IsDefined(state)
            || (state == CaliperCircleFitState.Fitted) != (circle != null) || snapshot.Length < 3
            || snapshot.Where((sample, index) => sample == null || sample.Index != index).Any()
            || circle == null && snapshot.Any(sample => sample.UsedInFit)
            || circle != null && (snapshot.Count(sample => sample.UsedInFit) != circle.PointCount
                || circle.Diameter.FrameId != frameId || circle.Diameter.CoordinateSpaceId != coordinateSpaceId))
        {
            throw new ArgumentException("圆卡尺来源、参与点数量或拟合状态不一致。");
        }

        FrameId = frameId;
        CoordinateSpaceId = coordinateSpaceId;
        State = state;
        Circle = circle;
        Samples = Array.AsReadOnly(snapshot);
    }
}
