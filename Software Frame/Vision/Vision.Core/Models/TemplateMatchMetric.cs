#nullable enable

namespace Vision.Core.Models;

/// <summary>
/// 模板匹配采用的评价方式，不同评价方式的分数不可直接互换。
/// </summary>
public enum TemplateMatchMetric
{
    /// <summary>
    /// 灰度相关匹配。
    /// </summary>
    GrayCorrelation,

    /// <summary>
    /// 形状边缘匹配。
    /// </summary>
    ShapeEdges
}
