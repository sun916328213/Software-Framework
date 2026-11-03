#nullable enable

namespace Vision.Core.Capabilities;

/// <summary>
/// 操作所需的可选能力；实现不支持时必须明确拒绝。
/// </summary>
[Flags]
public enum VisionOperationFeatures
{
    /// <summary>
    /// 不需要额外的可选能力。
    /// </summary>
    None = 0,

    /// <summary>
    /// 旋转搜索。
    /// </summary>
    Rotation = 1,

    /// <summary>
    /// 正的等比例缩放。
    /// </summary>
    UniformScale = 2,

    /// <summary>
    /// 模板参与域掩膜。
    /// </summary>
    TemplateMask = 4,

    /// <summary>
    /// 模型锚点的允许落点掩膜。
    /// </summary>
    SearchMask = 8,

    /// <summary>
    /// 返回多个匹配目标。
    /// </summary>
    MultipleMatches = 16,

    /// <summary>
    /// 忽略模板极性差异。
    /// </summary>
    IgnorePolarity = 32,

    /// <summary>
    /// 显式使用实现自身的自动搜索与精化方式。
    /// </summary>
    NativeAutomaticSearch = 64,

    /// <summary>
    /// 明确使用处理参与域；不同于模板掩膜或锚点搜索域。
    /// </summary>
    ProcessingRegion = 128
}
