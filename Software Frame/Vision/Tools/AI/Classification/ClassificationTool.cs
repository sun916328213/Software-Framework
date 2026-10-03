// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：ClassificationParameters、ClassificationTool。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Tools.AI、Vision.Core.AI、Vision.Tools.AI.Internal。导入不代表所有类型都会在运行时加载。
using Vision.Tools.AI;
// ─────────────────────────────────────────────────────────────────────────────
// P5 —— ClassificationTool / DetectionTool（计划 §6 结果表前两行）。
//
// 拆分来源：旧 AITool（DPLibrary_Halcon_F48/Tools/AI/AITool.cs）用 AIBrand +
// AIToolType 两个枚举撑起全部分类/检测/分割/异常任务，参数（LongSide / ShortSide /
// MinArea / MinScore…）对所有任务可见。新模型每任务一 Tool，参数只含本任务所需。
//
// 旧语义映射：MinScore → ScoreThreshold；LongSide / ShortSide（mm 长短边，依赖物理
// 标定）P5 不带入，P7 坐标链就绪后再以像素/物理双语义补回；MinArea（mm²）→ 像素
// 面积过滤（MinArea，≤0 关闭），由 Tool 层统一后过滤，不依赖 Provider 实现。
// ─────────────────────────────────────────────────────────────────────────────

using Vision.Core.AI;
using Vision.Tools.AI.Internal;

namespace Vision.Tools.AI.Classification;

/// <summary>分类参数（公共参数即全部；保留独立类型供将来扩展分类专有阈值）。</summary>
public sealed class ClassificationParameters : AIModelParameters
{
}

/// <summary>
/// 分类工具：整图/整 ROI 判一个类别。结果即 Core 公共结果（统一结果语义）。
/// </summary>
public sealed class ClassificationTool : AIToolBase<ClassificationParameters, AIClassificationResult>
{
    /// <inheritdoc/>
    public override string Name => "Classification";

    /// <inheritdoc/>
    public override AIModelType ModelType => AIModelType.Classification;

    /// <inheritdoc/>
    public override AICapabilities RequiredCapability => AICapabilities.Classification;
}
