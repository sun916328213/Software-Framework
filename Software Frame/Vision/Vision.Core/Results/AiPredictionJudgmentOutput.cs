#nullable enable

namespace Vision.Core.Results;

/// <summary>显式预测位置/期望类别/分数及完整分类快照的OK/NG；缺项或不符均为NG，不伪装推理失败。</summary>
public sealed class AiPredictionJudgmentOutput
{
    public AiClassificationOutput Source
    {
        get;
    }

    public int PredictionIndex
    {
        get;
    }

    public string ExpectedClassId
    {
        get;
    }

    public double MinimumScore
    {
        get;
    }

    public bool Passed
    {
        get;
    }

    public Guid FrameId => Source.FrameId;
    public Guid CoordinateSpaceId => Source.CoordinateSpaceId;
    public AiClassPrediction? ActualPrediction => PredictionIndex < Source.Predictions.Count ? Source.Predictions[PredictionIndex] : null;

    public AiPredictionJudgmentOutput(AiClassificationOutput source, int predictionIndex, string expectedClassId, double minimumScore, bool passed)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedClassId);
        if (predictionIndex < 0 || !double.IsFinite(minimumScore) || minimumScore < 0 || minimumScore > 1)
        {
            throw new ArgumentException("预测下标或分数范围无效，不推断缺省判定规则。");
        }
        var actual = predictionIndex < source.Predictions.Count ? source.Predictions[predictionIndex] : null;
        var expected = actual != null && string.Equals(actual.ClassId, expectedClassId, StringComparison.Ordinal) && actual.Score >= minimumScore;
        if (passed != expected)
        {
            throw new ArgumentException("分类判定必须符合本次显式预测位置、类别与分数。", nameof(passed));
        }
        Source = source;
        PredictionIndex = predictionIndex;
        ExpectedClassId = expectedClassId;
        MinimumScore = minimumScore;
        Passed = passed;
    }
}
