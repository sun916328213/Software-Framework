#nullable enable
using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Imaging;
using Vision.Core.Models;
using Vision.Core.Results;

namespace Vision.Core.Operations;

/// <summary>
/// 分类与传统视觉使用同一个引擎执行入口，不允许请求指定AI Provider或设备。
/// </summary>
public sealed class AiClassificationOperation : VisionImageOperation<AiClassificationOutput>
{
    public const string Id = "vision.ai.classification";

    /// <summary>
    /// 操作的稳定标识。
    /// </summary>
    public override string OperationId => Id;

    /// <summary>
    /// 该操作所属的执行域。
    /// </summary>
    public override VisionExecutionDomain Domain => VisionExecutionDomain.Ai;

    /// <summary>
    /// 本次请求必须满足的可选能力。
    /// </summary>
    public override VisionOperationFeatures RequiredFeatures => VisionOperationFeatures.None;

    /// <summary>
    /// 本次请求允许的最低得分。
    /// </summary>
    public double MinimumScore
    {
        get;
    }

    /// <summary>
    /// 本次最多保留的分类结果数量。
    /// </summary>
    public int MaximumResults
    {
        get;
    }

    /// <summary>
    /// 创建分类请求，校验模型种类、得分和返回数量。
    /// </summary>
    public AiClassificationOperation(
        VisionFrame image,
        VisionModelReference model,
        double minimumScore = 0,
        int maximumResults = 1) : base(image, model)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (model.Definition is not AiClassificationModelDefinition)
        {
            throw new ArgumentException("需要AI分类模型。", nameof(model));
        }

        if (!double.IsFinite(minimumScore) || minimumScore < 0 || minimumScore > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumScore));
        }

        if (maximumResults <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumResults));
        }

        MinimumScore = minimumScore;
        MaximumResults = maximumResults;
    }

    protected override void ValidateParameters(List<VisionIssue> issues)
    {
    }
}
