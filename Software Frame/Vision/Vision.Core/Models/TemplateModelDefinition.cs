#nullable enable
using Vision.Core.Geometry;
using Vision.Core.Imaging;

namespace Vision.Core.Models;

/// <summary>
/// 模板、有效掩膜、锚点和建模范围是同一配方快照；任一改变必须重新准备模型。
/// </summary>
public sealed class TemplateModelDefinition : VisionModelDefinition
{
    /// <summary>
    /// 模型种类。
    /// </summary>
    public override VisionModelKind Kind => VisionModelKind.Template;

    /// <summary>
    /// 模板图像的只读像素快照。
    /// </summary>
    public VisionFrame Template
    {
        get;
    }

    /// <summary>
    /// 模板参与域掩膜；为空时使用整个模板。
    /// </summary>
    public VisionMask? Mask
    {
        get;
    }

    /// <summary>
    /// 模板局部坐标中的匹配锚点。
    /// </summary>
    public Point2D Origin
    {
        get;
    }

    /// <summary>
    /// 模型准备时覆盖的角度和缩放范围。
    /// </summary>
    public TemplateSearchRange PreparedRange
    {
        get;
    }

    /// <summary>
    /// 用于比较模板与搜索图像的评价方式。
    /// </summary>
    public TemplateMatchMetric Metric
    {
        get;
    }

    /// <summary>
    /// 是否要求忽略模板与目标之间的极性差异。
    /// </summary>
    public bool IgnorePolarity
    {
        get;
    }

    /// <summary>
    /// 创建模板定义，校验锚点、匹配方式和参与域尺寸。
    /// </summary>
    public TemplateModelDefinition(
        VisionFrame template,
        Point2D origin,
        TemplateSearchRange preparedRange,
        VisionMask? mask = null,
        TemplateMatchMetric metric = TemplateMatchMetric.GrayCorrelation,
        bool ignorePolarity = false)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(preparedRange);
        if (!double.IsFinite(origin.X) || !double.IsFinite(origin.Y))
        {
            throw new ArgumentException("模板锚点必须有限。", nameof(origin));
        }

        if (!Enum.IsDefined(metric))
        {
            throw new ArgumentOutOfRangeException(nameof(metric));
        }

        mask?.EnsureSize(template.Width, template.Height);
        Template = template;
        Origin = origin;
        PreparedRange = preparedRange;
        Mask = mask;
        Metric = metric;
        IgnorePolarity = ignorePolarity;
    }
}
