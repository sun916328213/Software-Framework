// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：SegmentationParameters、SegmentationTool。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Tools.AI、Vision.Core.AI、Vision.Tools.AI.Internal。导入不代表所有类型都会在运行时加载。
using Vision.Tools.AI;
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
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 FilterResult。
    /// 正文中的关键协作调用包括 RemoveAll、RemoveRange；具体条件和先后次序见方法体。
    /// </remarks>
    protected override void FilterResult(AISegmentationResult result, SegmentationParameters parameters)
    {
        if (parameters.MinArea > 0)
            result.Items.RemoveAll(item => item.Bounds.Area < parameters.MinArea);
        if (parameters.MaxResults > 0 && result.Items.Count > parameters.MaxResults)
            result.Items.RemoveRange(parameters.MaxResults, result.Items.Count - parameters.MaxResults);
    }
}
