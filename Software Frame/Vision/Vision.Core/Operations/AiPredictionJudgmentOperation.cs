#nullable enable
using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Results;

namespace Vision.Core.Operations;

/// <summary>
/// 对已完成分类快照中显式零基预测下标作类别Ordinal相等与最低分数闭边界判定，无图无SDK。
/// 下标不存在为NG，不自动换取其他预测；不是推理、完整后验或自动“正常/缺陷”语义。
/// </summary>
public sealed class AiPredictionJudgmentOperation : VisionOperation<AiPredictionJudgmentOutput>
{
    public const string Id = "vision.evaluation.ai-prediction";
    public override string OperationId => Id;
    public override VisionExecutionDomain Domain => VisionExecutionDomain.Vision;
    public override VisionOperationFeatures RequiredFeatures => VisionOperationFeatures.None;
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

    public AiPredictionJudgmentOperation(AiClassificationOutput source, int predictionIndex, string expectedClassId, double minimumScore)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedClassId);
        if (predictionIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(predictionIndex));
        }
        if (!double.IsFinite(minimumScore) || minimumScore < 0 || minimumScore > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumScore));
        }
        Source = source;
        PredictionIndex = predictionIndex;
        ExpectedClassId = expectedClassId;
        MinimumScore = minimumScore;
    }

    protected override void ValidateParameters(List<VisionIssue> issues)
    {
    }
}
