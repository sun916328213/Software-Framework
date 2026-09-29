// （拆分说明见 Classification/ClassificationTool.cs 文件头。）

using Vision.Core.AI;
using Vision.Tools.AI.Internal;

namespace Vision.Tools.AI.AnomalyDetection;

/// <summary>异常检测参数（公共参数即全部；异常阈值即 ScoreThreshold）。</summary>
public sealed class AnomalyDetectionParameters : AIModelParameters
{
}

/// <summary>
/// 异常检测工具：输出是否异常＋异常分数/位置。能力按 Provider 声明开启（计划 §6）。
/// </summary>
public sealed class AnomalyDetectionTool : AIToolBase<AnomalyDetectionParameters, AIAnomalyResult>
{
    /// <inheritdoc/>
    public override string Name => "AnomalyDetection";

    /// <inheritdoc/>
    public override AIModelType ModelType => AIModelType.AnomalyDetection;

    /// <inheritdoc/>
    public override AICapabilities RequiredCapability => AICapabilities.AnomalyDetection;
}
