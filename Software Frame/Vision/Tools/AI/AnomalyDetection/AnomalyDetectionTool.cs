// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：AnomalyDetectionParameters、AnomalyDetectionTool。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Tools.AI、Vision.Core.AI、Vision.Tools.AI.Internal。导入不代表所有类型都会在运行时加载。
using Vision.Tools.AI;
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
