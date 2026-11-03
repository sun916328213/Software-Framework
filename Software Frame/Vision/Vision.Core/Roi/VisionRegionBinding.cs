#nullable enable
using Vision.Core.Imaging;
using Vision.Core.Models;
using Vision.Core.Results;

namespace Vision.Core.Roi;

/// <summary>
/// 固定参与域，或由明确模板匹配候选驱动的参与域。只保存冻结值，不修改基准ROI。
/// 跟随域的基准坐标为模板局部坐标；实际结果发布与模型存活由引擎核验。
/// </summary>
public sealed class VisionRegionBinding
{
    public SearchRegion ReferenceRegion
    {
        get;
    }
    public TemplateMatchOutput? Location
    {
        get;
    }
    public VisionModelReference? LocationModel
    {
        get;
    }

    /// <summary>匹配结果列表的零基下标，不能默认拿第一个目标。</summary>
    public int CandidateIndex
    {
        get;
    }

    public VisionRegionBinding(SearchRegion fixedRegion)
    {
        ReferenceRegion = fixedRegion ?? throw new ArgumentNullException(nameof(fixedRegion));
    }

    private VisionRegionBinding(SearchRegion templateRegion, TemplateMatchOutput location,
        VisionModelReference model, int candidateIndex)
    {
        ArgumentNullException.ThrowIfNull(templateRegion);
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(model);
        if (model.Definition is not TemplateModelDefinition)
        {
            throw new ArgumentException("跟随ROI需要模板模型。", nameof(model));
        }

        if (candidateIndex < 0 || candidateIndex >= location.Matches.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(candidateIndex), "必须显式选择本次有效匹配候选。");
        }

        ReferenceRegion = templateRegion;
        Location = location;
        LocationModel = model;
        CandidateIndex = candidateIndex;
    }

    public static VisionRegionBinding Follow(SearchRegion templateRegion, TemplateMatchOutput location,
        VisionModelReference model, int candidateIndex)
    {
        return new VisionRegionBinding(templateRegion, location, model, candidateIndex);
    }

    /// <summary>只做纯几何换算，不能证明模型、结果或图像实例有效；引擎还须核验原输入快照。</summary>
    public SearchRegion Resolve(VisionFrame image)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (Location == null)
        {
            return ReferenceRegion;
        }

        if (image.FrameId != Location.FrameId || image.CoordinateSpaceId != Location.CoordinateSpaceId)
        {
            throw new ArgumentException("跟随定位不能跨输入帧或坐标空间复用；派生图/裁剪需单独的明确映射。", nameof(image));
        }

        return ReferenceRegion.TransformSimilarity(Location.Matches[CandidateIndex].TemplateToImage);
    }
}
