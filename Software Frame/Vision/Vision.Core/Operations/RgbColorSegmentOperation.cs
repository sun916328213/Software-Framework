#nullable enable
using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Imaging;
using Vision.Core.Results;
using Vision.Core.Roi;

namespace Vision.Core.Operations;

/// <summary>
/// 固定输入像素域内RGB三通道闭区间分割，输出同尺寸/空间的新Mono8二值帧，域外始终0。
/// 不自动忽略alpha、转换灰度或推断HSV；本批不提供跨灰度定位分支的跟随ROI。
/// </summary>
public sealed class RgbColorSegmentOperation : VisionImageOperation<BinaryImageOutput>
{
    public const string Id = "vision.processing.rgb-color-segment";
    public override string OperationId => Id;
    public override VisionExecutionDomain Domain => VisionExecutionDomain.Vision;
    public override VisionOperationFeatures RequiredFeatures => Region == null
        ? VisionOperationFeatures.None : VisionOperationFeatures.ProcessingRegion;
    public RgbColorRange Range
    {
        get;
    }
    public SearchRegion? Region
    {
        get;
    }

    public RgbColorSegmentOperation(VisionFrame image, RgbColorRange range, SearchRegion? region = null) : base(image)
    {
        ArgumentNullException.ThrowIfNull(range);
        if (region != null && !region.Runs(image.Width, image.Height).Any())
        {
            throw new ArgumentException("颜色分割处理域与输入图像没有有效参与像素。", nameof(region));
        }
        Range = range;
        Region = region;
    }

    protected override void ValidateParameters(List<VisionIssue> issues)
    {
    }
}
