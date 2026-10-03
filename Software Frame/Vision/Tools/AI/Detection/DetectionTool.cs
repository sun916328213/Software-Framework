// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：DetectionParameters、DetectionTool。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Tools.AI、Vision.Core.AI、Vision.Tools.AI.Internal。导入不代表所有类型都会在运行时加载。
using Vision.Tools.AI;
// （拆分说明见 Classification/ClassificationTool.cs 文件头。）

using Vision.Core.AI;
using Vision.Tools.AI.Internal;

namespace Vision.Tools.AI.Detection;

/// <summary>检测参数。</summary>
public sealed class DetectionParameters : AIModelParameters
{
    /// <summary>
    /// 最小像素面积过滤；≤0 关闭。由 Tool 层后过滤统一保证，
    /// 不依赖各 Provider 是否实现面积过滤（三家不强求 1:1，见计划 §6）。
    /// </summary>
    public double MinArea { get; set; }
}

/// <summary>
/// 检测工具：输出带类别的框（置信度降序由 Provider 保证，Tool 再截断到 MaxResults）。
/// </summary>
public sealed class DetectionTool : AIToolBase<DetectionParameters, AIDetectionResult>
{
    /// <inheritdoc/>
    public override string Name => "Detection";

    /// <inheritdoc/>
    public override AIModelType ModelType => AIModelType.Detection;

    /// <inheritdoc/>
    public override AICapabilities RequiredCapability => AICapabilities.Detection;

    /// <inheritdoc/>
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 FilterResult。
    /// 正文中的关键协作调用包括 RemoveAll、RemoveRange；具体条件和先后次序见方法体。
    /// </remarks>
    protected override void FilterResult(AIDetectionResult result, DetectionParameters parameters)
    {
        if (parameters.MinArea > 0)
            result.Boxes.RemoveAll(box => box.Box.Area < parameters.MinArea);
        if (parameters.MaxResults > 0 && result.Boxes.Count > parameters.MaxResults)
            result.Boxes.RemoveRange(parameters.MaxResults, result.Boxes.Count - parameters.MaxResults);
    }
}
