#nullable enable
using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Imaging;
using Vision.Core.Models;
using Vision.Core.Results;

namespace Vision.Core.Operations;

/// <summary>
/// 厂商中立的模板搜索；模板有效域在模型定义中，搜索有效域在本请求中。
/// </summary>
public sealed class TemplateMatchOperation : VisionImageOperation<TemplateMatchOutput>
{
    public const string Id = "vision.match.template";

    /// <summary>
    /// 操作的稳定标识。
    /// </summary>
    public override string OperationId => Id;

    /// <summary>
    /// 该操作所属的执行域。
    /// </summary>
    public override VisionExecutionDomain Domain => VisionExecutionDomain.Vision;

    /// <summary>
    /// 本次搜索的角度和缩放范围。
    /// </summary>
    public TemplateSearchRange SearchRange
    {
        get;
    }

    /// <summary>
    /// 限制模型锚点允许落入的位置（最近像素中心），不是遮挡搜索图像素的掩膜。
    /// </summary>
    public VisionMask? SearchMask
    {
        get;
    }

    /// <summary>
    /// 本次请求明确采用的采样方式。
    /// </summary>
    public TemplateSearchSampling Sampling
    {
        get;
    }

    /// <summary>
    /// 本次请求允许的最低得分。
    /// </summary>
    public double MinimumScore
    {
        get;
    }

    /// <summary>
    /// 匹配目标之间允许的最大重叠比例。
    /// </summary>
    public double MaximumOverlap
    {
        get;
    }

    /// <summary>
    /// 本次最多返回的匹配目标数量。
    /// </summary>
    public int MaximumMatches
    {
        get;
    }

    /// <summary>
    /// 本次请求必须满足的可选能力。
    /// </summary>
    public override VisionOperationFeatures RequiredFeatures
    {
        get
        {
            var definition = (TemplateModelDefinition)Model!.Definition;
            var features = VisionOperationFeatures.None;
            if (SearchRange.AngleStartDegrees != 0 || SearchRange.AngleExtentDegrees != 0)
            {
                features |= VisionOperationFeatures.Rotation;
            }

            if (SearchRange.ScaleMinimum != 1 || SearchRange.ScaleMaximum != 1)
            {
                features |= VisionOperationFeatures.UniformScale;
            }

            if (definition.Mask != null)
            {
                features |= VisionOperationFeatures.TemplateMask;
            }

            if (SearchMask != null)
            {
                features |= VisionOperationFeatures.SearchMask;
            }

            if (Sampling.UsesNativeSearch)
            {
                features |= VisionOperationFeatures.NativeAutomaticSearch;
            }

            if (MaximumMatches > 1)
            {
                features |= VisionOperationFeatures.MultipleMatches;
            }

            if (definition.IgnorePolarity)
            {
                features |= VisionOperationFeatures.IgnorePolarity;
            }

            return features;
        }
    }

    /// <summary>
    /// 创建模板匹配请求，校验得分、目标数量、重叠比例和搜索域。
    /// </summary>
    public TemplateMatchOperation(
        VisionFrame image,
        VisionModelReference model,
        TemplateSearchRange searchRange,
        double minimumScore = 0.8,
        int maximumMatches = 1,
        double maximumOverlap = 0.5,
        VisionMask? searchMask = null,
        TemplateSearchSampling? sampling = null)
        : base(image, model)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (model.Definition is not TemplateModelDefinition)
        {
            throw new ArgumentException("需要模板模型。", nameof(model));
        }

        ArgumentNullException.ThrowIfNull(searchRange);
        if (!double.IsFinite(minimumScore) || minimumScore < 0 || minimumScore > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumScore));
        }

        if (!double.IsFinite(maximumOverlap) || maximumOverlap < 0 || maximumOverlap > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumOverlap));
        }

        if (maximumMatches <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumMatches));
        }

        searchMask?.EnsureSize(image.Width, image.Height);
        SearchRange = searchRange;
        MinimumScore = minimumScore;
        MaximumMatches = maximumMatches;
        MaximumOverlap = maximumOverlap;
        SearchMask = searchMask;
        Sampling = sampling ?? new();
    }

    protected override void ValidateParameters(List<VisionIssue> issues)
    {
        if (!((TemplateModelDefinition)Model!.Definition).PreparedRange.Contains(SearchRange))
        {
            issues.Add(new("ModelRangeMismatch", "搜索角度或缩放超出模型准备范围，必须重新准备模型。"));
        }
    }
}
