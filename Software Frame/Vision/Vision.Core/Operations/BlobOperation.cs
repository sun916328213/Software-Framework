#nullable enable
using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Imaging;
using Vision.Core.Results;
using Vision.Core.Roi;

namespace Vision.Core.Operations;

/// <summary>
/// 原图闭区间阈值→明确参与域/孔洞策略→连通域→特征筛选与排序。
/// 不生成假模型，几何与面积均使用本次输入图像坐标；最大数量是安全上限，不静默截断。
/// </summary>
public sealed class BlobOperation : VisionImageOperation<BlobOutput>
{
    public const string Id = "vision.inspection.blob";
    public override string OperationId => Id;
    public override VisionExecutionDomain Domain => VisionExecutionDomain.Vision;
    public override VisionOperationFeatures RequiredFeatures => Region == null
        ? VisionOperationFeatures.None : VisionOperationFeatures.ProcessingRegion;

    public ushort Minimum
    {
        get;
    }
    public ushort Maximum
    {
        get;
    }
    public BlobConnectivity Connectivity
    {
        get;
    }
    public BlobHolePolicy HolePolicy
    {
        get;
    }
    public BlobSort Sort
    {
        get;
    }
    public BlobFilterLogic FilterLogic
    {
        get;
    }
    public IReadOnlyList<BlobFeatureRange> Filters
    {
        get;
    }
    public int MaximumResults
    {
        get;
    }
    public VisionRegionBinding? RegionBinding
    {
        get;
    }
    /// <summary>由基准几何一次推导的冻结参与域，不会累积改写ROI。</summary>
    public SearchRegion? Region
    {
        get;
    }

    public BlobOperation(VisionFrame image, ushort minimum, ushort maximum,
        VisionRegionBinding? regionBinding = null,
        BlobConnectivity connectivity = BlobConnectivity.Eight,
        BlobHolePolicy holePolicy = BlobHolePolicy.Preserve,
        IEnumerable<BlobFeatureRange>? filters = null,
        BlobFilterLogic filterLogic = BlobFilterLogic.All,
        BlobSort sort = BlobSort.AreaDescending,
        int maximumResults = 1000) : base(image)
    {
        if (minimum > maximum)
        {
            throw new ArgumentException("阈值下限不能高于上限。", nameof(minimum));
        }

        if (!Enum.IsDefined(connectivity) || !Enum.IsDefined(holePolicy)
            || !Enum.IsDefined(filterLogic) || !Enum.IsDefined(sort))
        {
            throw new ArgumentOutOfRangeException(nameof(connectivity));
        }

        if (maximumResults < 1 || maximumResults > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumResults), "结果上限支持1..1000，超出时失败而非截断计数。");
        }

        var snapshot = filters?.ToArray() ?? [];
        if (snapshot.Any(filter => filter == null))
        {
            throw new ArgumentException("特征范围不能包含空项。", nameof(filters));
        }

        Region = regionBinding?.Resolve(image);
        if (Region != null && !Region.Runs(image.Width, image.Height).Any())
        {
            throw new ArgumentException("Blob参与域与图像没有有效像素。", nameof(regionBinding));
        }

        Minimum = minimum;
        Maximum = maximum;
        RegionBinding = regionBinding;
        Connectivity = connectivity;
        HolePolicy = holePolicy;
        Sort = sort;
        Filters = Array.AsReadOnly(snapshot);
        FilterLogic = filterLogic;
        MaximumResults = maximumResults;
    }

    protected override void ValidateParameters(List<VisionIssue> issues)
    {
        if (Image.Format == PixelFormat.Mono8 && Maximum > byte.MaxValue)
        {
            issues.Add(new("ThresholdRangeMismatch", "Mono8的Blob阈值不能超出0..255。"));
        }
    }
}
