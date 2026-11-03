using Vision.Core.Operations;
using Vision.Core.Results;
using Vision.Tools.Execution;

namespace Vision.Tools.Common;

internal static partial class CommonVisionOperations
{
    private static BackendOutput JudgeAiPrediction(AiPredictionJudgmentOperation operation)
    {
        var source = operation.Source;
        var actual = operation.PredictionIndex < source.Predictions.Count ? source.Predictions[operation.PredictionIndex] : null;
        var passed = actual != null && string.Equals(actual.ClassId, operation.ExpectedClassId, StringComparison.Ordinal)
            && actual.Score >= operation.MinimumScore;
        return new(new AiPredictionJudgmentOutput(source, operation.PredictionIndex, operation.ExpectedClassId, operation.MinimumScore, passed));
    }
}
