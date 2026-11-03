#nullable enable
using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Imaging;
using Vision.Core.Results;
using Vision.Core.Roi;

namespace Vision.Core.Operations;

/// <summary>
/// 整数灰度闭区间阈值：参与域内 Minimum ≤ pixel ≤ Maximum 输出255，其余像素为0。
/// 不需要模型；不指定处理域时使用全图，处理域外始终清零。
/// </summary>
public sealed class ThresholdRangeOperation : VisionImageOperation<BinaryImageOutput>
{
    public const string Id = "vision.processing.threshold-range";

    /// <summary>
    /// 操作的稳定标识。
    /// </summary>
    public override string OperationId => Id;

    /// <summary>
    /// 该操作所属的执行域。
    /// </summary>
    public override VisionExecutionDomain Domain => VisionExecutionDomain.Vision;

    /// <summary>
    /// 本次请求必须满足的可选能力。
    /// </summary>
    public override VisionOperationFeatures RequiredFeatures => Region == null
        ? VisionOperationFeatures.None : VisionOperationFeatures.ProcessingRegion;

    /// <summary>冻结的处理参与域，坐标属于本次输入图像；null 表示全图。</summary>
    public SearchRegion? Region
    {
        get;
    }

    /// <summary>
    /// 参与处理的最小灰度值，包含边界。
    /// </summary>
    public ushort Minimum
    {
        get;
    }

    /// <summary>
    /// 参与处理的最大灰度值，包含边界。
    /// </summary>
    public ushort Maximum
    {
        get;
    }

    /// <summary>
    /// 创建包含上下边界的整数灰度区间阈值请求。
    /// </summary>
    public ThresholdRangeOperation(
        VisionFrame image,
        ushort minimum,
        ushort maximum,
        SearchRegion? region = null) : base(image)
    {
        if (minimum > maximum)
        {
            throw new ArgumentException("阈值下限不能高于上限。", nameof(minimum));
        }

        if (region != null && !region.Runs(image.Width, image.Height).Any())
        {
            throw new ArgumentException("处理域与图像没有有效参与像素。", nameof(region));
        }

        Minimum = minimum;
        Maximum = maximum;
        Region = region;
    }

    protected override void ValidateParameters(List<VisionIssue> issues)
    {
        if (Image.Format == PixelFormat.Mono8 && Maximum > byte.MaxValue)
        {
            issues.Add(new("ThresholdRangeMismatch", "Mono8阈值不能超出0..255，不进行截断或隐式升位深。"));
        }
    }
}
