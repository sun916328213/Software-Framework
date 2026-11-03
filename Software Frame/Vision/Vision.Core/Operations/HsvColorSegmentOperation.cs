#nullable enable
using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Imaging;
using Vision.Core.Results;
using Vision.Core.Roi;

namespace Vision.Core.Operations;

/// <summary>
/// 从明确Rgb24/Bgr24转换为H=0..179、S/V=0..255量化HSV并分割，固定域外0；
/// 色相跨零与无色相策略显式，不暗称连续色度/感知色差，不自动丢alpha或猜输入颜色空间。
/// </summary>
public sealed class HsvColorSegmentOperation : VisionImageOperation<BinaryImageOutput>
{
    public const string Id = "vision.processing.hsv8-color-segment";
    public override string OperationId => Id;
    public override VisionExecutionDomain Domain => VisionExecutionDomain.Vision;
    public override VisionOperationFeatures RequiredFeatures => Region == null
        ? VisionOperationFeatures.None : VisionOperationFeatures.ProcessingRegion;
    public HsvColorRange Range
    {
        get;
    }

    public SearchRegion? Region
    {
        get;
    }

    public HsvColorSegmentOperation(VisionFrame image, HsvColorRange range, SearchRegion? region = null) : base(image)
    {
        ArgumentNullException.ThrowIfNull(range);
        if (region != null && !region.Runs(image.Width, image.Height).Any())
        {
            throw new ArgumentException("HSV分割参与域与输入没有有效像素。", nameof(region));
        }
        Range = range;
        Region = region;
    }

    protected override void ValidateParameters(List<VisionIssue> issues)
    {
    }
}
