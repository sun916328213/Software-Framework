#nullable enable
using Vision.Core.Imaging;
using Vision.Core.Models;
using Vision.Core.Results;

namespace Vision.Core.Roi;

/// <summary>
/// 本次定位到模板局部测量基准的明确绑定；候选零基下标必须显式指定。
/// 只提供几何值，不证明结果发布/模型存活/图像实例有效；引擎还须核验原结果及当前修订。
/// </summary>
public sealed class TemplateLocationBinding
{
    public TemplateMatchOutput Location
    {
        get;
    }

    public VisionModelReference Model
    {
        get;
    }

    public int CandidateIndex
    {
        get;
    }

    public TemplateLocationBinding(TemplateMatchOutput location, VisionModelReference model, int candidateIndex)
    {
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(model);
        if (model.Definition is not TemplateModelDefinition)
        {
            throw new ArgumentException("跟随测量需要模板模型。", nameof(model));
        }

        if (candidateIndex < 0 || candidateIndex >= location.Matches.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(candidateIndex), "必须显式选择本次有效匹配候选。");
        }

        Location = location;
        Model = model;
        CandidateIndex = candidateIndex;
    }

    /// <summary>仅做帧/空间标签核验，实际输入对象和模型当前修订由引擎认证；不返回可变SDK对象。</summary>
    public TemplateMatchCandidate Resolve(VisionFrame image)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (image.FrameId != Location.FrameId || image.CoordinateSpaceId != Location.CoordinateSpaceId)
        {
            throw new ArgumentException("跟随测量不能跨帧/空间复用定位；裁剪或派生图须先有明确映射。", nameof(image));
        }

        return Location.Matches[CandidateIndex];
    }
}
