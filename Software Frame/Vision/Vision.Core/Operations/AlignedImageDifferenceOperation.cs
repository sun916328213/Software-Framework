#nullable enable
using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Imaging;
using Vision.Core.Results;
using Vision.Core.Roi;

namespace Vision.Core.Operations;

/// <summary>
/// 明确当前图与参考图的同位深灰度差分并阈值成二值缺陷候选；同尺寸/空间标签只表示调用方声明对齐，
/// 不是内容配准证明。不偷读全局背景、自动配准或补偿光照；阈值为原位深正编码值。
/// </summary>
public sealed class AlignedImageDifferenceOperation : VisionImageOperation<ImageDifferenceOutput>
{
    public const string Id = "vision.inspection.aligned-image-difference";
    public override string OperationId => Id;
    public override VisionExecutionDomain Domain => VisionExecutionDomain.Vision;
    public override VisionOperationFeatures RequiredFeatures => Region == null
        ? VisionOperationFeatures.None : VisionOperationFeatures.ProcessingRegion;
    public VisionFrame Current => Image;
    public VisionFrame Reference
    {
        get;
    }
    public ImageDifferenceDirection Direction
    {
        get;
    }
    public ushort MinimumDifference
    {
        get;
    }
    public ushort MaximumDifference
    {
        get;
    }
    public SearchRegion? Region
    {
        get;
    }

    public AlignedImageDifferenceOperation(VisionFrame current, VisionFrame reference, ImageDifferenceDirection direction,
        ushort minimumDifference, ushort maximumDifference, SearchRegion? region = null) : base(current)
    {
        ArgumentNullException.ThrowIfNull(reference);
        if (current.Width != reference.Width || current.Height != reference.Height || current.Format != reference.Format
            || current.CoordinateSpaceId != reference.CoordinateSpaceId)
        {
            throw new ArgumentException("差分两图必须同尺寸、同格式及同像素空间；必须先显式对齐，不猜映射。", nameof(reference));
        }
        if (current.FrameId == reference.FrameId && !ReferenceEquals(current, reference))
        {
            throw new ArgumentException("不同输入快照不能复用同一帧身份，避免双图来源歧义。", nameof(reference));
        }
        if (!Enum.IsDefined(direction))
        {
            throw new ArgumentOutOfRangeException(nameof(direction));
        }
        if (minimumDifference == 0 || minimumDifference > maximumDifference)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumDifference), "差分阈值必须为正有序闭区间，不把无差异0当缺陷。");
        }
        if (region != null && !region.Runs(current.Width, current.Height).Any())
        {
            throw new ArgumentException("差分处理域与两图没有有效参与像素。", nameof(region));
        }
        Reference = reference;
        Direction = direction;
        MinimumDifference = minimumDifference;
        MaximumDifference = maximumDifference;
        Region = region;
    }

    protected override void ValidateParameters(List<VisionIssue> issues)
    {
        if (Image.Format == PixelFormat.Mono8 && MaximumDifference > byte.MaxValue)
        {
            issues.Add(new("DifferenceRangeMismatch", "Mono8差分阈值不能超过255，不隐式升位深或截断。"));
        }
    }
}
