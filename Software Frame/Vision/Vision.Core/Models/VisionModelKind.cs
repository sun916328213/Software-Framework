#nullable enable

namespace Vision.Core.Models;

/// <summary>
/// 需要准备或加载的模型种类。
/// </summary>
public enum VisionModelKind
{
    /// <summary>
    /// 传统视觉模板模型。
    /// </summary>
    Template,

    /// <summary>
    /// 单标签图像分类模型。
    /// </summary>
    AiClassification
}
