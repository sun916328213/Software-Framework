// （拆分说明见 Classification/ClassificationTool.cs 文件头。）

using Vision.Core.AI;
using Vision.Tools.AI.Internal;

namespace Vision.Tools.AI.Segmentation;

/// <summary>分割参数。</summary>
public sealed class SegmentationParameters : AIModelParameters
{
    /// <summary>
    /// 最小像素面积过滤（按外接框面积）；≤0 关闭。由 Tool 层后过滤统一保证。
    /// </summary>
    public double MinArea { get; set; }
}

/// <summary>
/// 分割工具：输出区域 / 轮廓（外接框 + 多边形 + 可选掩膜，见 Core 公共结果）。
/// </summary>
public sealed class SegmentationTool : AIToolBase<SegmentationParameters, AISegmentationResult>
{
    /// <inheritdoc/>
    public override string Name => "Segmentation";

    /// <inheritdoc/>
    public override AIModelType ModelType => AIModelType.Segmentation;

    /// <inheritdoc/>
    public override AICapabilities RequiredCapability => AICapabilities.Segmentation;

    /// <inheritdoc/>
    protected override void FilterResult(AISegmentationResult result, SegmentationParameters parameters)
    {
        if (parameters.MinArea > 0)
            result.Items.RemoveAll(item => item.Bounds.Area < parameters.MinArea);
        if (parameters.MaxResults > 0 && result.Items.Count > parameters.MaxResults)
            result.Items.RemoveRange(parameters.MaxResults, result.Items.Count - parameters.MaxResults);
    }
}
